using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §10, §19 (AotSmoke) and INV-24: the NativeAOT console smoke. Staged (§24): the trimmed AOT-WASM step joins at
// stage 18 (M14-06).
internal sealed partial class Build
{
    private AbsolutePath AotSolution => RootDirectory / "Ducky.Aot.slnx";
    private AbsolutePath AotPublish => Artifacts / "aot";

    // Linux only (§10): Ducky.AotSmoke is restored unlocked (its csproj) outside the locked Ducky.slnx, published for
    // linux-x64 and run; SpecTraceGate never runs the binary, so its manifest check lives here.
    private Target AotSmoke => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var selfCheck = AotSmokeSelfCheck().ToList();
            selfCheck.ForEach(v => Log.Error(v));
            Assert.True(selfCheck.Count == 0, $"AotSmoke: {selfCheck.Count} self-check violation(s)");
            Assert.True(OperatingSystem.IsLinux(), "AotSmoke publishes Ducky.AotSmoke for linux-x64 and runs it: Linux only (SPEC §10)");

            DotNet("workload install wasm-tools");
            DotNet($"restore {AotSolution}");
            // PublishAot=true comes from the csproj: as a global -p: it would reach the netstandard2.0 generator (NETSDK1207).
            // Cleaned first, so files left by an earlier publish can't hide what this one produced.
            AotPublish.CreateOrCleanDirectory();
            DotNet($"publish {RootDirectory / "test" / AotSmokeProject / $"{AotSmokeProject}.csproj"} -c Release -r linux-x64 --no-restore -o {AotPublish}");
            var publishViolations = AotPublishViolations(AotPublish.GetFiles().Select(f => f.Name)).ToList();
            publishViolations.ForEach(v => Log.Error(v));
            Assert.True(publishViolations.Count == 0, $"AotSmoke: {publishViolations.Count} publish violation(s)");

            // A smoke assertion that deadlocks under NativeAOT fails here with a diagnostic, not at the runner's job limit.
            var timeout = TimeSpan.FromMinutes(5);
            var smoke = ProcessTasks.StartProcess(AotPublish / AotSmokeProject, workingDirectory: AotPublish,
                timeout: (int)timeout.TotalMilliseconds);
            Assert.True(smoke.WaitForExit(), $"AotSmoke: {AotSmokeProject} timed out after {timeout.TotalMinutes} minutes and was killed");

            var violations = AotSmokeViolations(smoke.Output.Select(o => o.Text).ToList(), smoke.ExitCode,
                ParseManifest(Manifest.ReadAllText())).ToList();
            violations.ForEach(v => Log.Error(v));
            Assert.True(violations.Count == 0, $"AotSmoke: {violations.Count} violation(s)");
        });

    // The binary must exit 0 and print only PASS lines, and its PASS names must equal the active Ducky.AotSmoke entries.
    private static IEnumerable<string> AotSmokeViolations(IReadOnlyList<string> output, int exitCode, TestManifest manifest)
    {
        if (exitCode != 0)
        {
            yield return $"{AotSmokeProject} exited with code {exitCode}";
        }
        foreach (var line in output.Where(l => !l.StartsWith("PASS ", StringComparison.Ordinal)))
        {
            yield return $"{AotSmokeProject} printed '{line}': only PASS <name> lines are allowed";
        }
        // One line per named assertion (§10): a second line for a name means an area story added a Check instead of
        // extending the existing assertion (P8), which a set comparison alone would let through.
        var duplicates = output
            .Where(l => l.StartsWith("PASS ", StringComparison.Ordinal) || l.StartsWith("FAIL ", StringComparison.Ordinal))
            .GroupBy(l => l["PASS ".Length..], StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Order(StringComparer.Ordinal);
        foreach (var name in duplicates)
        {
            yield return $"{AotSmokeProject} printed {name} more than once";
        }
        var expected = manifest.Tests
            .Where(t => t.Stage <= manifest.ActiveStage && t.Projects.Contains(AotSmokeProject))
            .Select(t => t.Name)
            .ToHashSet();
        var passed = output.Where(l => l.StartsWith("PASS ", StringComparison.Ordinal)).Select(l => l["PASS ".Length..]).ToHashSet();
        foreach (var name in passed.Except(expected).Order(StringComparer.Ordinal))
        {
            yield return $"{AotSmokeProject}: PASS {name} is not an active {AotSmokeProject} manifest entry";
        }
        foreach (var name in expected.Except(passed).Order(StringComparer.Ordinal))
        {
            yield return $"{AotSmokeProject} printed no PASS {name} (active {AotSmokeProject} manifest entry)";
        }
    }

    // The acceptance checks of M0-08, planted on a miniature manifest and binary output.
    private static IEnumerable<string> AotSmokeSelfCheck()
    {
        var manifest = new TestManifest
        {
            ActiveStage = 2,
            Tests =
            [
                new() { Name = "AotSmoke", Projects = [AotSmokeProject], Invariants = ["INV-24"], Stage = 1 },
                new() { Name = "ActionType_AttributeName_SurvivesAot", Projects = [AotSmokeProject], Stage = 6 },
                new() { Name = "Skeleton_{Project}_Smoke", Projects = ["Ducky.Tests"], Stage = 1 },
            ],
        };
        foreach (var violation in AotSmokeViolations(["PASS AotSmoke"], 0, manifest))
        {
            yield return $"AotSmokeSelfCheck: the unplanted output must pass, got: {violation}";
        }
        (string Case, List<string> Output, int ExitCode, string Expected)[] planted =
        [
            ("a FAIL line", ["PASS AotSmoke", "FAIL AotSmoke"], 1, "printed 'FAIL AotSmoke'"),
            ("a FAIL line with exit code 0", ["FAIL AotSmoke"], 0, "printed 'FAIL AotSmoke'"),
            ("a non-zero exit code", ["PASS AotSmoke"], 134, "exited with code 134"),
            ("a PASS name of an inactive entry", ["PASS AotSmoke", "PASS ActionType_AttributeName_SurvivesAot"], 0,
                "PASS ActionType_AttributeName_SurvivesAot is not an active Ducky.AotSmoke manifest entry"),
            ("a PASS name of another project's entry", ["PASS AotSmoke", "PASS Skeleton_{Project}_Smoke"], 0,
                "PASS Skeleton_{Project}_Smoke is not an active Ducky.AotSmoke manifest entry"),
            ("a PASS name absent from the manifest", ["PASS AotSmoke", "PASS Ghost_Name"], 0,
                "PASS Ghost_Name is not an active Ducky.AotSmoke manifest entry"),
            ("a missing active entry", [], 0, "Ducky.AotSmoke printed no PASS AotSmoke"),
            ("a duplicate PASS name", ["PASS AotSmoke", "PASS AotSmoke"], 0, "Ducky.AotSmoke printed AotSmoke more than once"),
            ("a duplicate name across PASS and FAIL", ["PASS AotSmoke", "FAIL AotSmoke"], 1, "Ducky.AotSmoke printed AotSmoke more than once"),
        ];
        foreach (var (plantedCase, output, exitCode, expected) in planted)
        {
            var violations = AotSmokeViolations(output, exitCode, manifest).ToList();
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"AotSmokeSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
        foreach (var violation in AotPublishViolations(["Ducky.AotSmoke", "Ducky.AotSmoke.dbg", "Ducky.pdb", "Ducky.xml"]))
        {
            yield return $"AotSmokeSelfCheck: a NativeAOT publish must pass, got: {violation}";
        }
        if (!AotPublishViolations(["Ducky.AotSmoke", "Ducky.AotSmoke.dll", "Ducky.dll"]).Any(v => v.Contains("PublishAot is not in effect", StringComparison.Ordinal)))
        {
            yield return "AotSmokeSelfCheck: a publish with managed assemblies must report 'PublishAot is not in effect'";
        }
    }

    // A NativeAOT publish holds the native binary and no managed assembly: a *.dll means PublishAot is not in effect, and
    // the JIT app with an apphost that dotnet publish quietly produces instead would still print PASS (INV-24).
    private static IEnumerable<string> AotPublishViolations(IEnumerable<string> publishedFileNames) =>
        publishedFileNames
            .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(f => $"{AotSmokeProject}: the publish produced managed assembly {f}, so PublishAot is not in effect (INV-24)");
}

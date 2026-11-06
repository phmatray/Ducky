using System.Text.RegularExpressions;
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

    // The Ducky.AotSmoke publish's IL warning allowance (AotIlWarningViolations): any IL warning line (ILC's or Roslyn's)
    // fails except the framework's IL3050 in these two members.
    private const string AllowedIl3050Type = "Microsoft.AspNetCore.Components.PersistentComponentState";
    private static readonly string[] AllowedIl3050Members = ["PersistAsJson", "TryTakeFromJson"];
    private static readonly Regex IlWarning = new(@"\b(?:warning|error) IL\d+:");
    private static readonly Regex AllowedIl3050 = new($@"\bwarning IL3050: {Regex.Escape(AllowedIl3050Type)}\.({string.Join('|', AllowedIl3050Members)})<TValue>\(");

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
            // Cleaned first, so files left by an earlier publish can't hide what this one produced. The RID intermediates go
            // too: with them, csc and ILC are up to date on a re-run and print no warning, so the IL check would read nothing.
            AotPublish.CreateOrCleanDirectory();
            (RootDirectory / "test" / AotSmokeProject / "obj" / "Release" / "net10.0" / "linux-x64").DeleteDirectory();
            var publishOutput = DotNet($"publish {RootDirectory / "test" / AotSmokeProject / $"{AotSmokeProject}.csproj"} -c Release -r linux-x64 --no-restore -o {AotPublish}")
                .Select(o => o.Text);
            var publishViolations = AotPublishViolations(AotPublish.GetFiles().Select(f => f.Name))
                .Concat(AotIlWarningViolations(publishOutput))
                .ToList();
            publishViolations.ForEach(v => Log.Error(v));
            Assert.True(publishViolations.Count == 0, $"AotSmoke: {publishViolations.Count} publish violation(s)");

            // A smoke assertion that deadlocks under NativeAOT fails here with a diagnostic, not at the runner's job limit.
            var timeout = TimeSpan.FromMinutes(5);
            var manifest = ParseManifest(Manifest.ReadAllText());
            var smoke = ProcessTasks.StartProcess(AotPublish / AotSmokeProject, string.Join(' ', AotSmokeInactiveNames(manifest)),
                workingDirectory: AotPublish, timeout: (int)timeout.TotalMilliseconds);
            Assert.True(smoke.WaitForExit(), $"AotSmoke: {AotSmokeProject} timed out after {timeout.TotalMinutes} minutes and was killed");

            var violations = AotSmokeViolations(smoke.Output.Select(o => o.Text).ToList(), smoke.ExitCode, manifest).ToList();
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
        // Only the Ducky.AotSmoke entries above activeStage are passed to the binary: not one at activeStage, nor another
        // project's.
        var staged = new TestManifest
        {
            ActiveStage = 12,
            Tests =
            [
                new() { Name = "AotSmoke", Projects = [AotSmokeProject], Stage = 12 },
                new() { Name = "DevToolsPayload_NonEmpty", Projects = [AotSmokeProject], Stage = 15 },
                new() { Name = "Draft_Smoke", Projects = [AotSmokeProject], Stage = 13 },
                new() { Name = "Later_Test", Projects = ["Ducky.Tests"], Stage = 15 },
            ],
        };
        var inactive = string.Join(' ', AotSmokeInactiveNames(staged));
        if (inactive != "DevToolsPayload_NonEmpty Draft_Smoke")
        {
            yield return $"AotSmokeSelfCheck: the inactive Ducky.AotSmoke names must be 'DevToolsPayload_NonEmpty Draft_Smoke', got '{inactive}'";
        }
        foreach (var violation in AotPublishViolations(["Ducky.AotSmoke", "Ducky.AotSmoke.dbg", "Ducky.pdb", "Ducky.xml"]))
        {
            yield return $"AotSmokeSelfCheck: a NativeAOT publish must pass, got: {violation}";
        }
        if (!AotPublishViolations(["Ducky.AotSmoke", "Ducky.AotSmoke.dll", "Ducky.dll"]).Any(v => v.Contains("PublishAot is not in effect", StringComparison.Ordinal)))
        {
            yield return "AotSmokeSelfCheck: a publish with managed assemblies must report 'PublishAot is not in effect'";
        }
        // ILC's line format (linux-x64, 10.0.12), and Roslyn's for an analyzer warning in Program.cs.
        static string Ilc(string code, string member) =>
            $"ILC : AOT analysis warning {code}: {member}: Using member 'System.Text.Json.JsonSerializer.Deserialize<TValue>(Utf8JsonReader&,JsonSerializerOptions)' which has 'RequiresDynamicCodeAttribute' can break functionality when AOT compiling. [/repo/test/Ducky.AotSmoke/Ducky.AotSmoke.csproj]";
        var take = Ilc("IL3050", "Microsoft.AspNetCore.Components.PersistentComponentState.TryTakeFromJson<TValue>(String,!!0&)");
        var persist = Ilc("IL3050", "Microsoft.AspNetCore.Components.PersistentComponentState.PersistAsJson<TValue>(String,!!0)");
        // MSBuild repeats each warning in its summary: a line seen twice is still allowed.
        foreach (var violation in AotIlWarningViolations(["Generating native code", take, persist, take, persist]))
        {
            yield return $"AotSmokeSelfCheck: the framework's two allowed IL3050 must pass, got: {violation}";
        }
        (string Case, List<string> Output, string Expected)[] plantedIl =
        [
            ("an IL3050 from Ducky.Blazor", [take, persist, Ilc("IL3050", "Ducky.Blazor.PersistenceWriter.Write<TValue>(String,!!0)")],
                "outside the allowance: ILC : AOT analysis warning IL3050: Ducky.Blazor.PersistenceWriter"),
            ("an IL3050 from another Components member", [take, persist, Ilc("IL3050", "Microsoft.AspNetCore.Components.RenderTree.Renderer.Dispatch<TValue>(String)")],
                "outside the allowance: ILC : AOT analysis warning IL3050: Microsoft.AspNetCore.Components.RenderTree"),
            ("another IL code on an allowed member", [take, persist, Ilc("IL2026", "Microsoft.AspNetCore.Components.PersistentComponentState.PersistAsJson<TValue>(String,!!0)")],
                "outside the allowance: ILC : AOT analysis warning IL2026"),
            ("a collapsed IL3053", [take, persist, "/nuget/Microsoft.Extensions.Caching.Memory.dll : warning IL3053: Assembly 'Microsoft.Extensions.Caching.Memory' produced AOT analysis warnings."],
                "outside the allowance: /nuget/Microsoft.Extensions.Caching.Memory.dll : warning IL3053"),
            ("a Roslyn analyzer warning", [take, persist, "/repo/test/Ducky.AotSmoke/Program.cs(12,5): warning IL2026: Using member 'X' [/repo/test/Ducky.AotSmoke/Ducky.AotSmoke.csproj]"],
                "outside the allowance: /repo/test/Ducky.AotSmoke/Program.cs(12,5): warning IL2026"),
            ("a publish without the TryTakeFromJson IL3050", ["Generating native code", persist],
                "the publish reported no IL3050 for Microsoft.AspNetCore.Components.PersistentComponentState.TryTakeFromJson"),
            ("a publish without the PersistAsJson IL3050", ["Generating native code", take],
                "the publish reported no IL3050 for Microsoft.AspNetCore.Components.PersistentComponentState.PersistAsJson"),
        ];
        foreach (var (plantedCase, output, expected) in plantedIl)
        {
            var violations = AotIlWarningViolations(output).ToList();
            if (!violations.Any(v => v.Contains(expected, StringComparison.Ordinal)))
            {
                yield return $"AotSmokeSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
            }
        }
    }

    // A NativeAOT publish holds the native binary and no managed assembly: a *.dll means PublishAot is not in effect, and
    // the JIT app with an apphost that dotnet publish quietly produces instead would still print PASS (INV-24).
    private static IEnumerable<string> AotPublishViolations(IEnumerable<string> publishedFileNames) =>
        publishedFileNames
            .Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(f => $"{AotSmokeProject}: the publish produced managed assembly {f}, so PublishAot is not in effect (INV-24)");

    // ILC would collapse Microsoft.AspNetCore.Components' AOT warnings into one IL3053; the Ducky.AotSmoke csproj keeps that
    // assembly uncollapsed and demotes IL3050, so the publish shows the framework's own IL3050 member by member: the two in
    // PersistentComponentState.PersistAsJson/TryTakeFromJson (reached through §10's audited IL2026 suppression of the
    // prerender seed, whose public methods carry no [RequiresDynamicCode]). Any other IL warning (from Ducky, another
    // Components member, a collapsed IL3053, a Roslyn analyzer) fails, and both allowed lines must be present: the positive
    // control that the warnings were visible and the parse reads them, as for §10's IL2104 allowlist.
    // SPEC §10 (Smoke gates) and §19 state this allowance.
    private static IEnumerable<string> AotIlWarningViolations(IEnumerable<string> publishOutput)
    {
        var lines = publishOutput.Where(line => IlWarning.IsMatch(line)).ToList();
        foreach (var line in lines.Where(line => !AllowedIl3050.IsMatch(line)).Distinct(StringComparer.Ordinal))
        {
            yield return $"{AotSmokeProject}: the publish reported an IL warning outside the allowance: {line} (INV-24)";
        }
        var seen = lines.Select(line => AllowedIl3050.Match(line)).Where(m => m.Success).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        foreach (var member in AllowedIl3050Members.Where(member => !seen.Contains(member)))
        {
            yield return $"{AotSmokeProject}: the publish reported no IL3050 for {AllowedIl3050Type}.{member}: the output parse missed it, or the framework fixed it (update the allowance)";
        }
    }

    // The binary's arguments (P1): an entry of a later stage may land before activeStage reaches it. Its assertion still
    // runs and a FAIL still fails, but the binary prints no PASS for a name passed here, so the PASS set stays the active
    // entries (§19) until the bump; a name absent from the manifest is never passed, so it still prints and fails (P8).
    // SPEC §10 (Smoke gates) and §19 state this exception.
    private static IEnumerable<string> AotSmokeInactiveNames(TestManifest manifest) =>
        manifest.Tests
            .Where(t => t.Stage > manifest.ActiveStage && t.Projects.Contains(AotSmokeProject))
            .Select(t => t.Name)
            .Order(StringComparer.Ordinal);
}

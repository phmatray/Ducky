using System.Text.Json;
using Ducky.E2E;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §17.1 (JS), §19 (E2E, E2EAllBrowsers): Playwright specs of test/Ducky.E2E, then, in Chromium, the 100% block
// coverage gate of ducky.js over the CDP precise coverage the harness wrote for every page (JsBlockCoverage, linked from
// test/Ducky.E2E). Staged (§24): samples are published only once they exist in Ducky.slnx (stage 18); until then the
// harness specs alone measure ducky.js.
internal sealed partial class Build
{
    private AbsolutePath E2EProject => RootDirectory / "test" / "Ducky.E2E" / "Ducky.E2E.csproj";
    private AbsolutePath E2EArtifacts => Artifacts / "e2e";
    private AbsolutePath DuckyJs => RootDirectory / "src" / "Ducky.Blazor" / "wwwroot" / "ducky.js";

    private Target E2E => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var selfCheck = JsCoverageSelfCheck().ToList();
            selfCheck.ForEach(v => Log.Error(v));
            Assert.True(selfCheck.Count == 0, $"E2E: {selfCheck.Count} self-check violation(s)");

            // Cleaned first: a file left by an earlier run would feed the gate old counts.
            var coverage = E2EArtifacts / "js-coverage";
            coverage.CreateOrCleanDirectory();
            RunE2E(PublishSamples(), "chromium", coverage);

            var violations = JsCoverageViolations(coverage.GlobFiles("*.json").Select(f => f.ReadAllText()), DuckyJs.ReadAllText()).ToList();
            violations.ForEach(v => Log.Error(v));
            Assert.True(violations.Count == 0, $"E2E: {violations.Count} ducky.js block(s) below 100% block coverage");
        });

    // Functional only: block coverage is a Chromium (CDP) measurement.
    private Target E2EAllBrowsers => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var samples = PublishSamples();
            RunE2E(samples, "firefox");
            RunE2E(samples, "webkit");
        });

    // Each sample of Ducky.slnx, published for PublishedHost (test/Ducky.E2E) to start from $DUCKY_SAMPLES_DIR/<name>/.
    private AbsolutePath PublishSamples()
    {
        var samples = E2EArtifacts / "samples";
        samples.CreateOrCleanDirectory();
        foreach (var sample in Solution.AllProjects.Where(p => (RootDirectory / "samples").Contains(p.Path)))
        {
            DotNet($"publish {sample.Path} -c Release --no-restore -o {samples / sample.Name}");
        }
        return samples;
    }

    // MTP syntax (§19); the harness reads the browser from DUCKY_BROWSER and writes coverage only when given a directory.
    private void RunE2E(AbsolutePath samples, string browser, AbsolutePath? coverage = null)
    {
        ProcessTasks.StartProcess("pwsh", $"{E2EProject.Parent / "bin" / "Release" / "net10.0" / "playwright.ps1"} install --with-deps {browser}")
            .AssertZeroExitCode();
        var environment = new Dictionary<string, string>(EnvironmentInfo.Variables)
        {
            ["DUCKY_BROWSER"] = browser,
            ["DUCKY_SAMPLES_DIR"] = samples,
        };
        environment.Remove("DUCKY_JS_COVERAGE_DIR");
        if (coverage is not null)
        {
            environment["DUCKY_JS_COVERAGE_DIR"] = coverage;
        }
        DotNet($"test --project {E2EProject} -c Release --no-build --report-trx --results-directory {E2EArtifacts / browser}",
            environmentVariables: environment);
    }

    // Every file is a JSON array of V8 ScriptCoverage objects of ducky.js. No collection at all fails, so the gate can
    // never pass vacuously. A copy whose top-level range (functionName "", from offset 0) does not span the source is of
    // another text, whose ranges would lend counts to the real module's blocks, so it fails. Otherwise every block whose
    // merged count is zero is reported at its line.
    private static IEnumerable<string> JsCoverageViolations(IEnumerable<string> files, string source)
    {
        var scripts = files.SelectMany(f => JsonSerializer.Deserialize<List<JsonElement>>(f)!).ToList();
        if (scripts.Count == 0)
        {
            yield return "no ducky.js coverage was collected: no Chromium spec loaded the module (SPEC §17.1)";
            yield break;
        }
        foreach (var script in scripts)
        {
            foreach (var function in script.GetProperty("functions").EnumerateArray().Where(f => f.GetProperty("functionName").GetString() == ""))
            {
                var top = function.GetProperty("ranges")[0];
                var length = top.GetProperty("endOffset").GetInt32();
                if (top.GetProperty("startOffset").GetInt32() == 0 && length != source.Length)
                {
                    yield return $"{script.GetProperty("url").GetString()} is {length} characters long, src/Ducky.Blazor/wwwroot/ducky.js {source.Length}: a script served as ducky.js must be its source";
                }
            }
        }
        foreach (var (start, end) in JsBlockCoverage.ZeroBlocks(scripts))
        {
            var before = source[..Math.Min(start, source.Length)];
            var text = string.Join(' ', source[Math.Min(start, source.Length)..Math.Min(end, source.Length)]
                .Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));
            yield return $"ducky.js line {before.Count(c => c == '\n') + 1}: block [{start}, {end}) never ran in any page: {(text.Length > 80 ? text[..80] + "..." : text)}";
        }
    }

    // The build side of the gate on miniature collections; the planted check on real V8 output is the Ducky.E2E spec
    // JsBlockCoverage_PlantedUnexecutedBlock_FailsGate, which runs inside the same E2E run.
    private static IEnumerable<string> JsCoverageSelfCheck()
    {
        const string Source = "export function f(x) {\n  if (x) { return 1; }\n  return 0;\n}\n";
        static string Copy(params (int Start, int End, int Count)[] ranges) =>
            $$"""[{"url":"http://h/ducky.js","functions":[{"functionName":"f","ranges":[{{string.Join(",", ranges.Select(r => $$"""{"startOffset":{{r.Start}},"endOffset":{{r.End}},"count":{{r.Count}}}"""))}}]}]}]""";
        static string TopLevel(int end) =>
            $$"""[{"url":"http://h/ducky.js","functions":[{"functionName":"","ranges":[{"startOffset":0,"endOffset":{{end}},"count":1}]}]}]""";
        if (!JsCoverageViolations([], Source).Any(v => v.Contains("no ducky.js coverage was collected", StringComparison.Ordinal)))
        {
            yield return "JsCoverageSelfCheck: a run without ducky.js coverage must fail";
        }
        if (!JsCoverageViolations(["[]"], Source).Any(v => v.Contains("no ducky.js coverage was collected", StringComparison.Ordinal)))
        {
            yield return "JsCoverageSelfCheck: coverage files without a ducky.js script must fail";
        }
        var unexecuted = Copy((0, 50, 1), (30, 43, 0));
        if (!JsCoverageViolations([unexecuted], Source).Any(v => v.StartsWith("ducky.js line 2: block [30, 43)", StringComparison.Ordinal)))
        {
            yield return $"JsCoverageSelfCheck: an unexecuted block must be reported at line 2, got [{string.Join("; ", JsCoverageViolations([unexecuted], Source))}]";
        }
        // The block ran in a second page only, whose copy omits the range because it equals its parent's count.
        foreach (var violation in JsCoverageViolations([unexecuted, Copy((0, 50, 2))], Source))
        {
            yield return $"JsCoverageSelfCheck: a block run in another page must pass, got: {violation}";
        }
        // A copy of another text (a stale or rewritten module served as ducky.js) must fail: its top-level range encloses
        // every offset of the real module and would lend its count to blocks that never ran.
        if (!JsCoverageViolations([TopLevel(Source.Length), TopLevel(Source.Length - 1)], Source)
            .Any(v => v.Contains($"is {Source.Length - 1} characters long", StringComparison.Ordinal)))
        {
            yield return "JsCoverageSelfCheck: a ducky.js copy whose length differs from the source must fail";
        }
        foreach (var violation in JsCoverageViolations([TopLevel(Source.Length)], Source))
        {
            yield return $"JsCoverageSelfCheck: a copy of the source itself must pass, got: {violation}";
        }
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §17.1 and §19 (M16-01): Audit, the only place NuGetAudit is enforced (NU1901-NU1904 stay warnings everywhere
// else, Directory.Build.props), and Benchmarks, BenchmarkDotNet against the committed benchmarks/baseline (report only).
// Both run in nightly-audit (Build.CI.cs).
internal sealed partial class Build
{
    // %3B: an unescaped ';' would end the -p value. Global properties, so a project can't opt out with NuGetAudit=false
    // nor drop the low and moderate advisories with a higher NuGetAuditLevel.
    private const string AuditProperties = "-p:NuGetAudit=true -p:NuGetAuditMode=all -p:NuGetAuditLevel=low -p:WarningsAsErrors=NU1901%3BNU1902%3BNU1903%3BNU1904";

    private static readonly Regex AuditCode = new("NU190[1-4]");

    // The array of a BenchmarkDotNet JSON report (a const: CA1507 takes the literal for the Benchmarks target's name).
    private const string BenchmarksReportKey = "Benchmarks";

    private AbsolutePath BenchmarksProject => RootDirectory / "benchmarks" / "Ducky.Benchmarks" / "Ducky.Benchmarks.csproj";
    private AbsolutePath BenchmarksBaseline => RootDirectory / "benchmarks" / "baseline";

    // Over Ducky.slnx only, never Ducky.Migration.slnx: its "before" samples reference legacy packages on purpose.
    private Target Audit => _ => _
        .Executes(() =>
        {
            FailOnViolations("Audit", [.. Solution.AllProjects.SelectMany(p => AuditSuppressionViolations(p.Path)), .. AuditSelfCheck()]);
            DotNet($"restore {Solution} {AuditProperties:nq}");
        });

    // Report only: the numbers never fail the run, a harness that measured nothing does. The ratios only mean something
    // when the baseline was measured on the same kind of host, so both hosts are logged. Refresh the baseline from a
    // nightly-audit (ubuntu-latest) run: the generated workflow uploads no artifact (§20.1), so take the numbers from its
    // log. A local run writes artifacts/benchmarks/results/*-report-brief.json, the baseline's format.
    private Target Benchmarks => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            FailOnViolations("Benchmarks", [.. BenchmarkSelfCheck()]);
            var results = Artifacts / "benchmarks";
            results.CreateOrCleanDirectory();
            DotNet($"run --project {BenchmarksProject} -c Release --no-build -- --filter * --exporters briefjson --artifacts {results}");
            var current = BenchmarkResults(results / "results");
            Assert.True(current.Count > 0, $"Benchmarks: no result in {Relative(results / "results")}");
            Log.Information("Benchmarks: measured on {Current}; baseline measured on {Baseline}", BenchmarkHosts(results / "results"), BenchmarkHosts(BenchmarksBaseline));
            BenchmarkReport(BenchmarkResults(BenchmarksBaseline), current).ForEach(line => Log.Information("Benchmarks: {Line}", line));
        });

    // A suppression other than a <NuGetAuditSuppress> item: an audit code in the project's NoWarn, or in the NoWarn metadata
    // of a PackageReference or ProjectReference. Evaluated values, so imports, $(...) indirection and multi-line values count.
    private List<string> AuditSuppressionViolations(AbsolutePath project)
    {
        var evaluated = JsonDocument.Parse(string.Join('\n',
            DotNet($"msbuild {project} -getProperty:NoWarn -getItem:PackageReference -getItem:ProjectReference {AuditProperties:nq}", logOutput: false, logInvocation: false)
                .Where(o => o.Type == OutputType.Std).Select(o => o.Text))).RootElement;
        var noWarns = evaluated.GetProperty("Items").EnumerateObject()
            .SelectMany(items => items.Value.EnumerateArray().Select(item =>
                (Source: $"{items.Name} {item.GetProperty("Identity").GetString()}", NoWarn: item.TryGetProperty("NoWarn", out var n) ? n.GetString()! : "")))
            .Prepend((Source: "NoWarn", NoWarn: evaluated.GetProperty("Properties").GetProperty("NoWarn").GetString()!));
        return [.. noWarns.Where(w => AuditCode.IsMatch(w.NoWarn)).Select(w => AuditSuppression(Relative(project), w.Source))];
    }

    private static string AuditSuppression(string project, string source) =>
        $"{project}: {source} suppresses an audit warning with NoWarn; use a <NuGetAuditSuppress> item (SPEC §17.1)";

    // The acceptance checks of M16-01: the audited restore of a project under the real Directory.* files fails on a
    // planted vulnerable package, direct or transitive, even under a project's NuGetAudit=false, NuGetAuditMode=direct or
    // NuGetAuditLevel=critical (one row per global property of AuditProperties: SDK 10 defaults mode to all), and passes
    // without one; the NoWarn check reports every suppression shape and no other NoWarn. Newtonsoft.Json 12.0.1
    // has a high advisory (GHSA-5crp-9r3c-p9vr, NU1903); Newtonsoft.Json.Bson 1.0.2 depends on it.
    private IEnumerable<string> AuditSelfCheck()
    {
        var fixture = Planted / "Audit";
        var project = fixture / "Planted.csproj";
        void Plant(string properties, string items, string imported = "")
        {
            fixture.CreateOrCleanDirectory();
            (fixture / "Imported.props").WriteAllText($"<Project><PropertyGroup>{imported}</PropertyGroup></Project>");
            project.WriteAllText($"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <Import Project="Imported.props" />
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>{properties}
                  </PropertyGroup>
                  <ItemGroup>{items}</ItemGroup>
                </Project>
                """);
        }
        (int ExitCode, List<string> Output) Restore()
        {
            var exitCode = 0;
            var output = DotNet($"restore {project} {AuditProperties:nq} -p:RestoreLockedMode=false",
                    logOutput: false, exitHandler: p => exitCode = p.ExitCode)
                .Select(o => o.Text)
                .ToList();
            return (exitCode, output);
        }

        Plant("<NoWarn>$(NoWarn);NU1608</NoWarn>", "<NuGetAuditSuppress Include=\"https://github.com/advisories/GHSA-5crp-9r3c-p9vr\" />");
        var (cleanExit, cleanOutput) = Restore();
        if (cleanExit != 0)
        {
            yield return $"AuditSelfCheck: the unplanted fixture must restore, exit code {cleanExit}: [{string.Join("; ", cleanOutput.Where(l => l.Contains(": error ", StringComparison.Ordinal)).Distinct())}]";
        }
        if (AuditSuppressionViolations(project) is [_, ..] cleanViolations)
        {
            yield return $"AuditSelfCheck: a NoWarn without an audit code must not be reported, got [{string.Join("; ", cleanViolations)}]";
        }

        (string Case, string Package, string Version, string Properties)[] planted =
        [
            ("a direct vulnerable package", "Newtonsoft.Json", "12.0.1", ""),
            ("a transitive vulnerable package", "Newtonsoft.Json.Bson", "1.0.2", ""),
            ("a direct vulnerable package under NuGetAuditLevel=critical", "Newtonsoft.Json", "12.0.1", "<NuGetAuditLevel>critical</NuGetAuditLevel>"),
            ("a direct vulnerable package under NuGetAudit=false", "Newtonsoft.Json", "12.0.1", "<NuGetAudit>false</NuGetAudit>"),
            ("a transitive vulnerable package under NuGetAuditMode=direct", "Newtonsoft.Json.Bson", "1.0.2", "<NuGetAuditMode>direct</NuGetAuditMode>"),
        ];
        foreach (var (plantedCase, package, version, properties) in planted)
        {
            Plant(properties, $"<PackageVersion Include=\"{package}\" Version=\"{version}\" /><PackageReference Include=\"{package}\" />");
            var (exitCode, output) = Restore();
            if (exitCode == 0 || !output.Any(line => line.Contains("error NU1903", StringComparison.Ordinal)))
            {
                yield return $"AuditSelfCheck: {plantedCase} must fail the restore with NU1903, exit code {exitCode}";
            }
        }

        (string Case, string Items, string Imported, string Source)[] suppressed =
        [
            ("a NoWarn property", "", "<NoWarn>$(NoWarn);NU1901</NoWarn>", "NoWarn"),
            ("a multi-line NoWarn through a property", "", "<AuditCodes>NU1902</AuditCodes><NoWarn>\n      $(NoWarn);\n      $(AuditCodes)\n    </NoWarn>", "NoWarn"),
            ("a PackageReference NoWarn", "<PackageReference Include=\"Newtonsoft.Json\" NoWarn=\"NU1903\" />", "", "PackageReference Newtonsoft.Json"),
            ("a ProjectReference NoWarn", "<ProjectReference Include=\"Other.csproj\" NoWarn=\"NU1904\" />", "", "ProjectReference Other.csproj"),
        ];
        foreach (var (suppressedCase, items, imported, source) in suppressed)
        {
            Plant("", items, imported);
            var expected = AuditSuppression(Relative(project), source);
            if (AuditSuppressionViolations(project) is not [var only] || only != expected)
            {
                yield return $"AuditSelfCheck: {suppressedCase} must be reported as exactly [{expected}]";
            }
        }
    }

    // FullName -> (mean ns, allocated bytes per operation) of every BenchmarkDotNet JSON report in the directory.
    private static Dictionary<string, (double Mean, long? Allocated)> BenchmarkResults(AbsolutePath directory) =>
        directory.GlobFiles("*.json")
            .SelectMany(f => JsonDocument.Parse(f.ReadAllText()).RootElement.GetProperty(BenchmarksReportKey).EnumerateArray())
            .Where(b => b.TryGetProperty("Statistics", out var s) && s.ValueKind == JsonValueKind.Object)
            .ToDictionary(
                b => b.GetProperty("FullName").GetString()!,
                b => (b.GetProperty("Statistics").GetProperty("Mean").GetDouble(),
                    b.TryGetProperty("Memory", out var m) && m.ValueKind == JsonValueKind.Object ? m.GetProperty("BytesAllocatedPerOperation").GetInt64() : (long?)null));

    // "ProcessorName, OsVersion, Architecture" of the reports in the directory.
    private static string BenchmarkHosts(AbsolutePath directory) =>
        string.Join(" | ", directory.GlobFiles("*.json").Select(f => BenchmarkHost(JsonDocument.Parse(f.ReadAllText()).RootElement)).Distinct().Order(StringComparer.Ordinal));

    private static string BenchmarkHost(JsonElement report)
    {
        var host = report.GetProperty("HostEnvironmentInfo");
        return $"{host.GetProperty("ProcessorName").GetString()}, {host.GetProperty("OsVersion").GetString()}, {host.GetProperty("Architecture").GetString()}";
    }

    private static List<string> BenchmarkReport(Dictionary<string, (double Mean, long? Allocated)> baseline, Dictionary<string, (double Mean, long? Allocated)> current) =>
    [
        .. current.OrderBy(c => c.Key, StringComparer.Ordinal).Select(c => baseline.TryGetValue(c.Key, out var b)
            ? FormattableString.Invariant($"{c.Key}: {c.Value.Mean:F1} ns ({c.Value.Mean / b.Mean:F2}x baseline {b.Mean:F1} ns), {c.Value.Allocated} B allocated (baseline {b.Allocated} B)")
            : FormattableString.Invariant($"{c.Key}: {c.Value.Mean:F1} ns, {c.Value.Allocated} B allocated (no baseline)")),
        .. baseline.Keys.Except(current.Keys).Order(StringComparer.Ordinal).Select(k => $"{k}: in the baseline, not measured"),
    ];

    private static IEnumerable<string> BenchmarkSelfCheck()
    {
        var report = BenchmarkReport(new() { ["A"] = (100, 32), ["Gone"] = (5, null) }, new() { ["A"] = (150, 64), ["New"] = (7, 0) });
        string[] expected =
        [
            "A: 150.0 ns (1.50x baseline 100.0 ns), 64 B allocated (baseline 32 B)",
            "New: 7.0 ns, 0 B allocated (no baseline)",
            "Gone: in the baseline, not measured",
        ];
        if (!report.SequenceEqual(expected, StringComparer.Ordinal))
        {
            yield return $"BenchmarkSelfCheck: expected [{string.Join("; ", expected)}], got [{string.Join("; ", report)}]";
        }
        var host = BenchmarkHost(JsonDocument.Parse("""{"HostEnvironmentInfo":{"OsVersion":"Ubuntu 24.04","ProcessorName":"AMD EPYC","LogicalCoreCount":2,"Architecture":"X64"}}""").RootElement);
        if (host != "AMD EPYC, Ubuntu 24.04, X64")
        {
            yield return $"BenchmarkSelfCheck: expected the host [AMD EPYC, Ubuntu 24.04, X64], got [{host}]";
        }
    }
}

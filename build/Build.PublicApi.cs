using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §17.10 and §19: PublicAPI tracking (RS0016/RS0017 are errors in Compile), release tracking of the generator
// descriptors (AnalyzerReleases.*.md), ShipPublicApi and SetBaseline (§21: run after the GA push, never by the release PR).
internal sealed partial class Build
{
    [Parameter("NuGet V3 flat-container base URL SetBaseline reads the latest Ducky GA from")]
    private readonly string NuGetFlatContainer = "https://api.nuget.org/v3-flatcontainer/";

    private const string PublicApiHeader = "#nullable enable";
    private const string RemovedPrefix = "*REMOVED*";
    private static readonly string[] PublicApiFiles = ["PublicAPI.Shipped.txt", "PublicAPI.Unshipped.txt"];
    private static readonly string[] PublicApiCodes = ["RS0016", "RS0017"];
    private static readonly Regex BaselineElement = new("<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>");
    private static readonly Regex BaselineLine = new(@"^[ \t]*<PackageValidationBaselineVersion>[^<]*</PackageValidationBaselineVersion>\r?\n", RegexOptions.Multiline);
    private static readonly Regex EnablePackageValidationLine = new(@"^([ \t]*)<EnablePackageValidation>true</EnablePackageValidation>\n", RegexOptions.Multiline);

    private AbsolutePath Planted => Artifacts / "planted";
    private AbsolutePath BuildProps => RootDirectory / "Directory.Build.props";

    private Target ShipPublicApi => _ => _
        .Executes(() =>
        {
            FailOnViolations("ShipPublicApi", ShipPublicApiSelfCheck().ToList());
            foreach (var file in ShipPublicApiIn(RootDirectory, ReleaseVersion ?? MinVerVersion()))
            {
                Log.Information("ShipPublicApi: updated {File}", Relative(file));
            }
        });

    private Target SetBaseline => _ => _
        .Executes(async () =>
        {
            FailOnViolations("SetBaseline", SetBaselineSelfCheck().ToList());
            var version = await SetBaselineIn(NuGetFlatContainer, BuildProps);
            Log.Information("SetBaseline: PackageValidationBaselineVersion={Version} in {File}", version, Relative(BuildProps));
        });

    // Writes PackageValidationBaselineVersion={latest 2.x GA in the flat container} into props; returns that version.
    private static async Task<string> SetBaselineIn(string flatContainer, AbsolutePath props)
    {
        using var http = new HttpClient();
        var feed = new Uri(new Uri(flatContainer.TrimEnd('/') + "/"), "ducky/index.json");
        var version = LatestGa(await http.GetStringAsync(feed));
        Assert.True(version is not null, $"{feed} lists no 2.x GA of Ducky: SetBaseline runs only after the GA push (§21)");
        props.WriteAllText(WithBaseline(props.ReadAllText(), version!));
        return version!;
    }

    private static void FailOnViolations(string target, List<string> violations)
    {
        violations.ForEach(v => Log.Error(v));
        Assert.True(violations.Count == 0, $"{target}: {violations.Count} violation(s)");
    }

    // Moves every src/*/PublicAPI.Unshipped.txt into its Shipped twin (*REMOVED* lines delete their Shipped line) and every
    // non-empty src/*/AnalyzerReleases.Unshipped.md into a "## Release {version}" section of its Shipped twin; a pending
    // analyzer release with a prerelease version fails before anything is written. Nothing else is touched: the package
    // validation baseline belongs to SetBaseline. Returns the files it rewrote.
    private static List<AbsolutePath> ShipPublicApiIn(AbsolutePath root, string version)
    {
        var changed = new List<AbsolutePath>();
        // Analyzer releases first: the GA assert fires on the first pending release, before any file is written.
        foreach (var unshipped in root.GlobFiles("src/*/AnalyzerReleases.Unshipped.md"))
        {
            var lines = unshipped.ReadAllText().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
            var header = lines.TakeWhile(l => l.StartsWith(';')).ToList();
            var body = string.Join('\n', lines.Skip(header.Count)).Trim('\n');
            if (body.Length == 0)
            {
                continue;
            }
            // The release-tracking analyzer reads "## Release {version}" as a GA version (RS2007 otherwise).
            Assert.True(Version.TryParse(version, out _), $"ShipPublicApi: '{version}' is not a GA version; pass --release-version");
            var shipped = unshipped.Parent / "AnalyzerReleases.Shipped.md";
            shipped.WriteAllText(shipped.ReadAllText().TrimEnd('\n') + $"\n\n## Release {version}\n\n{body}\n");
            unshipped.WriteAllText(string.Join('\n', header) + "\n");
            changed.AddRange([shipped, unshipped]);
        }
        foreach (var unshipped in root.GlobFiles("src/*/PublicAPI.Unshipped.txt"))
        {
            var pending = ApiLines(unshipped.ReadAllText());
            if (pending.Count == 0)
            {
                continue;
            }
            var shipped = unshipped.Parent / "PublicAPI.Shipped.txt";
            var removed = pending.Where(l => l.StartsWith(RemovedPrefix, StringComparison.Ordinal)).Select(l => l[RemovedPrefix.Length..]);
            var lines = ApiLines(shipped.ReadAllText())
                .Except(removed)
                .Union(pending.Where(l => !l.StartsWith(RemovedPrefix, StringComparison.Ordinal)))
                .Order(StringComparer.Ordinal);
            shipped.WriteAllText(string.Join('\n', lines.Prepend(PublicApiHeader)) + "\n");
            unshipped.WriteAllText(PublicApiHeader + "\n");
            changed.AddRange([shipped, unshipped]);
        }
        return changed;
    }

    // The highest GA (no prerelease label) of a flat-container index.json, or null when there is no 2.x GA yet.
    private static string? LatestGa(string indexJson) =>
        JsonDocument.Parse(indexJson).RootElement.GetProperty("versions").EnumerateArray()
            .Select(v => Version.TryParse(v.GetString(), out var version) ? version : null)
            .Where(v => v?.Major >= 2)
            .Max()?.ToString();

    // Sets PackageValidationBaselineVersion next to EnablePackageValidation, or replaces the value already there.
    private static string WithBaseline(string props, string version)
    {
        var element = $"<PackageValidationBaselineVersion>{version}</PackageValidationBaselineVersion>";
        if (BaselineElement.IsMatch(props))
        {
            return BaselineElement.Replace(props, element);
        }
        Assert.True(EnablePackageValidationLine.IsMatch(props), "Directory.Build.props has no <EnablePackageValidation>true</EnablePackageValidation> line");
        return EnablePackageValidationLine.Replace(props, m => $"{m.Value}{m.Groups[1].Value}{element}\n", 1);
    }

    // The acceptance checks of M0-10, run by Compile.
    private IEnumerable<string> PublicApiSelfCheck() =>
        PublicApiAnalyzerSelfCheck().Concat(ShipPublicApiSelfCheck()).Concat(SetBaselineSelfCheck());

    // Planted check: a src/ project with a public type missing from PublicAPI.Unshipped.txt (RS0016) and a line naming no
    // symbol (RS0017) fails to build with both; the unplanted twin builds. The fixture sits under artifacts/, so it takes
    // the real Directory.Build.props/targets and .globalconfig; DuckyIsSrcProject=true gives it the src/ analyzers. Then
    // every real src project is checked to take that configuration.
    private IEnumerable<string> PublicApiAnalyzerSelfCheck()
    {
        var fixture = Planted / "PublicApi";
        fixture.CreateOrCleanDirectory();
        (fixture / "Planted.csproj").WriteAllText("""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        (fixture / "PublicAPI.Shipped.txt").WriteAllText(PublicApiHeader + "\n");
        (fixture / "Listed.cs").WriteAllText("""
            namespace Planted;

            /// <summary>A public type with its PublicAPI line.</summary>
            public sealed class Listed;
            """);
        const string listedApi = PublicApiHeader + "\nPlanted.Listed\nPlanted.Listed.Listed() -> void\n";

        // TreatWarningsAsErrors=false: the errors must come from the .globalconfig severity, not from the promotion.
        (int ExitCode, List<string> Output) BuildFixture()
        {
            var exitCode = 0;
            var output = DotNet($"build {fixture / "Planted.csproj"} -c Release -p:DuckyIsSrcProject=true -p:TreatWarningsAsErrors=false -p:RestoreLockedMode=false",
                    logOutput: false, exitHandler: p => exitCode = p.ExitCode)
                .Select(o => o.Text)
                .ToList();
            return (exitCode, output);
        }

        (fixture / "PublicAPI.Unshipped.txt").WriteAllText(listedApi);
        var (unplantedExit, unplantedOutput) = BuildFixture();
        if (unplantedExit != 0)
        {
            var errors = unplantedOutput.Where(line => line.Contains(": error ", StringComparison.Ordinal)).Distinct();
            yield return $"PublicApiSelfCheck: the unplanted fixture must build, exit code {unplantedExit}: [{string.Join("; ", errors)}]";
        }

        (fixture / "Unlisted.cs").WriteAllText("""
            namespace Planted;

            /// <summary>A public type without its PublicAPI line.</summary>
            public sealed class Unlisted;
            """);
        (fixture / "PublicAPI.Unshipped.txt").WriteAllText(listedApi + "Planted.Gone\n");
        var (plantedExit, plantedOutput) = BuildFixture();
        foreach (var code in PublicApiCodes)
        {
            if (plantedExit == 0 || !plantedOutput.Any(line => line.Contains($"error {code}", StringComparison.Ordinal)))
            {
                yield return $"PublicApiSelfCheck: the planted fixture must fail Compile with {code} (exit code {plantedExit})";
            }
        }

        // The fixture proves the shared configuration; each real src project must also take it: src/ detection (it loads
        // the analyzer), both PublicAPI files (the analyzer is silent without them), and no NoWarn or WarningsNotAsErrors
        // naming RS0016/RS0017 (both override the .globalconfig severity). The NoTargets tombstone compiles nothing.
        foreach (var project in RootDirectory.GlobFiles("src/*/*.csproj"))
        {
            var json = string.Join('\n', DotNet(
                    $"msbuild {project} -getProperty:UsingMicrosoftNoTargetsSdk -getProperty:DuckyIsSrcProject -getProperty:NoWarn -getProperty:WarningsNotAsErrors",
                    logOutput: false, logInvocation: false)
                .Select(o => o.Text));
            var properties = JsonDocument.Parse(json).RootElement.GetProperty("Properties");
            string Property(string name) => properties.GetProperty(name).GetString() ?? "";
            if (Property("UsingMicrosoftNoTargetsSdk").Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var name = project.NameWithoutExtension;
            if (!Property("DuckyIsSrcProject").Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"PublicApiSelfCheck: {name} is not detected as a src project (DuckyIsSrcProject), so it loads no PublicApiAnalyzers";
            }
            foreach (var file in PublicApiFiles.Where(f => !(project.Parent / f).FileExists()))
            {
                yield return $"PublicApiSelfCheck: {name} has no {file}";
            }
            foreach (var property in new[] { "NoWarn", "WarningsNotAsErrors" })
            {
                var codes = Property(property).Split(';', StringSplitOptions.TrimEntries);
                foreach (var code in PublicApiCodes.Where(codes.Contains))
                {
                    yield return $"PublicApiSelfCheck: {name} lists {code} in {property}";
                }
            }
        }
    }

    // ShipPublicApi on a scratch copy of the repository's tracking files and Directory.Build.props, plus a planted project
    // with a *REMOVED* line and an analyzer release row: lines move, Unshipped files keep only their header, the props
    // file is byte-identical.
    private IEnumerable<string> ShipPublicApiSelfCheck()
    {
        var scratch = Planted / "ShipPublicApi";
        scratch.CreateOrCleanDirectory();
        var tracked = RootDirectory.GlobFiles("src/*/PublicAPI.*.txt", "src/*/AnalyzerReleases.*.md");
        foreach (var file in tracked)
        {
            file.Copy(scratch / RootDirectory.GetRelativePathTo(file));
        }
        BuildProps.Copy(scratch / "Directory.Build.props");

        var planted = scratch / "src" / "Planted";
        (planted / "PublicAPI.Shipped.txt").WriteAllText(PublicApiHeader + "\nPlanted.Kept\nPlanted.Old\n");
        (planted / "PublicAPI.Unshipped.txt").WriteAllText(PublicApiHeader + "\nPlanted.New\n" + RemovedPrefix + "Planted.Old\n");
        const string releasesHelp = "; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md\n";
        (planted / "AnalyzerReleases.Shipped.md").WriteAllText("; Shipped analyzer releases\n" + releasesHelp);
        const string newRules = "### New Rules\n\nRule ID | Category | Severity | Notes\n--------|----------|----------|-------\nDUCKY001 | Usage | Error | Planted\n";
        (planted / "AnalyzerReleases.Unshipped.md").WriteAllText("; Unshipped analyzer release\n" + releasesHelp + "\n" + newRules);

        // §21 has the release PR run plain ShipPublicApi, where MinVer yields a prerelease: with a pending analyzer
        // release it must fail before writing anything, not leave the tree half-shipped.
        Dictionary<AbsolutePath, string> Snapshot() => scratch.GlobFiles("**/*").ToDictionary(f => f, f => f.ReadAllText());
        var before = Snapshot();
        var prereleaseThrew = false;
        try
        {
            ShipPublicApiIn(scratch, "2.0.0-alpha.0.5");
        }
        catch (Exception)
        {
            prereleaseThrew = true;
        }
        var after = Snapshot();
        if (!prereleaseThrew)
        {
            yield return "ShipPublicApiSelfCheck: a prerelease version with a pending analyzer release must fail";
        }
        foreach (var file in before.Keys.Where(f => !after.TryGetValue(f, out var text) || text != before[f]))
        {
            yield return $"ShipPublicApiSelfCheck: a failing prerelease ShipPublicApi rewrote {scratch.GetRelativePathTo(file)}";
        }

        var expected = RootDirectory.GlobFiles("src/*/PublicAPI.Unshipped.txt")
            .Select(unshipped => (Unshipped: unshipped, Lines: ApiLines(unshipped.ReadAllText()).Where(l => !l.StartsWith(RemovedPrefix, StringComparison.Ordinal)).ToList()))
            .ToList();
        List<AbsolutePath> changed;
        string? failure = null;
        try
        {
            changed = ShipPublicApiIn(scratch, "2.0.0");
        }
        catch (Exception e)
        {
            changed = [];
            failure = $"ShipPublicApiSelfCheck: ShipPublicApi threw {e.GetType().Name}: {e.Message}";
        }
        if (failure is not null)
        {
            yield return failure;
            yield break;
        }

        if ((scratch / "Directory.Build.props").ReadAllText() != BuildProps.ReadAllText())
        {
            yield return "ShipPublicApiSelfCheck: ShipPublicApi must leave Directory.Build.props untouched";
        }
        if (changed.Any(f => f.Name == "Directory.Build.props"))
        {
            yield return "ShipPublicApiSelfCheck: ShipPublicApi reported Directory.Build.props as updated";
        }
        foreach (var (unshipped, lines) in expected)
        {
            var relative = RootDirectory.GetRelativePathTo(unshipped);
            var scratchUnshipped = scratch / relative;
            // By content: an Unshipped with nothing pending is left as it is, whatever its trailing whitespace.
            var text = scratchUnshipped.ReadAllText();
            if (ApiLines(text).Count != 0 || !text.Split('\n').Any(l => l.TrimEnd('\r') == PublicApiHeader))
            {
                yield return $"ShipPublicApiSelfCheck: {relative} must keep only '{PublicApiHeader}', got [{text}]";
            }
            var shipped = ApiLines((scratchUnshipped.Parent / "PublicAPI.Shipped.txt").ReadAllText());
            foreach (var line in lines.Where(l => !shipped.Contains(l)))
            {
                yield return $"ShipPublicApiSelfCheck: the Shipped twin of {relative} is missing '{line}'";
            }
        }
        const string plantedShipped = PublicApiHeader + "\nPlanted.Kept\nPlanted.New\n";
        if ((planted / "PublicAPI.Shipped.txt").ReadAllText() != plantedShipped)
        {
            yield return $"ShipPublicApiSelfCheck: the planted Shipped must be [{plantedShipped}] (sorted, *REMOVED* applied), got [{(planted / "PublicAPI.Shipped.txt").ReadAllText()}]";
        }
        var plantedReleases = "; Shipped analyzer releases\n" + releasesHelp + "\n## Release 2.0.0\n\n" + newRules;
        if ((planted / "AnalyzerReleases.Shipped.md").ReadAllText() != plantedReleases)
        {
            yield return $"ShipPublicApiSelfCheck: the planted AnalyzerReleases.Shipped.md must end with '## Release 2.0.0' and the rule, got [{(planted / "AnalyzerReleases.Shipped.md").ReadAllText()}]";
        }
        if ((planted / "AnalyzerReleases.Unshipped.md").ReadAllText() != "; Unshipped analyzer release\n" + releasesHelp)
        {
            yield return $"ShipPublicApiSelfCheck: the planted AnalyzerReleases.Unshipped.md must keep only its header, got [{(planted / "AnalyzerReleases.Unshipped.md").ReadAllText()}]";
        }
    }

    // SetBaseline against a fake feed and the real Directory.Build.props, in memory (a dry run: nothing is written).
    private IEnumerable<string> SetBaselineSelfCheck()
    {
        (string Case, string Index, string? Expected)[] feeds =
        [
            ("a feed with prereleases above the latest GA", """{"versions":["1.0.292","2.0.0-rc.1","2.0.0","2.0.10","2.0.9","2.1.0-alpha.0.3"]}""", "2.0.10"),
            ("a feed with only 1.x GAs and a 2.0 prerelease", """{"versions":["1.0.291","1.0.292","2.0.0-rc.1"]}""", null),
        ];
        foreach (var (feedCase, index, expectedVersion) in feeds)
        {
            string? actual;
            try
            {
                actual = LatestGa(index);
            }
            catch (Exception e)
            {
                actual = $"<{e.GetType().Name}>";
            }
            if (actual != expectedVersion)
            {
                yield return $"SetBaselineSelfCheck: {feedCase} must give {expectedVersion ?? "no baseline"}, got {actual ?? "none"}";
            }
        }

        // Independent of the committed baseline (§21 commits 2.0.0 after the GA push): start from the props without it,
        // with sentinel versions no commit ever holds.
        const string first = "0.0.0-selfcheck.1", second = "0.0.0-selfcheck.2";
        static string Element(string version) => $"<PackageValidationBaselineVersion>{version}</PackageValidationBaselineVersion>";
        static List<string> Added(string before, string after) =>
            [.. after.Split('\n').Except(before.Split('\n'))];
        var props = BaselineLine.Replace(BuildProps.ReadAllText(), "");
        string written, rewritten;
        try
        {
            written = WithBaseline(props, first);
            rewritten = WithBaseline(written, second);
        }
        catch (Exception e)
        {
            written = rewritten = $"<{e.GetType().Name}: {e.Message}>";
        }
        var added = Added(props, written);
        if (added is not [var line] || line.Trim() != Element(first))
        {
            yield return $"SetBaselineSelfCheck: Directory.Build.props must gain exactly the baseline line, got [{string.Join("; ", added)}]";
        }
        var replaced = Added(written, rewritten);
        if (replaced is not [var newLine] || newLine.Trim() != Element(second)
            || rewritten.Split('\n').Length != written.Split('\n').Length)
        {
            yield return $"SetBaselineSelfCheck: a second SetBaseline must replace the value in place, got [{string.Join("; ", replaced)}]";
        }

        // Dry run of the target's body against a fake flat container, on a scratch props that holds the committed 2.0.0.
        var scratchProps = Planted / "SetBaseline" / "Directory.Build.props";
        var committed = WithBaseline(props, "2.0.0");
        scratchProps.WriteAllText(committed);
        var (version, error) = SetBaselineAgainstFakeFeed(feeds[0].Index, scratchProps);
        var moved = Added(committed, scratchProps.ReadAllText());
        if (version != "2.0.10" || moved is not [var movedLine] || movedLine.Trim() != Element("2.0.10")
            || scratchProps.ReadAllText().Split('\n').Length != committed.Split('\n').Length)
        {
            yield return $"SetBaselineSelfCheck: SetBaseline against a fake feed must move 2.0.0 to 2.0.10 in place, got {version ?? error} and [{string.Join("; ", moved)}]";
        }
        scratchProps.WriteAllText(committed);
        (version, error) = SetBaselineAgainstFakeFeed(feeds[1].Index, scratchProps);
        if (error is null || scratchProps.ReadAllText() != committed)
        {
            yield return $"SetBaselineSelfCheck: SetBaseline against a feed without a 2.x GA must fail and leave the props untouched, got {version}";
        }
    }

    // SetBaselineIn against a loopback flat container (no trailing slash) serving index only at /feed/ducky/index.json.
    private static (string? Version, string? Error) SetBaselineAgainstFakeFeed(string index, AbsolutePath props)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            var found = context.Request.Url!.AbsolutePath == "/feed/ducky/index.json";
            context.Response.StatusCode = found ? 200 : 404;
            if (found)
            {
                await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(index));
            }
            context.Response.Close();
        });
        try
        {
            return (SetBaselineIn($"http://127.0.0.1:{port}/feed", props).GetAwaiter().GetResult(), null);
        }
        catch (Exception e)
        {
            return (null, $"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            listener.Stop();
        }
    }

    private static HashSet<string> ApiLines(string text) =>
        [.. text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0 && l != PublicApiHeader)];
}

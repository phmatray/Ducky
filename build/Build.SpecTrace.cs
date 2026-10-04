using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §4, §17.1 (Spec trace) and §19: SpecTraceGate over the staged docs/spec/tests.yaml (§24, PLAN P1).
internal sealed partial class Build
{
    private AbsolutePath Spec => RootDirectory / "docs" / "spec" / "SPEC.md";

    // The AotSmoke target checks these names against the binary's PASS lines (§19); this gate only checks their names.
    private const string AotSmokeProject = "Ducky.AotSmoke";

    private static readonly Regex DiscoveredAssembly = new(@"^Discovered \d+ tests? in assembly - (.+?\.dll) \(", RegexOptions.Compiled);
    private static readonly Regex Placeholder = new(@"\{\w+\}", RegexOptions.Compiled);
    private static readonly Regex InvariantRow = new(@"^\| \*\*(INV-\d+)\*\* \|", RegexOptions.Compiled);
    private static readonly Regex Backticked = new("`([^`]+)`", RegexOptions.Compiled);

    // §4 catalogues INV-01..INV-32; --strict-stages checks all 32 (§17.1, §19).
    private static readonly string[] SpecInvariantIds = [.. Enumerable.Range(1, 32).Select(i => $"INV-{i:00}")];

    private Target SpecTraceGate => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            // One run lists every .NET test project of the solution, Ducky.E2E included (§17.1).
            var listed = ListedTests(DotNet($"test --solution {Solution} -c Release --no-build --list-tests", logOutput: false)
                .Select(o => o.Text));
            var testDirectory = RootDirectory / "test";
            var unlisted = Solution.AllProjects
                .Where(p => testDirectory.Contains(p.Path) && !listed.ContainsKey(p.Name))
                .Select(p => $"{p.Name}: --list-tests reported no test assembly for this project");
            var manifest = ParseManifest(Manifest.ReadAllText());
            var violations = unlisted
                .Concat(SpecTraceViolations(manifest, Spec.ReadAllText(), listed, StrictSpecTrace, SpecInvariantIds))
                .Concat(SpecTraceSelfCheck())
                .ToList();
            violations.ForEach(v => Log.Error(v));
            Assert.True(violations.Count == 0, $"SpecTraceGate: {violations.Count} violation(s)");
            Log.Information("SpecTraceGate: {Count} manifest entries, activeStage {Stage}{Strict}", manifest.Tests.Count,
                manifest.ActiveStage, StrictSpecTrace ? " (strict)" : "");
        });

    private sealed class TestManifest
    {
        public int ActiveStage { get; set; }
        public Dictionary<string, int> Projects { get; set; } = [];
        public Dictionary<string, int> Invariants { get; set; } = [];
        public List<ManifestTest> Tests { get; set; } = [];
    }

    private sealed class ManifestTest
    {
        public string Name { get; set; } = "";
        public List<string> Projects { get; set; } = [];
        public List<string> Invariants { get; set; } = [];
        public int Stage { get; set; }
    }

    private static TestManifest ParseManifest(string yaml) =>
        new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).WithDuplicateKeyChecking().Build()
            .Deserialize<TestManifest>(yaml);

    // `dotnet test --solution … --list-tests` prints one "Discovered N tests in assembly - <path>.dll (…)" header per
    // test module, then its tests indented by two spaces.
    private static Dictionary<string, HashSet<string>> ListedTests(IEnumerable<string> lines)
    {
        var listed = new Dictionary<string, HashSet<string>>();
        HashSet<string>? current = null;
        foreach (var line in lines)
        {
            var header = DiscoveredAssembly.Match(line);
            if (header.Success)
            {
                listed[Path.GetFileNameWithoutExtension(header.Groups[1].Value)] = current = [];
            }
            else if (current != null && line.StartsWith("  ", StringComparison.Ordinal))
            {
                current.Add(line.Trim());
            }
            else
            {
                current = null;
            }
        }
        return listed;
    }

    // §19: the name checks always run; existence and the covered-test rule apply to active entries (stage <= activeStage);
    // strict (--strict-stages, -rc.N and GA tags) also fails on any inactive entry and checks every §4 invariant.
    private static List<string> SpecTraceViolations(TestManifest manifest, string spec, Dictionary<string, HashSet<string>> listed, bool strict,
        IReadOnlyList<string> invariantIds)
    {
        var violations = new List<string>();
        var heading = spec.IndexOf("\n## 4.", StringComparison.Ordinal);
        if (heading < 0)
        {
            violations.Add("SPEC.md has no '## 4.' heading (the invariants catalogue)");
        }
        var start = heading + 1;
        var end = spec.IndexOf("\n## ", start, StringComparison.Ordinal);
        var rows = spec[start..(end < 0 ? spec.Length : end)].Split('\n')
            .Select(line => (Match: InvariantRow.Match(line), Line: line.TrimEnd().TrimEnd('|')))
            .Where(row => row.Match.Success)
            .Select(row => (Id: row.Match.Groups[1].Value, Names: Backticked.Matches(row.Line.Split(" | ")[^1]).Select(m => m.Groups[1].Value).ToList()))
            .ToList();
        // YamlDotNet reads `name:` as null; a blank name would match the empty string in every check below.
        manifest.Tests.ForEach(t => t.Name ??= "");
        var names = manifest.Tests.Select(t => t.Name).ToList();
        violations.AddRange(names.Where(string.IsNullOrWhiteSpace).Select(_ => "a manifest entry has a blank name"));

        // A {Placeholder} name broad enough to match another manifest or §4 name would stand in for it in all three checks
        // (a bare `{X}` matches every name). Placeholders in the other name are filled in so a wider pattern is caught too.
        violations.AddRange(
            from n in names.Distinct()
            where Placeholder.IsMatch(n)
            let shadowed = names.Concat(rows.SelectMany(r => r.Names)).Distinct()
                .Where(other => other != n && TestNameMatches(n, Placeholder.Replace(other, "x"))).ToList()
            where shadowed.Count > 0
            select $"{n} also matches {string.Join(", ", shadowed)}; a {{Placeholder}} name must not stand in for another name");

        // The §4 table must parse to exactly the expected invariants (all 32 in SPEC.md), each naming a test, or every check
        // below would pass on nothing.
        if (!rows.Select(r => r.Id).SequenceEqual(invariantIds))
        {
            violations.Add($"SPEC.md §4: expected the invariant rows [{string.Join(", ", invariantIds)}], parsed [{string.Join(", ", rows.Select(r => r.Id))}]");
        }
        violations.AddRange(rows.Where(r => r.Names.Count == 0).Select(r => $"SPEC.md §4: {r.Id} names no test in its last column"));
        violations.AddRange(manifest.Invariants.Keys.Except(invariantIds).Select(id => $"{id} is in the manifest's invariants: map but is not a §4 invariant"));
        violations.AddRange(invariantIds.Except(manifest.Invariants.Keys).Select(id => $"{id} is missing from the manifest's invariants: map"));

        // YamlDotNet fills a missing field with its default, which would silently switch checks off.
        if (manifest.ActiveStage < 1)
        {
            violations.Add($"activeStage is {manifest.ActiveStage}; it must be at least 1");
        }
        violations.AddRange(manifest.Invariants.Where(i => i.Value < 1).Select(i => $"{i.Key} has stage {i.Value} in the manifest's invariants: map; it must be at least 1"));
        violations.AddRange(manifest.Tests.Where(t => t.Stage < 1).Select(t => $"{t.Name} has stage {t.Stage}; it must be at least 1"));
        violations.AddRange(manifest.Tests.Where(t => t.Projects.Count == 0).Select(t => $"{t.Name} lists no projects"));
        violations.AddRange(
            from t in manifest.Tests
            from g in t.Projects.GroupBy(p => p)
            where g.Count() > 1
            select $"{t.Name} lists {g.Key} twice");

        violations.AddRange(names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => $"{g.Key} is listed {g.Count()} times in the manifest"));
        violations.AddRange(names
            .Where(n => !Regex.IsMatch(spec, $@"(?<![\w{{])(?:{Regex.Escape(n)}|{NamePattern(n)})(?![\w}}])"))
            .Select(n => $"{n} does not appear in SPEC.md"));
        violations.AddRange(
            from row in rows
            from name in row.Names
            where !names.Any(n => TestNameMatches(n, name))
            select $"§4 names {name} ({row.Id}), which is missing from the manifest");
        // The stage and covered-test rules read `invariants:`, so a §4 test must keep its row's ID or it could be postponed
        // while a trivial entry carries the invariant instead.
        violations.AddRange(
            from row in rows
            from name in row.Names
            from t in manifest.Tests
            where t.Name == name && !t.Invariants.Contains(row.Id)
            select $"{name} is named by {row.Id} in §4 but does not map to it");
        violations.AddRange(
            from t in manifest.Tests
            from id in t.Invariants
            where !manifest.Invariants.ContainsKey(id)
            select $"{t.Name} maps to {id}, which is not in the manifest's invariants: map");

        // P1: an invariant's stage is the lowest stage of its covered entries (INV-24: of its AotSmoke entries), so raising
        // it cannot postpone the covered-test rule below.
        violations.AddRange(
            from i in manifest.Invariants
            let kind = i.Key == "INV-24" ? AotSmokeProject : "covered"
            let stages = manifest.Tests
                .Where(t => t.Invariants.Contains(i.Key) && t.Projects.Any(p => i.Key == "INV-24" ? p == AotSmokeProject : IsCovered(p)))
                .Select(t => t.Stage).ToList()
            where stages.Count > 0 && i.Value != stages.Min()
            select $"{i.Key} has stage {i.Value} in invariants: but the lowest-stage {kind} entry mapped to it has stage {stages.Min()}");

        bool Active(int stage) => strict || stage <= manifest.ActiveStage;
        if (strict)
        {
            violations.AddRange(manifest.Tests.Where(t => t.Stage > manifest.ActiveStage)
                .Select(t => $"{t.Name} is inactive (stage {t.Stage} > activeStage {manifest.ActiveStage})"));
        }
        violations.AddRange(
            from t in manifest.Tests
            where Active(t.Stage)
            from project in t.Projects
            where project != AotSmokeProject && !(listed.TryGetValue(project, out var tests) && tests.Any(test => TestNameMatches(t.Name, test)))
            select $"{t.Name} (stage {t.Stage}) is missing from {project}");
        violations.AddRange(
            from id in invariantIds
            where id != "INV-24" && manifest.Invariants.TryGetValue(id, out var stage) && Active(stage)
                && !manifest.Tests.Any(t => Active(t.Stage) && t.Invariants.Contains(id) && t.Projects.Any(IsCovered))
            select $"{id} (stage {manifest.Invariants[id]}) has no active manifest entry in a covered project (§17.1)");
        return violations;
    }

    // Covered = measured by CoverageGate (§17.1): neither Ducky.Concurrency.Tests, Ducky.E2E nor the AotSmoke binary.
    private static bool IsCovered(string project) => project != AotSmokeProject && !CoverageExclusions.Contains(project);

    private static string NamePattern(string name) => string.Join(@"\w+", Placeholder.Split(name).Select(Regex.Escape));

    // The one matching rule of §17.1: a {Placeholder} matches \w+ at any position, and a listed test matches on its method
    // name, the last '.'-separated segment before any '(' (a theory listed as "…Name(repeat: 1)" matches Name).
    private static bool TestNameMatches(string pattern, string test)
    {
        var method = test.Split('(')[0].Split('.')[^1];
        return method == pattern || Regex.IsMatch(method, $"^{NamePattern(pattern)}$");
    }

    // The acceptance checks of M0-06, planted on a miniature spec, manifest and listing.
    private static IEnumerable<string> SpecTraceSelfCheck()
    {
        const string spec = """
            ## 3. Before

            ## 4. Invariants catalogue

            | ID | Invariant | Enforced in | Named tests |
            |---|---|---|---|
            | **INV-01** | Reducers never run concurrently | `Dispatcher` | `Reducers_NeverRunConcurrently`, `Reducers_DispatchFromReducer_QueuedNotNested` |
            | **INV-24** | Zero IL warnings | csproj | `AotSmoke` (the binary's assertion) |

            ## 5. Next
            `Skeleton_{Project}_Smoke`, `Caching_{Step}_Cached`
            """;
        static TestManifest Manifest(Action<TestManifest>? plant = null)
        {
            List<ManifestTest> tests =
            [
                new() { Name = "Reducers_NeverRunConcurrently", Projects = [ConcurrencyTests], Invariants = ["INV-01"], Stage = 2 },
                new() { Name = "Reducers_DispatchFromReducer_QueuedNotNested", Projects = ["Ducky.Tests"], Invariants = ["INV-01"], Stage = 2 },
                new() { Name = "AotSmoke", Projects = [AotSmokeProject], Invariants = ["INV-24"], Stage = 1 },
                new() { Name = "Skeleton_{Project}_Smoke", Projects = ["Ducky.Tests"], Stage = 1 },
                new() { Name = "Caching_{Step}_Cached", Projects = ["Ducky.Generators.Tests"], Stage = 7 },
            ];
            var manifest = new TestManifest { ActiveStage = 2, Invariants = new() { ["INV-01"] = 2, ["INV-24"] = 1 }, Tests = tests };
            plant?.Invoke(manifest);
            return manifest;
        }
        static Dictionary<string, HashSet<string>> Listed(string reducerTest = "Reducers_DispatchFromReducer_QueuedNotNested") => new()
        {
            ["Ducky.Tests"] = [$"Ducky.Tests.Pipeline.{reducerTest}", "Ducky.Tests.SkeletonTests.Skeleton_DuckyTests_Smoke"],
            [ConcurrencyTests] = ["Ducky.Concurrency.Tests.DispatcherTests.Reducers_NeverRunConcurrently(repeat: 1)"],
        };
        static List<string> Check(TestManifest manifest, string planted = spec, Dictionary<string, HashSet<string>>? listed = null, bool strict = false) =>
            SpecTraceViolations(manifest, planted, listed ?? Listed(), strict, ["INV-01", "INV-24"]);
        IEnumerable<(string Case, List<string> Violations, string Expected)> planted =
        [
            ("deleting a §4 name from the manifest", Check(Manifest(m => m.Tests.RemoveAll(e => e.Name == "Reducers_NeverRunConcurrently"))),
                "§4 names Reducers_NeverRunConcurrently"),
            ("a manifest name absent from SPEC.md", Check(Manifest(m => m.Tests.Add(new() { Name = "Ghost_Test_Name", Projects = ["Ducky.Tests"], Stage = 9 }))),
                "Ghost_Test_Name does not appear in SPEC.md"),
            ("renaming an active test", Check(Manifest(), listed: Listed("Reducers_DispatchFromReducer_QueuedNotNestedRenamed")),
                "Reducers_DispatchFromReducer_QueuedNotNested (stage 2) is missing from Ducky.Tests"),
            ("an active invariant whose only active test is in Ducky.Concurrency.Tests", Check(Manifest(m => m.Tests[1].Stage = 3)),
                "INV-01 (stage 2) has no active manifest entry in a covered project"),
            ("a §4-named entry that drops its row's invariant", Check(Manifest(m => m.Tests[1].Invariants = [])),
                "Reducers_DispatchFromReducer_QueuedNotNested is named by INV-01 in §4 but does not map to it"),
            ("--strict-stages with an inactive entry", Check(Manifest(), strict: true),
                "Caching_{Step}_Cached is inactive (stage 7 > activeStage 2)"),
            ("a SPEC.md without the §4 heading", Check(Manifest(), spec.Replace("## 4. Invariants catalogue", "## Invariants catalogue", StringComparison.Ordinal)),
                "SPEC.md has no '## 4.' heading"),
            ("a §4 table whose IDs are not bold", Check(Manifest(), spec.Replace("| **INV-01** |", "| INV-01 |", StringComparison.Ordinal)),
                "SPEC.md §4: expected the invariant rows [INV-01, INV-24], parsed [INV-24]"),
            ("a §4 row with no named test in its last column", Check(Manifest(), spec.Replace("(the binary's assertion) |", "(the binary's assertion) | none |", StringComparison.Ordinal)),
                "SPEC.md §4: INV-24 names no test in its last column"),
            ("an invariants: key that is not a §4 row", Check(Manifest(m => m.Invariants["INV-33"] = 2)),
                "INV-33 is in the manifest's invariants: map but is not a §4 invariant"),
            ("an invariants: map missing a §4 row", Check(Manifest(m => m.Invariants.Remove("INV-24"))),
                "INV-24 is missing from the manifest's invariants: map"),
            ("a missing activeStage", Check(Manifest(m => m.ActiveStage = 0)),
                "activeStage is 0; it must be at least 1"),
            ("an active entry with projects: []", Check(Manifest(m => m.Tests[1].Projects = [])),
                "Reducers_DispatchFromReducer_QueuedNotNested lists no projects"),
            ("an entry listing a project twice", Check(Manifest(m => m.Tests[1].Projects = ["Ducky.Tests", "Ducky.Tests"])),
                "Reducers_DispatchFromReducer_QueuedNotNested lists Ducky.Tests twice"),
            ("an entry with a missing stage", Check(Manifest(m => m.Tests[3].Stage = 0)),
                "Skeleton_{Project}_Smoke has stage 0; it must be at least 1"),
            ("an invariant with a missing stage", Check(Manifest(m => m.Invariants["INV-01"] = 0)),
                "INV-01 has stage 0 in the manifest's invariants: map; it must be at least 1"),
            ("an invariant stage raised past its covered entries", Check(Manifest(m => m.Invariants["INV-01"] = 5)),
                "INV-01 has stage 5 in invariants: but the lowest-stage covered entry mapped to it has stage 2"),
            ("an invariant stage lowered below its covered entries", Check(Manifest(m => m.Invariants["INV-01"] = 1)),
                "INV-01 has stage 1 in invariants: but the lowest-stage covered entry mapped to it has stage 2"),
            ("INV-24's stage raised past its AotSmoke entry", Check(Manifest(m => m.Invariants["INV-24"] = 4)),
                "INV-24 has stage 4 in invariants: but the lowest-stage Ducky.AotSmoke entry mapped to it has stage 1"),
            ("a bare {X} entry standing in for every name", Check(Manifest(m => m.Tests.Add(new() { Name = "{X}", Projects = ["Ducky.Tests"], Invariants = ["INV-01", "INV-24"], Stage = 1 }))),
                "{X} also matches Reducers_NeverRunConcurrently"),
            ("a Reducers_{Case} entry next to the Reducers_* names", Check(Manifest(m => m.Tests.Add(new() { Name = "Reducers_{Case}", Projects = ["Ducky.Tests"], Stage = 9 }))),
                "Reducers_{Case} also matches Reducers_NeverRunConcurrently, Reducers_DispatchFromReducer_QueuedNotNested;"),
            ("an entry with an empty name", Check(Manifest(m => m.Tests.Add(new() { Name = "", Projects = ["Ducky.Tests"], Stage = 9 }))),
                "a manifest entry has a blank name"),
            ("an entry with a missing name", Check(Manifest(m => m.Tests.Add(new() { Name = null!, Projects = ["Ducky.Tests"], Stage = 9 }))),
                "a manifest entry has a blank name"),
        ];
        foreach (var violation in Check(Manifest()))
        {
            yield return $"SpecTraceSelfCheck: the unplanted fixture must pass, got: {violation}";
        }
        foreach (var (plantedCase, violations, expected) in planted.Where(p => !p.Violations.Any(v => v.Contains(p.Expected, StringComparison.Ordinal))))
        {
            yield return $"SpecTraceSelfCheck: {plantedCase} must report '{expected}', got [{string.Join("; ", violations)}]";
        }
        (string Pattern, string Test, bool Matches)[] matching =
        [
            ("Caching_{Step}_Cached", "Caching_Pipeline_Cached", true),
            ("Skeleton_{Project}_Smoke", "Skeleton_DuckyTests_Smoke", true),
            ("Skeleton_{Project}_Smoke", "Ducky.Concurrency.Tests.SkeletonTests.Skeleton_DuckyConcurrencyTests_Smoke(repeat: 7)", true),
            ("{Area}_Smoke", "Skeleton_Smoke", true),
            ("Caching_{Step}_Cached", "Caching_Pipeline_CachedTwice", false),
            ("Caching_{Step}_Cached", "Caching__Cached", false),
            ("Reducers_NeverRunConcurrently", "Reducers_NeverRunConcurrentlyToo", false),
            ("Reducers_NeverRunConcurrently", "Reducers.NeverRunConcurrently", false),
        ];
        foreach (var (pattern, test, matches) in matching.Where(m => TestNameMatches(m.Pattern, m.Test) != m.Matches))
        {
            yield return $"SpecTraceSelfCheck: '{pattern}' must {(matches ? "" : "not ")}match '{test}'";
        }
        // PLAN §1 resolves tests.yaml conflicts by keeping both sides: a doubled key must fail, not keep the last value.
        static bool Rejects(Action<string> parse, string yaml)
        {
            try { parse(yaml); return false; }
            catch (YamlException) { return true; }
        }
        (string Parser, Action<string> Parse, string Yaml)[] duplicates =
        [
            (nameof(ParseManifest), y => ParseManifest(y), "activeStage: 3\nactiveStage: 2\n"),
            (nameof(ParseManifest), y => ParseManifest(y), "tests:\n  - { name: A, stage: 2, stage: 7 }\n"),
            (nameof(ManifestStages), y => ManifestStages(y), "activeStage: 3\nactiveStage: 2\nprojects: { Ducky.Tests: 1 }\n"),
        ];
        foreach (var (parser, _, yaml) in duplicates.Where(d => !Rejects(d.Parse, d.Yaml)))
        {
            yield return $"SpecTraceSelfCheck: {parser} must reject the duplicate key in '{yaml.ReplaceLineEndings(" | ")}'";
        }
    }
}

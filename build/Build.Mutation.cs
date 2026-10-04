using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Serilog;
using YamlDotNet.Serialization;
using static Fallout.Common.Tools.DotNet.DotNetTasks;

// SPEC §17.8 and §19: MutationPr, Mutation, PropertyLong, and the stryker-config.json checks of ExclusionGate (§17.1).
internal sealed partial class Build
{
    // The mutated projects and their stages, pinned by §17.8 (the §24 step that gives each one real logic): the manifest's
    // projects: map must equal it, since every story edits the manifest and a raised stage would silently skip a project.
    // Each has src/<name>/stryker-config.json.
    private static readonly Dictionary<string, int> MutatedProjects = new()
    {
        ["Ducky"] = 2, ["Ducky.Generators"] = 7, ["Ducky.Testing"] = 9, ["Ducky.Blazor"] = 10, ["Ducky.Reactive"] = 16,
        ["Ducky.Draft"] = 17, ["Ducky.Draft.Generators"] = 17,
    };

    private const string ConcurrencyTests = "Ducky.Concurrency.Tests";
    private const double BreakScore = 85;
    private const double TargetScore = 90;

    [Parameter("Mutation: mutate only this project (repeatable); default every active mutated project")]
    private readonly string[] Project = [];

    // SPEC's `--strict` (§17.8, §19): Fallout 10.4 reserves `strict` (its execution planner then demands one total target
    // order and fails every run of this build), so the switch is --strict-stages. SpecTraceGate shares it.
    [Parameter("For -rc.N and GA tags: every stage counts as active; Mutation mutates and checks all seven projects (§17.8), SpecTraceGate fails on any inactive entry and checks all 32 invariants (§19)")]
    private readonly bool StrictStages;

    private AbsolutePath Manifest => RootDirectory / "docs" / "spec" / "tests.yaml";

    // --since per active project; Stryker scores only the mutants of changed files, and a diff without any is green,
    // unless a test, shared-helper or build input changed (CanChangeResultsOf): that project gets a full run.
    private Target MutationPr => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var projects = MutatedProjectsToRun(strict: false);
            var baseRef = BaseRef ?? $"origin/{(EnvironmentInfo.GetVariable("GITHUB_BASE_REF") is { Length: > 0 } b ? b : ReleaseBranch)}";
            var since = Git(RootDirectory, $"rev-parse --verify {baseRef + "^{commit}"}").Single();
            var mergeBase = Git(RootDirectory, $"merge-base {since} HEAD").Single();
            // activeStage only moves up (PLAN P1): a lowered one would stop mutating active projects, so it fails before the
            // no-active-project return. The change that raises it seldom touches the project's .cs files, so a --since run
            // would score none of the code that landed while the project was inactive: a project active now but not at the
            // base gets a full run.
            var (activeStage, _) = ManifestStages(Manifest.ReadAllText());
            var (baseStage, activeAtBase) = ActiveProjectsAt(mergeBase);
            Assert.True(activeStage >= baseStage, $"{Relative(Manifest)}: activeStage {activeStage} is below {baseStage} at the merge base {mergeBase}; it never moves down");
            if (projects.Count == 0)
            {
                return;
            }
            var workspace = MutationWorkspace(since);
            var changed = ChangedPaths(workspace, mergeBase);
            var failures = new List<string>();
            foreach (var project in projects)
            {
                if (!activeAtBase.Contains(project))
                {
                    Log.Information("{Project}: inactive at the merge base {MergeBase}, active now; full run", project, mergeBase);
                    Mutate(project, since: null, workspace, failures);
                    continue;
                }
                var testDirectories = TestProjectsOf(project).Select(p => p[..(p.LastIndexOf('/') + 1)]).ToList();
                if (changed.FirstOrDefault(path => CanChangeResultsOf(project, testDirectories, path)) is { } trigger)
                {
                    Log.Information("{Project}: {Path} can change its results without changing a mutant; full run", project, trigger);
                    Mutate(project, since: null, workspace, failures);
                    continue;
                }
                Mutate(project, since, workspace, failures, changed);
            }
            failures.ForEach(f => Log.Error(f));
            Assert.True(failures.Count == 0, $"MutationPr: {failures.Count} project(s) failed");
        });

    // Nightly full run per active project (or --project); below 90% an issue is opened, below 85% or with zero mutants
    // the target fails.
    private Target Mutation => _ => _
        .DependsOn(Compile)
        .Executes(() => RunMutation(StrictStages));

    // Mutation's body, also run in-process by MutationForSha's fallback: a child build could not open the build.log
    // this process holds.
    private void RunMutation(bool strict)
    {
        var unknown = Project.Except(MutatedProjects.Keys).ToList();
        Assert.True(unknown.Count == 0, $"--project: unknown [{string.Join(", ", unknown)}]; mutated projects are [{string.Join(", ", MutatedProjects.Keys)}]");
        var projects = MutatedProjectsToRun(strict);
        // A named project that is not run would leave the target green with nothing mutated.
        var inactive = Project.Except(projects).ToList();
        Assert.True(inactive.Count == 0, $"--project: [{string.Join(", ", inactive)}] above activeStage (stages logged above); pass --strict-stages to mutate them");
        var failures = new List<string>();
        var belowTarget = new List<(string Project, double Score)>();
        foreach (var project in projects.Where(p => Project.Length == 0 || Project.Contains(p)))
        {
            if (Mutate(project, since: null, RootDirectory, failures) is { } score && score < TargetScore)
            {
                belowTarget.Add((project, score));
            }
        }
        OpenScoreIssues(belowTarget, failures);
        failures.ForEach(f => Log.Error(f));
        Assert.True(failures.Count == 0, $"Mutation: {failures.Count} project(s) failed");
    }

    // Nightly: long random CsCheck runs, covered projects and the SampleParallel models of Ducky.Concurrency.Tests.
    private Target PropertyLong => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var environment = new Dictionary<string, string>(EnvironmentInfo.Variables)
            {
                ["CsCheck_Iter"] = "100000",
                ["DUCKY_REPEAT"] = "1",
            };
            environment.Remove("DUCKY_PROPERTY_SEEDS");
            var results = Artifacts / "property-long";
            results.CreateOrCleanDirectory();
            DotNet($"test --solution {TestFilter} -c Release --no-build --report-trx --results-directory {results}",
                environmentVariables: environment);
            DotNet($"test --project {RootDirectory / "test" / ConcurrencyTests} -c Release --no-build --report-trx --results-directory {results}",
                environmentVariables: environment);
        });

    // The projects whose manifest stage is at or below activeStage, or all seven when strict; the others are logged.
    private List<string> MutatedProjectsToRun(bool strict)
    {
        Assert.FileExists(Manifest, "the staged test manifest (§24) holds activeStage and the projects: stages");
        var (activeStage, stages) = ManifestStages(Manifest.ReadAllText());
        Assert.True(stages.Count == MutatedProjects.Count && !stages.Except(MutatedProjects).Any(),
            $"{Relative(Manifest)}: projects: must be exactly {{ {Stages(MutatedProjects)} }} (§17.8); found {{ {Stages(stages)} }}");

        var run = new List<string>();
        foreach (var project in MutatedProjects.Keys)
        {
            if (strict || stages[project] <= activeStage)
            {
                run.Add(project);
                continue;
            }
            Log.Information("{Project}: stage {Stage} is above activeStage {ActiveStage}, not mutated", project, stages[project], activeStage);
        }
        return run;
    }

    private static string Stages(Dictionary<string, int> stages) => string.Join(", ", stages.Select(s => $"{s.Key}: {s.Value}"));

    // The manifest's activeStage and projects: stages, from its text (the working tree's, or a commit's in MutationPr).
    private static (int ActiveStage, Dictionary<string, int> Stages) ManifestStages(string yaml)
    {
        var manifest = new DeserializerBuilder().WithDuplicateKeyChecking().Build().Deserialize<Dictionary<string, object>>(yaml);
        return (int.Parse((string)manifest["activeStage"], CultureInfo.InvariantCulture),
            ((Dictionary<object, object>)manifest["projects"]).ToDictionary(p => (string)p.Key, p => int.Parse((string)p.Value, CultureInfo.InvariantCulture)));
    }

    // The manifest's activeStage at a commit and the projects active there; stage 0 and none when it did not exist there.
    private (int ActiveStage, List<string> Active) ActiveProjectsAt(string commit)
    {
        var path = Relative(Manifest);
        if (Git(RootDirectory, $"ls-tree --name-only {commit} -- {path}").Count == 0)
        {
            return (0, []);
        }
        var (activeStage, stages) = ManifestStages(string.Join('\n', Git(RootDirectory, $"show {commit}:{path}")));
        return (activeStage, [.. stages.Where(s => s.Value <= activeStage).Select(s => s.Key)]);
    }

    // Runs Stryker from <workspace>/src/<project> (its stryker-config.json) and returns the score, or null when no
    // mutant counts. The test hosts inherit the Stryker process environment (S-6): one repetition, fixed seeds, one
    // CsCheck thread. Logs the mutants ignored by `// Stryker disable once` comments (§17.8: CI reports the count).
    // Added to failures: a missing or unreadable report, zero mutants on a full run (since null; a --since run may
    // legitimately have none), a score below the break threshold, a failed run, a disable that ignored mutants beyond
    // DisableReach lines below it, a Killed mutant without killedBy, and, on a full run, tests that killed every killed
    // mutant across several files (they fail on every mutated build, whatever the active mutant, S-6).
    // `changed`: for a --since run, the paths changed since the merge base, to cross-check Stryker's diff.
    private double? Mutate(string project, string? since, AbsolutePath workspace, List<string> failures, List<string>? changed = null)
    {
        var output = Artifacts / "mutation" / project;
        output.CreateOrCleanDirectory();
        // The dotnet-stryker local tool (.config/dotnet-tools.json, the one pin), like reportgenerator in CoverageGate:
        // StrykerTasks would need a second pin as a PackageDownload of this project.
        // The mtp runner hands each test host its active mutant through <temp>/stryker-mutant-<runner>.txt: under the
        // shared temp directory, concurrent runs (parallel worktrees) overwrite each other's mutant ids and score the
        // wrong mutants, so every run gets its own. A short one under /tmp, not under artifacts/: the test hosts' IPC
        // sockets live there too, and a macOS socket path is capped at 104 bytes (a longer one fails test discovery).
        var temp = (AbsolutePath)Path.Combine(OperatingSystem.IsWindows() ? Path.GetTempPath() : "/tmp", $"dks-{Guid.NewGuid():N}"[..12]);
        temp.CreateDirectory();
        var environment = new Dictionary<string, string>(EnvironmentInfo.Variables)
        {
            ["DUCKY_REPEAT"] = "1",
            ["DUCKY_PROPERTY_SEEDS"] = "1",
            ["CsCheck_Threads"] = "1",
            ["TMPDIR"] = temp,
            ["TMP"] = temp,
            ["TEMP"] = temp,
        };
        var sinceArgument = since is null ? "" : $"--since:{since}";
        var exitCode = 0;
        DotNet($"stryker --test-runner mtp --reporter Progress --reporter Html --reporter Json --output {output} {sinceArgument:nq}",
            workingDirectory: workspace / "src" / project,
            environmentVariables: environment,
            exitHandler: p => exitCode = p.ExitCode);
        temp.DeleteDirectory();

        // Stryker writes the report even with zero mutants ("a mutant-free world"), so a missing one is a broken run.
        var report = output / "reports" / "mutation-report.json";
        if (!report.FileExists() || JsonNode.Parse(report.ReadAllText()) is not JsonObject { } root || root["files"] is not JsonObject files)
        {
            failures.Add($"{project}: Stryker wrote no readable {Relative(report)} with a \"files\" object (exit {exitCode})");
            return null;
        }
        var mutants = (from file in files
                       from mutant in file.Value!["mutants"]!.AsArray()
                       select (File: Path.GetRelativePath(workspace, file.Key).Replace('\\', '/'), Source: (string?)file.Value["source"] ?? "", Mutant: mutant!))
            .ToList();
        var statuses = mutants.Select(m => (string)m.Mutant["status"]!).ToList();
        var detected = statuses.Count(s => s is "Killed" or "Timeout");
        var counted = detected + statuses.Count(s => s is "Survived" or "NoCoverage");
        double? score = counted == 0 ? null : 100.0 * detected / counted;
        Log.Information("{Project}: {Counted} mutant(s) scored, score {Score}", project, counted, score?.ToString("F2", CultureInfo.InvariantCulture) ?? "none");

        if (score is null && since is null)
        {
            failures.Add($"{project}: zero mutants or no score (a mutate pattern that matches nothing must not pass)");
        }
        else if (score is null)
        {
            Log.Information("{Project}: no mutant in the diff since {Since}", project, since);
        }
        else if (score < BreakScore)
        {
            failures.Add($"{project}: mutation score {score:F2}% is below the break threshold {BreakScore}% (report: {Relative(output)})");
        }
        // S-6: a --since diff of the wrong tree (a linked worktree) leaves the mutants of a changed file Ignored by the since
        // filter, and the run green with nothing scored. Stryker rescores every mutant of a changed file, so none may carry a
        // since-filter reason. Any, not All: mutants under a disable comment keep the comment's reason even in that bad run.
        failures.AddRange(from path in changed ?? []
                          where path.StartsWith($"src/{project}/", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal)
                          let reasons = mutants.Where(m => m.File == path).Select(m => (string?)m.Mutant["statusReason"] ?? "").ToList()
                          where reasons.Any(r => r == "Removed by since filter" || r.StartsWith("Mutant not changed", StringComparison.Ordinal))
                          select $"{project}: {path} changed since {since} but Stryker saw no changed mutant in it: the --since diff is wrong");
        if (exitCode != 0 && !(score < BreakScore))
        {
            failures.Add($"{project}: Stryker exited with code {exitCode}");
        }

        // Stryker's own filters give reasons starting "Removed by" or "Mutant"; any other Ignored reason is a comment's
        // (ExclusionGate rejects a comment reason that starts like Stryker's).
        // Each mutant is tied to the nearest disable comment above it that carries its reason.
        var disabled = (from m in mutants
                        let reason = (string?)m.Mutant["statusReason"] ?? ""
                        where (string?)m.Mutant["status"] == "Ignored"
                            && !reason.StartsWith("Removed by", StringComparison.Ordinal) && !reason.StartsWith("Mutant", StringComparison.Ordinal)
                        let lines = m.Source.Split('\n')
                        let start = Math.Min((int)m.Mutant["location"]!["start"]!["line"]!, lines.Length)
                        let comment = 1 + Array.FindLastIndex(lines, start - 1, start,
                            l => l.Contains("Stryker disable", StringComparison.OrdinalIgnoreCase) && l.Contains(reason, StringComparison.Ordinal))
                        select (m.File, Comment: comment, Reason: reason, End: (int)m.Mutant["location"]!["end"]!["line"]!))
            .ToList();
        Log.Information("{Project}: {Disabled} mutant(s) ignored by Stryker disable comments", project, disabled.Count);
        failures.AddRange(disabled.GroupBy(d => (d.File, d.Comment, d.Reason))
            .Where(g => g.Max(d => d.End) - g.Key.Comment > DisableReach)
            .Select(g => $"{project}: {g.Key.File}:{g.Key.Comment}: the Stryker disable '{g.Key.Reason}' ignored {g.Count()} mutant(s) down to line " +
                $"{g.Max(d => d.End)}, more than {DisableReach} lines below it: it covers a member, a type or a block, not one statement"));

        var killed = mutants.Where(m => (string?)m.Mutant["status"] == "Killed")
            .Select(m => (m.File, Tests: m.Mutant["killedBy"] is JsonArray ids ? ids.Select(id => (string)id!).ToHashSet() : []))
            .ToList();
        var unattributed = killed.Count(k => k.Tests.Count == 0);
        if (unattributed > 0)
        {
            failures.Add($"{project}: {unattributed} Killed mutant(s) without killedBy test ids: the runner no longer reports which tests kill a " +
                "mutant, so the always-failing-test check cannot run (S-6 saw killedBy on every Killed mutant under Stryker 4.16 and MTP)");
        }
        // Full runs only: a --since run scores the changed files, where one broad test can rightly kill every mutant. A test
        // that fails on every mutated build comes from a test change (a full run, MutationPr) or from Stryker (the nightly).
        else if (since is null && killed.Count >= AlwaysFailingMinimumKilled && killed.Select(k => k.File).Distinct().Count() >= AlwaysFailingMinimumFiles)
        {
            var common = killed.Select(k => k.Tests).Aggregate((all, next) => [.. all.Intersect(next)]);
            if (common.Count > 0)
            {
                var names = (root["testFiles"] as JsonObject ?? [])
                    .SelectMany(f => f.Value?["tests"]?.AsArray() ?? [])
                    .ToDictionary(t => (string)t!["id"]!, t => (string?)t!["name"]);
                var tests = common.Order(StringComparer.Ordinal).Select(id => names.GetValueOrDefault(id) ?? id);
                failures.Add($"{project}: test(s) [{string.Join(", ", tests)}] killed all {killed.Count} killed mutants in {killed.Select(k => k.File).Distinct().Count()} " +
                    "files: they fail on every mutated build whatever the active mutant (like NoStaticMutableFields on Stryker's injected helpers, " +
                    "spike S-6), so the score is not real");
            }
        }
        return score;
    }

    // Below this many killed mutants, or in fewer files, one test killing all of them is plausible (a broad end-to-end or
    // golden-snapshot test of one file); above both, it fails on every mutated build.
    private const int AlwaysFailingMinimumKilled = 10;

    private const int AlwaysFailingMinimumFiles = 3;

    // A `disable once` above one statement ignores mutants at most this many lines below it (a statement over a few
    // lines); further down, it sits above a member, a type or a block statement that ExclusionGate's text check missed.
    private const int DisableReach = 5;

    // Where MutationPr runs Stryker. S-6: in a linked git worktree (.git is a file) Stryker 4.16's --since reads the main
    // checkout's working tree, so the run moves to a throwaway plain clone of HEAD with the uncommitted changes (tracked
    // and untracked) committed on top, and the base commit fetched into it.
    private AbsolutePath MutationWorkspace(string since)
    {
        if ((RootDirectory / ".git").DirectoryExists())
        {
            return RootDirectory;
        }
        var clone = Artifacts / "mutation" / "clone";
        clone.DeleteDirectory();
        Log.Information("Linked git worktree: MutationPr runs in the plain clone {Clone} (spike S-6)", Relative(clone));
        Git(RootDirectory, $"clone --no-local --single-branch --quiet {RootDirectory} {clone}");
        var patch = Artifacts / "mutation" / "uncommitted.patch";
        Git(RootDirectory, $"diff HEAD --binary --output={patch}");
        if (patch.ReadAllText().Length > 0)
        {
            Git(clone, $"apply {patch}");
        }
        // ponytail: path lines with core.quotePath off; git still quotes a name holding '"', '\' or a control character,
        // which no repository path does (use -z and split on NUL if one ever must).
        foreach (var file in Git(RootDirectory, $"-c core.quotePath=false ls-files --others --exclude-standard"))
        {
            (clone / file).Parent.CreateDirectory();
            File.Copy(RootDirectory / file, clone / file);
        }
        Git(clone, $"add --all");
        Git(clone, $"-c user.name=MutationPr -c user.email=mutation-pr@localhost commit --quiet --allow-empty --no-verify -m uncommitted");
        Git(clone, $"fetch --quiet origin {since}");
        return clone;
    }

    // Paths changed since the merge base, committed or not, unquoted. --no-renames: a moved file lists its old path too, so
    // a test moved out of its project's directory still triggers CanChangeResultsOf.
    private static List<string> ChangedPaths(AbsolutePath workspace, string mergeBase) =>
        [.. Git(workspace, $"-c core.quotePath=false diff --name-only --no-renames {mergeBase}"),
            .. Git(workspace, $"-c core.quotePath=false ls-files --others --exclude-standard")];

    // git's standard output lines; a non-zero exit fails the target.
    private static List<string> Git(AbsolutePath directory, ArgumentStringHandler arguments) =>
        ProcessTasks.StartProcess("git", arguments, directory, logOutput: false).AssertZeroExitCode().Output
            .Where(o => o.Type == OutputType.Std)
            .Select(o => o.Text)
            .ToList();

    // A changed path outside the mutated src/<project>/**/*.cs that can still change the project's mutation results
    // (its tests, the shared test helpers and banned-symbol lists, the MSBuild and package configuration of src/ and test/,
    // the SDK and tool pins, the property seeds):
    // with --since and coverage-analysis off it would leave zero mutants to score (S-6).
    private static bool CanChangeResultsOf(string project, List<string> testDirectories, string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return (path.StartsWith($"src/{project}/", StringComparison.Ordinal) && !path.EndsWith(".cs", StringComparison.Ordinal))
            || testDirectories.Any(d => path.StartsWith(d, StringComparison.Ordinal))
            || path.StartsWith("test/Shared/", StringComparison.Ordinal)
            || (path.StartsWith("test/", StringComparison.Ordinal) && path.Count(c => c == '/') == 1)
            || ((!path.Contains('/') || path.StartsWith("src/", StringComparison.Ordinal) || path.StartsWith("test/", StringComparison.Ordinal))
                && (name.StartsWith("Directory.Build.", StringComparison.Ordinal) || name == "Directory.Packages.props"))
            || path is "global.json" or "build/property-seeds.txt" or ".config/dotnet-tools.json";
    }

    // §17.8: below the 90% target the nightly opens an issue (workflow permission issues: write, §20.1), or comments on
    // the open one. A gh failure is a failure of the run, after every project was mutated.
    private void OpenScoreIssues(List<(string Project, double Score)> belowTarget, List<string> failures)
    {
        foreach (var (project, score) in belowTarget)
        {
            var prefix = $"Mutation score of {project} is ";
            var title = $"{prefix}{score.ToString("F2", CultureInfo.InvariantCulture)}% (target {TargetScore}%)";
            if (GitHubActions.Instance is not { } actions)
            {
                Log.Warning("{Title}: the issue is opened on CI only", title);
                continue;
            }
            var body = $"The `Mutation` run {actions.ServerUrl}/{actions.Repository}/actions/runs/{actions.RunId} scored {project} below {TargetScore}%. The HTML report is in its artifacts.";
            try
            {
                var open = JsonNode.Parse(string.Join('\n', ProcessTasks.StartProcess("gh", $"issue list --state open --search {$"in:title {prefix}"} --json number,title", logOutput: false)
                        .AssertZeroExitCode().Output.Where(o => o.Type == OutputType.Std).Select(o => o.Text)))!
                    .AsArray()
                    .FirstOrDefault(i => ((string)i!["title"]!).StartsWith(prefix, StringComparison.Ordinal));
                var process = open is null
                    ? ProcessTasks.StartProcess("gh", $"issue create --title {title} --body {body}")
                    : ProcessTasks.StartProcess("gh", $"issue comment {(int)open["number"]!} --body {$"{title}. {body}"}");
                process.AssertZeroExitCode();
            }
            catch (Exception e)
            {
                failures.Add($"{project}: could not open or update the issue '{title}': {e.Message}");
            }
        }
    }

    // ExclusionGate (§17.1): the seven stryker-config.json files, nothing else named like a Stryker config, and in each
    // exactly the keys below; the rest (mutation-level, ignore-mutations, ignore-methods, since, a narrower mutate…)
    // narrows what is mutated or scored.
    private IEnumerable<string> StrykerConfigViolations()
    {
        var expected = MutatedProjects.Keys.Select(p => $"src/{p}/stryker-config.json").ToList();
        var found = GatedFiles("**/stryker-config.*", "**/*stryker*.json", "**/*stryker*.yaml", "**/*stryker*.yml");
        foreach (var extra in found.Except(expected))
        {
            yield return $"{extra}: unexpected Stryker configuration; each mutated project has exactly src/<project>/stryker-config.json";
        }

        string[] keys = ["test-runner", "thresholds", "mutate", "test-projects", "additional-timeout", "coverage-analysis"];
        foreach (var (project, file) in MutatedProjects.Keys.Zip(expected))
        {
            if (!found.Contains(file))
            {
                yield return $"{file}: missing (§17.8)";
                continue;
            }

            if (ReadStrykerConfig(RootDirectory / file) is not { } config)
            {
                yield return $"{file}: not a JSON object holding only \"stryker-config\": {{...}}";
                continue;
            }

            foreach (var key in config.Select(p => p.Key).Except(keys))
            {
                yield return $"{file}: '{key}' is not allowed: only [{string.Join(", ", keys)}] (equivalent mutants use '// Stryker disable once <mutator> : <reason>')";
            }
            foreach (var key in keys.Except(config.Select(p => p.Key)))
            {
                yield return $"{file}: '{key}' is required";
            }
            if (config["test-runner"]?.ToJsonString() is { } runner && runner != "\"mtp\"")
            {
                yield return $"{file}: test-runner must be \"mtp\"; found {runner}";
            }
            if (config["thresholds"]?.ToJsonString() is { } thresholds && thresholds != """{"high":90,"low":85,"break":85}""")
            {
                yield return $"{file}: thresholds must be {{\"high\": 90, \"low\": 85, \"break\": 85}}; found {thresholds}";
            }
            if (config["mutate"]?.ToJsonString() is { } mutate && mutate != """["**/*.cs"]""")
            {
                var negated = mutate.Contains("\"!", StringComparison.Ordinal) ? " (a '!' pattern excludes files from mutation)" : "";
                yield return $"{file}: mutate must be exactly [\"**/*.cs\"]{negated}; found {mutate}";
            }
            if (config["additional-timeout"] is { } timeout && !(int.TryParse(timeout.ToJsonString(), CultureInfo.InvariantCulture, out var ms) && ms > 0))
            {
                yield return $"{file}: additional-timeout must be a positive number of milliseconds; found {timeout.ToJsonString()}";
            }
            // S-6: with several test projects, the MTP runner's per-test coverage keeps only the last project's hits and
            // reports the other lines NoCoverage; "off" runs every test against every mutant.
            if (config["coverage-analysis"]?.ToJsonString() is { } coverage && coverage != "\"off\"")
            {
                yield return $"{file}: coverage-analysis must be \"off\" (spike S-6); found {coverage}";
            }
            if (config["test-projects"] is { } listed)
            {
                var directory = (RootDirectory / file).Parent;
                var actual = listed is JsonArray array && array.All(e => e?.GetValueKind() == JsonValueKind.String)
                    ? array.Select(e => Relative(directory / (string)e!)).ToList()
                    : null;
                var computed = TestProjectsOf(project);
                if (actual is null || !actual.ToHashSet().SetEquals(computed) || actual.Count != computed.Count)
                {
                    yield return $"{file}: test-projects must list exactly [{string.Join(", ", computed)}] (every covered test project that " +
                        $"references {project}, plus {ConcurrencyTests} when it does, §17.8); found {listed.ToJsonString()}";
                }
            }
        }
    }

    private static JsonObject? ReadStrykerConfig(AbsolutePath file)
    {
        try
        {
            return JsonNode.Parse(file.ReadAllText()) is JsonObject { Count: 1 } root && root["stryker-config"] is JsonObject config ? config : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // The covered test projects (Ducky.Tests.slnf's set) and Ducky.Concurrency.Tests whose assembly references reach
    // the project: the projects whose coverage feeds its CoverageGate row, plus the concurrency suite.
    private List<string> TestProjectsOf(string project)
    {
        var target = RootDirectory / "src" / project / $"{project}.csproj";
        return Solution.AllProjects
            .Where(p => (RootDirectory / "test").Contains(p.Path))
            .Where(p => !CoverageExclusions.Contains(p.Name) || p.Name == ConcurrencyTests)
            .Where(p => ReferencedAssemblies(p.Path).Contains(target))
            .Select(p => Relative(p.Path))
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    // Transitive ProjectReferences that bring an assembly (ReferenceOutputAssembly="false" feeds analyzers only).
    private static HashSet<AbsolutePath> ReferencedAssemblies(AbsolutePath projectFile)
    {
        var seen = new HashSet<AbsolutePath>();
        var pending = new Stack<AbsolutePath>([projectFile]);
        while (pending.TryPop(out var current))
        {
            foreach (var reference in XDocument.Load(current).Descendants("ProjectReference"))
            {
                if (string.Equals((string?)reference.Attribute("ReferenceOutputAssembly"), "false", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                var include = (string)reference.Attribute("Include")!;
                Assert.True(!include.Contains("$(", StringComparison.Ordinal), $"{current}: ProjectReference '{include}' must be a relative path");
                if (seen.Add(current.Parent / include.Replace('\\', '/')))
                {
                    pending.Push(current.Parent / include.Replace('\\', '/'));
                }
            }
        }
        return seen;
    }
}

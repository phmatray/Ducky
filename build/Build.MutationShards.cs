using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Fallout.Common;
using Fallout.Common.IO;
using Serilog;
using YamlDotNet.RepresentationModel;

// SPEC §17.8 (M0-15): the skip predicate, the shard partition and aggregation, the additive-test baseline, and the
// checks of the hand-written mutation.yml and nightly-mutation.yml (§20.1).
internal sealed partial class Build
{
    private const string SinceMode = "since";
    private const string FullMode = "full";
    private const string BaselineMode = "baseline";

    // Stryker's reason for a mutant outside every --mutate pattern.
    private const string MutateFilterReason = "Removed by mutate filter";

    // The baseline statuses an added test can change; Killed, CompileError and Ignored stay as the baseline found them.
    private static readonly string[] RerunStatuses = ["Survived", "NoCoverage", "Timeout"];

    // A tested mutant's statuses; with CompileError, Ignored and RuntimeError (a mutant that crashes the test host, as one
    // of Dispatcher.cs does in every full run), the statuses a finished run leaves. An aborted run leaves Pending.
    private static readonly string[] TestedStatuses = ["Killed", .. RerunStatuses];
    private static readonly string[] FinalStatuses = [.. TestedStatuses, "CompileError", "Ignored", "RuntimeError"];

    // Patterns MutationPr hands Stryker: project-relative files, whole or as a character span; Targets are the mutant keys
    // of each file rerun by span, WholeFiles the repository-relative files rerun whole.
    private sealed record RerunPlan(List<string> Filters, HashSet<string> WholeFiles, Dictionary<string, HashSet<string>> Targets);

    [Parameter("Mutation and MutationPr: run only shard <index>/<count> of each project's files (CI's matrix); its report goes to artifacts/mutation-shards/<index> for MutationAggregate or MutationPrAggregate")]
    private readonly string? Shard;

    [Parameter("MutationAggregate and MutationPrAggregate: the number of shards that must have reported")]
    private readonly int Shards = 4;

    private AbsolutePath ShardReports => Artifacts / "mutation-shards";

    // CI's plan job: whether the PR can change any mutation result, and the merge base whose baseline the shards restore.
    private Target MutationPlan => _ => _
        .Executes(() =>
        {
            FailOnViolations(nameof(MutationPlan), MutationSelfCheck());
            var pr = PlanMutationPr(baseline: false);
            if (EnvironmentInfo.GetVariable("GITHUB_OUTPUT") is { Length: > 0 } output)
            {
                File.AppendAllText(output, $"run={(pr.Plan is null ? "false" : "true")}\nmerge-base={pr.MergeBase}\n");
            }
        });

    // The required check `mutation`: recomputes MutationPr's plan, so a skip needs no shard and a run needs every shard;
    // each project's merged report then gets MutationPr's checks.
    private Target MutationPrAggregate => _ => _
        .Executes(() =>
        {
            FailOnViolations(nameof(MutationPrAggregate), MutationSelfCheck());
            var pr = PlanMutationPr(baseline: false);
            if (pr.Plan is null)
            {
                return;
            }
            var changed = pr.Changed.Select(c => c.Path).ToList();
            var shards = ReadShards();
            var failures = new List<string>();
            foreach (var (project, mode, _) in pr.Plan)
            {
                // The aggregate job has no baseline: a project eligible for one ran either way, and both are full-run results.
                string[] modes = mode switch { SinceMode => [SinceMode], BaselineMode => [FullMode, BaselineMode], _ => [FullMode] };
                var since = mode == SinceMode ? pr.Since : null;
                if (MergeShards(nameof(MutationPr), project, Shards, ProjectFiles(RootDirectory, project), modes, pr.MergeBase, Git(RootDirectory, $"rev-parse HEAD").Single(), shards, failures) is { } merged)
                {
                    CheckReport(project, since, merged, changed, failures, Relative(ShardReports));
                }
            }
            failures.ForEach(f => Log.Error(f));
            Assert.True(failures.Count == 0, $"MutationPrAggregate: {failures.Count} failure(s)");
        });

    // nightly-mutation's aggregate job: Mutation's checks and issues on the merged reports, and the baseline they leave.
    private Target MutationAggregate => _ => _
        .Executes(() =>
        {
            FailOnViolations(nameof(MutationAggregate), MutationSelfCheck());
            var shards = ReadShards();
            var head = Git(RootDirectory, $"rev-parse HEAD").Single();
            var failures = new List<string>();
            var belowTarget = new List<(string Project, double Score)>();
            foreach (var project in MutationProjects(StrictStages))
            {
                var before = failures.Count;
                if (MergeShards(nameof(Mutation), project, Shards, ProjectFiles(RootDirectory, project), [FullMode], null, head, shards, failures) is not { } merged)
                {
                    continue;
                }
                if (CheckReport(project, null, merged, null, failures, Relative(ShardReports)) is { } score && OpensScoreIssue(score))
                {
                    belowTarget.Add((project, score));
                }
                if (failures.Count == before)
                {
                    WriteBaseline(project, merged, shards.Where(s => (string?)s["project"] == project).Select(s => (string?)s["uncommitted"]).FirstOrDefault(u => u is not null));
                }
            }
            OpenScoreIssues(belowTarget, failures);
            failures.ForEach(f => Log.Error(f));
            Assert.True(failures.Count == 0, $"MutationAggregate: {failures.Count} failure(s)");
        });

    // The --shard of this run, or null; its report directory starts empty.
    private (int Index, int Count)? StartShard()
    {
        var shard = ParseShard(Shard);
        Assert.True(Shard is null || shard is not null, $"--shard '{Shard}': expected <index>/<count> with 1 <= index <= count, such as 1/4");
        if (shard is { } s)
        {
            (ShardReports / s.Index.ToString(CultureInfo.InvariantCulture)).CreateOrCleanDirectory();
        }
        return shard;
    }

    // One shard of a project: Stryker on the shard's files only. Its report and failures go to
    // artifacts/mutation-shards/<index>/<project>.shard.json; the checks run on the merged report in the aggregate job.
    private void RunShard(string target, string project, string mode, string? since, string? mergeBase, AbsolutePath workspace,
        (int Index, int Count) shard, List<string> changed, List<string> failures)
    {
        var files = PartitionFiles([.. ProjectFiles(workspace, project).Select(f => (f, File.ReadLines(workspace / f).Count()))], shard.Count)[shard.Index - 1];
        var directory = ShardReports / shard.Index.ToString(CultureInfo.InvariantCulture);
        // The tree the shard ran on: MergeShards refuses a shard of another commit, MutationAggregate writes no baseline
        // from a shard that ran with uncommitted inputs.
        var commit = Git(RootDirectory, $"rev-parse HEAD").Single();
        var uncommitted = UncommittedInput(project);
        Log.Information("{Project}: shard {Index}/{Count}, {Files} file(s), {Mode} run", project, shard.Index, shard.Count, files.Count, mode);
        var shardFailures = new List<string>();
        // An empty partition runs nothing: without a --mutate pattern Stryker would mutate the whole project.
        var report = files.Count == 0
            ? new JsonObject { ["files"] = new JsonObject(), ["testFiles"] = new JsonObject() }
            : RunProject(project, mode, since, mergeBase, workspace, files, directory / project, changed, shardFailures);
        (directory / $"{project}.shard.json").WriteAllText(new JsonObject
        {
            ["index"] = shard.Index,
            ["count"] = shard.Count,
            ["target"] = target,
            ["project"] = project,
            ["commit"] = commit,
            ["uncommitted"] = uncommitted,
            ["mergeBase"] = mergeBase,
            ["mode"] = mode,
            ["files"] = new JsonArray([.. files.Select(f => (JsonNode)f)]),
            ["failures"] = new JsonArray([.. shardFailures.Select(f => (JsonNode)f)]),
            ["report"] = report,
        }.ToJsonString());
        failures.AddRange(shardFailures);
    }

    private List<JsonObject> ReadShards() =>
        ShardReports.DirectoryExists()
            ? [.. ShardReports.GlobFiles("**/*.shard.json").Select(f => JsonNode.Parse(f.ReadAllText()) as JsonObject ?? throw new InvalidOperationException($"{Relative(f)}: not a JSON object"))]
            : [];

    // Runs Stryker on the whole project (files null) or on the given files; in baseline mode on the changed files and the
    // baseline's non-killed mutants only, merged with the baseline (§17.8). Returns the report of those files, or null.
    private JsonObject? RunProject(string project, string mode, string? since, string? mergeBase, AbsolutePath workspace, List<string>? files,
        AbsolutePath output, List<string> changed, List<string> failures)
    {
        var prefix = $"src/{project}/";
        var filters = files?.Select(f => f[prefix.Length..]).ToList();
        if (mode != BaselineMode)
        {
            return Stryker(project, since, workspace, filters, output, failures);
        }
        var scope = files ?? ProjectFiles(workspace, project);
        var baseline = ParseBaseline(BaselineFile(project).ReadAllText(), mergeBase!).Report!;
        var rerun = PlanRerun(project, (JsonObject)baseline["files"]!, scope, f => (workspace / f).FileExists() ? (workspace / f).ReadAllText() : null, changed);
        Log.Information("{Project}: baseline of {MergeBase}; rerunning {Whole} file(s) whole and {Targets} non-killed mutant(s) by span",
            project, mergeBase, rerun.WholeFiles.Count, rerun.Targets.Sum(t => t.Value.Count));
        var report = rerun.Filters.Count == 0
            ? new JsonObject { ["files"] = new JsonObject(), ["testFiles"] = new JsonObject() }
            : Stryker(project, since: null, workspace, rerun.Filters, output, failures);
        return report is null ? null : MergeBaseline(baseline, report, scope, rerun, failures);
    }

    private AbsolutePath BaselineFile(string project) => Artifacts / "mutation" / "baseline" / $"{project}.json";

    // The merged full report of a checkout whose mutation inputs are committed, for MutationPr's reuse; CI caches it by SHA.
    private void WriteBaseline(string project, JsonObject report, string? uncommitted = null)
    {
        if ((uncommitted ?? UncommittedInput(project)) is { } path)
        {
            Log.Information("{Project}: no baseline written: {Path} is not committed", project, path);
            return;
        }
        BaselineFile(project).Parent.CreateDirectory();
        BaselineFile(project).WriteAllText(new JsonObject
        {
            ["commit"] = Git(RootDirectory, $"rev-parse HEAD").Single(),
            ["project"] = project,
            ["report"] = report.DeepClone(),
        }.ToJsonString());
    }

    // The first uncommitted path that can change the project's results, or null: a baseline is stamped with HEAD.
    private string? UncommittedInput(string project)
    {
        var directories = TestDirectoriesOf(project);
        return ChangedPaths(RootDirectory, "HEAD")
            .FirstOrDefault(c => c.Path.StartsWith("src/", StringComparison.Ordinal) || CanChangeResultsOf(project, directories, c.Path)).Path;
    }

    // The files Stryker mutates: every .cs file of src/<project> outside bin/ and obj/, repository-relative. They go to
    // Stryker as literal --mutate patterns, so none may hold a glob character.
    private static List<string> ProjectFiles(AbsolutePath workspace, string project)
    {
        var files = (workspace / "src" / project).GlobFiles("**/*.cs")
            .Select(f => workspace.GetUnixRelativePathTo(f).ToString())
            .Where(f => !f.Split('/').Any(s => s is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .ToList();
        var globbed = files.Where(f => f.IndexOfAny(['*', '?', '[', ']', '{', '}', '!']) >= 0).ToList();
        Assert.True(globbed.Count == 0, $"[{string.Join(", ", globbed)}]: a glob character in a file name would break the shards' --mutate patterns");
        return files;
    }

    // A changed path that can change some active project's mutation result: any src/ file, a full-run trigger of an active
    // project (CanChangeResultsOf), the mutation build code and the PR mutation workflow.
    private static bool MutationRelevant(string path, Dictionary<string, List<string>> testDirectories) =>
        path.StartsWith("src/", StringComparison.Ordinal)
        || (path.StartsWith("build/Build.Mutation", StringComparison.Ordinal) && path.EndsWith(".cs", StringComparison.Ordinal))
        || path == ".github/workflows/mutation.yml"
        || testDirectories.Any(p => CanChangeResultsOf(p.Key, p.Value, path));

    // MutationPr's plan: null when no relevant path changed and no project became active (the check passes without Stryker);
    // else per project a --since run, a full run (a newly active project, or a trigger of CanChangeResultsOf), or a baseline
    // run when every trigger is a newly added .cs file in one of its test projects and the merge base's baseline is usable.
    private static List<(string Project, string Mode, string Why)>? PlanProjects(List<string> projects, List<string> activeAtBase,
        Dictionary<string, List<string>> testDirectories, List<(char Status, string Path)> changed, Func<string, string?> baselineProblem)
    {
        if (projects.Count == 0 || (projects.All(activeAtBase.Contains) && !changed.Any(c => MutationRelevant(c.Path, testDirectories))))
        {
            return null;
        }
        return [.. projects.Select(project =>
        {
            if (!activeAtBase.Contains(project))
            {
                return (project, FullMode, "inactive at the merge base, active now");
            }
            var triggers = changed.Where(c => CanChangeResultsOf(project, testDirectories[project], c.Path)).ToList();
            if (triggers.Count == 0)
            {
                return (project, SinceMode, "no test or build input changed");
            }
            var other = triggers.FirstOrDefault(t => !(t.Status == 'A' && t.Path.EndsWith(".cs", StringComparison.Ordinal)
                && testDirectories[project].Any(d => t.Path.StartsWith(d, StringComparison.Ordinal))));
            if (other.Path is { } path)
            {
                return (project, FullMode, $"{path} ({other.Status}) can change its results without changing a mutant");
            }
            return baselineProblem(project) is { } problem
                ? (project, FullMode, $"only added test files, but {problem}")
                : (project, BaselineMode, $"only added test files: {string.Join(", ", triggers.Select(t => t.Path))}");
        })];
    }

    // "<index>/<count>" with 1 <= index <= count, else null.
    private static (int Index, int Count)? ParseShard(string? text) =>
        text?.Split('/') is [var i, var n]
            && int.TryParse(i, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            && int.TryParse(n, NumberStyles.None, CultureInfo.InvariantCulture, out var count)
            && index >= 1 && index <= count
            ? (index, count)
            : null;

    // Disjoint, complete and deterministic: heaviest file first (path breaks ties) onto the lightest shard (lowest index
    // breaks ties), the weight being the line count, a proxy for the mutant count.
    private static List<List<string>> PartitionFiles(List<(string Path, int Weight)> files, int count)
    {
        var shards = Enumerable.Range(0, count).Select(_ => new List<string>()).ToList();
        var weights = new long[count];
        foreach (var (path, weight) in files.OrderByDescending(f => f.Weight).ThenBy(f => f.Path, StringComparer.Ordinal))
        {
            var lightest = Array.IndexOf(weights, weights.Min());
            shards[lightest].Add(path);
            weights[lightest] += weight;
        }
        shards.ForEach(s => s.Sort(StringComparer.Ordinal));
        return shards;
    }

    // One project's report from its shards, or null with the reasons added to failures: every shard 1..count reported once,
    // for this target, merge base and an allowed mode, without failures; the partitions are disjoint and cover exactly the
    // project's files; each file comes from the shard that owns it, and no mutant of it was dropped by the mutate filter.
    private static JsonObject? MergeShards(string target, string project, int count, List<string> files, string[] modes, string? mergeBase,
        string commit, List<JsonObject> shards, List<string> failures)
    {
        var violations = new List<string>();
        var mine = shards.Where(s => (string?)s["project"] == project).OrderBy(s => (int?)s["index"]).ToList();
        for (var index = 1; index <= count; index++)
        {
            var reported = mine.Count(s => (int?)s["index"] == index);
            if (reported != 1)
            {
                violations.Add($"{project}: shard {index}/{count} reported {(reported == 0 ? "no result" : $"{reported} results")}");
            }
        }
        var owners = new Dictionary<string, int>(StringComparer.Ordinal);
        var merged = new JsonObject();
        var tests = new JsonObject();
        JsonObject? root = null;
        foreach (var shard in mine)
        {
            var index = (int?)shard["index"] ?? 0;
            if ((int?)shard["count"] != count)
            {
                violations.Add($"{project}: shard {index} ran as one of {shard["count"]} shards, not {count}");
            }
            if ((string?)shard["target"] != target)
            {
                violations.Add($"{project}: shard {index} ran {shard["target"]}, not {target}");
            }
            if ((string?)shard["commit"] != commit)
            {
                violations.Add($"{project}: shard {index} ran on commit {shard["commit"]}, not {commit}");
            }
            if ((string?)shard["mergeBase"] != mergeBase)
            {
                violations.Add($"{project}: shard {index} used the merge base {shard["mergeBase"]}, not {mergeBase}");
            }
            if (!modes.Contains((string?)shard["mode"]))
            {
                violations.Add($"{project}: shard {index} ran in mode {shard["mode"]}; the plan allows [{string.Join(", ", modes)}]");
            }
            violations.AddRange((shard["failures"] as JsonArray ?? []).Select(f => $"{project}: shard {index}: {f}"));
            var partition = (shard["files"] as JsonArray ?? []).Select(f => (string)f!).ToList();
            foreach (var file in partition)
            {
                if (!owners.TryAdd(file, index))
                {
                    violations.Add($"{project}: {file} is in shards {owners[file]} and {index}");
                }
            }
            if (shard["report"] is not JsonObject { } report || report["files"] is not JsonObject reported)
            {
                violations.Add($"{project}: shard {index} has no report");
                continue;
            }
            root ??= report;
            foreach (var (file, entry) in reported)
            {
                if (!files.Contains(file))
                {
                    violations.Add($"{project}: Stryker reported {file}, which is in no partition");
                }
                else if (partition.Contains(file) && !merged.ContainsKey(file))
                {
                    merged[file] = entry!.DeepClone();
                }
            }
            foreach (var (id, test) in report["testFiles"] as JsonObject ?? [])
            {
                tests[id] = tests[id] ?? test?.DeepClone();
            }
        }
        violations.AddRange(files.Where(f => !owners.ContainsKey(f)).Select(f => $"{project}: {f} is in no shard"));
        violations.AddRange(owners.Keys.Except(files).Select(f => $"{project}: {f} is in a shard but not in the project"));
        // Stryker lists every project file, mutant-free and --since runs included (spike M0-15): a missing one is a broken run.
        violations.AddRange(owners.Where(o => files.Contains(o.Key) && !merged.ContainsKey(o.Key))
            .Select(o => $"{project}: {o.Key} is in shard {o.Value}'s partition but missing from its report"));
        violations.AddRange(from file in merged
                            let removed = file.Value!["mutants"]!.AsArray().Count(m => (string?)m!["statusReason"] == MutateFilterReason)
                            where removed > 0
                            select $"{project}: {file.Key}: {removed} mutant(s) of the shard's own file were removed by the mutate filter: its --mutate pattern did not match it");
        if (violations.Count > 0)
        {
            failures.AddRange(violations);
            return null;
        }
        var result = (JsonObject)root!.DeepClone();
        result["files"] = merged;
        result["testFiles"] = tests;
        return result;
    }

    // The baseline MutationPr may reuse: the stored report of exactly the merge base.
    private static (JsonObject? Report, string? Problem) ParseBaseline(string? text, string mergeBase)
    {
        if (text is null)
        {
            return (null, "no baseline report was restored for the merge base");
        }
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            root = null;
        }
        if (root is not JsonObject { } stored || stored["report"] is not JsonObject { } report || report["files"] is not JsonObject)
        {
            return (null, "the baseline report is unreadable");
        }
        var unfinished = (from file in (JsonObject)report["files"]!
                          from mutant in file.Value?["mutants"] as JsonArray ?? []
                          let status = (string?)mutant?["status"]
                          where !FinalStatuses.Contains(status)
                          select $"{file.Key} has a {status ?? "status-less"} mutant").FirstOrDefault();
        if (unfinished is not null)
        {
            return (null, $"the baseline report is incomplete: {unfinished}");
        }
        var commit = stored["commit"] is JsonValue value && value.TryGetValue<string>(out var c) ? c : null;
        return commit == mergeBase ? (report, null) : (null, $"the baseline report is stale (commit {commit}, merge base {mergeBase})");
    }

    // What a baseline run hands Stryker: a file changed since the merge base, new, edited or with mutants the key cannot tell
    // apart reruns whole; in an unchanged file each Survived, NoCoverage or Timeout mutant reruns by its character span
    // (Stryker 4.16 runs exactly the mutants whose span lies inside a "File.cs{start..end}" pattern; a line span is not
    // supported, spike in docs/spec/spikes.md, M0-15), which may rerun mutants nested in it too.
    private static RerunPlan PlanRerun(string project, JsonObject baselineFiles, List<string> scope, Func<string, string?> currentText, List<string> changed)
    {
        var plan = new RerunPlan([], [], []);
        var prefix = $"src/{project}/";
        foreach (var file in scope)
        {
            var text = currentText(file);
            var entry = baselineFiles[file] as JsonObject;
            var mutants = (entry?["mutants"] as JsonArray ?? []).Select(m => m!).ToList();
            var keys = mutants.Select(MutantKey).ToList();
            // ponytail: offsets count lines on \n (\r\n included); a lone \r or a Unicode line separator reruns the file whole.
            if (changed.Contains(file) || text is null || (string?)entry?["source"] != text || keys.Distinct().Count() != keys.Count
                || text.Replace("\r\n", "\n", StringComparison.Ordinal).AsSpan().IndexOfAny('\r', '\u0085', '\u2028') >= 0 || text.Contains('\u2029', StringComparison.Ordinal))
            {
                plan.WholeFiles.Add(file);
                plan.Filters.Add(file[prefix.Length..]);
                continue;
            }
            var lineStarts = new List<int> { 0 };
            lineStarts.AddRange(text.Select((ch, i) => (ch, i)).Where(c => c.ch == '\n').Select(c => c.i + 1));
            int Offset(JsonNode position) => lineStarts[(int)position["line"]! - 1] + (int)position["column"]! - 1;
            foreach (var mutant in mutants.Where(m => RerunStatuses.Contains((string?)m["status"])))
            {
                var filter = $"{file[prefix.Length..]}{{{Offset(mutant["location"]!["start"]!)}..{Offset(mutant["location"]!["end"]!)}}}";
                if (!plan.Filters.Contains(filter))
                {
                    plan.Filters.Add(filter);
                }
                if (!plan.Targets.TryGetValue(file, out var targets))
                {
                    plan.Targets[file] = targets = [];
                }
                targets.Add(MutantKey(mutant));
            }
        }
        return plan;
    }

    // A mutant's identity across two runs of the same file: mutator, replacement and location.
    private static string MutantKey(JsonNode mutant)
    {
        var location = mutant["location"]!;
        return $"{mutant["mutatorName"]}|{mutant["replacement"]}|{location["start"]!["line"]}:{location["start"]!["column"]}-{location["end"]!["line"]}:{location["end"]!["column"]}";
    }

    // The baseline with the rerun's results for every file rerun whole and every targeted mutant; a target the rerun did not
    // test, or a whole file the mutate filter missed, is a failure (never the baseline's stale result).
    private static JsonObject MergeBaseline(JsonObject baseline, JsonObject rerun, List<string> scope, RerunPlan plan, List<string> failures)
    {
        var files = new JsonObject();
        foreach (var file in scope)
        {
            var fresh = rerun["files"]?[file] as JsonObject;
            if (plan.WholeFiles.Contains(file))
            {
                var removed = (fresh?["mutants"] as JsonArray ?? []).Count(m => (string?)m!["statusReason"] == MutateFilterReason);
                if (removed > 0)
                {
                    failures.Add($"{file}: {removed} mutant(s) of a file rerun whole were removed by the mutate filter: its --mutate pattern did not match it");
                }
                if (fresh is null)
                {
                    failures.Add($"{file}: rerun whole but missing from the rerun report");
                }
                else
                {
                    files[file] = fresh.DeepClone();
                }
                continue;
            }
            if (baseline["files"]?[file] is not JsonObject old)
            {
                continue;
            }
            var merged = (JsonObject)old.DeepClone();
            var targets = plan.Targets.GetValueOrDefault(file) ?? [];
            var results = (fresh?["mutants"] as JsonArray ?? [])
                .Where(m => (string?)m!["statusReason"] != MutateFilterReason)
                .GroupBy(m => MutantKey(m!))
                .ToDictionary(g => g.Key, g => g.First()!);
            var mutants = merged["mutants"]!.AsArray();
            for (var i = 0; i < mutants.Count; i++)
            {
                var key = MutantKey(mutants[i]!);
                if (!targets.Contains(key))
                {
                    continue;
                }
                var start = mutants[i]!["location"]!["start"]!;
                if (results.TryGetValue(key, out var result) && TestedStatuses.Contains((string?)result["status"]))
                {
                    mutants[i] = result.DeepClone();
                }
                else if (result is not null)
                {
                    failures.Add($"{file}: the baseline's {mutants[i]!["mutatorName"]} mutant at {start["line"]}:{start["column"]} came back {result["status"]} from the rerun");
                }
                else
                {
                    failures.Add($"{file}: the baseline's {mutants[i]!["mutatorName"]} mutant at {start["line"]}:{start["column"]} was not rerun (no result with its mutator, replacement and location)");
                }
            }
            files[file] = merged;
        }
        var root = (JsonObject)baseline.DeepClone();
        root["files"] = files;
        if (root["testFiles"] is not JsonObject tests)
        {
            root["testFiles"] = tests = [];
        }
        foreach (var (id, test) in rerun["testFiles"] as JsonObject ?? [])
        {
            tests[id] = test?.DeepClone();
        }
        return root;
    }

    // §17.8: below the 90% target the nightly opens an issue.
    private static bool OpensScoreIssue(double? score) => score < TargetScore;

    // The hand-written mutation.yml and nightly-mutation.yml (§20.1): the aggregate job keeps the required check name, runs
    // after every shard whatever their result (a skipped job would pass the check), each job runs exactly its build command
    // with nothing that turns a failure into a success, and tokens are least-privilege (only the nightly aggregate writes
    // issues). A missing, failed or miscounted shard fails the aggregate itself, so the matrix needs no further check here.
    private static IEnumerable<string> MutationWorkflowViolations(string file, string workflow)
    {
        var pr = file == "mutation.yml";
        var aggregate = pr ? "mutation" : "nightly-mutation";
        var shard = $"{aggregate}-shard";
        (string Job, string Run, string[] Permissions)[] expected = pr
            ?
            [
                ("mutation-plan", "./build.sh MutationPlan", []),
                (shard, "./build.sh MutationPr --shard ${{ matrix.shard }}/<n>", []),
                (aggregate, "./build.sh MutationPrAggregate --shards <n>", []),
            ]
            :
            [
                (shard, "./build.sh Mutation --shard ${{ matrix.shard }}/<n>", []),
                (aggregate, "./build.sh MutationAggregate --shards <n>", ["actions: read", "contents: read", "issues: write"]),
            ];
        string[] triggers = pr ? ["pull_request"] : ["push", "schedule"];
        static string Key(string entry) => entry.Split(':')[0];

        var stream = new YamlStream();
        stream.Load(new StringReader(workflow));
        var root = stream.Documents.FirstOrDefault()?.RootNode;
        var on = YamlScalars(YamlAt(root, "on")).Select(Key).Order(StringComparer.Ordinal).ToList();
        if (!on.SequenceEqual(triggers))
        {
            yield return $"{file} must run on [{string.Join(", ", triggers)}] only, runs on [{string.Join(", ", on)}]";
        }
        if (!YamlScalars(YamlAt(root, "permissions")).SequenceEqual(["contents: read"]))
        {
            yield return $"{file}: permissions must be [contents: read] (a job that needs more asks for it)";
        }
        var jobs = YamlScalars(YamlAt(root, "jobs")).Select(Key).Order(StringComparer.Ordinal).ToList();
        if (!jobs.SequenceEqual(expected.Select(e => e.Job).Order(StringComparer.Ordinal)))
        {
            yield return $"{file} must have exactly the jobs [{string.Join(", ", expected.Select(e => e.Job))}], has [{string.Join(", ", jobs)}]";
        }
        foreach (var (job, run, permissions) in expected.Where(e => YamlAt(root, "jobs", e.Job) is not null))
        {
            var name = job == shard ? $"{job}-${{{{ matrix.shard }}}}" : job;
            if (YamlScalars(YamlAt(root, "jobs", job, "name")) is not [var actual] || actual != name)
            {
                yield return $"{file}: job '{job}' must be named '{name}' (the required check and unique job names, §20.2)";
            }
            var steps = (YamlAt(root, "jobs", job, "steps") as YamlSequenceNode)?.Children ?? [];
            var builds = steps.SelectMany(s => YamlScalars(YamlAt(s, "run"))).SelectMany(r => r.Split('\n')).Select(l => l.Trim())
                .Where(l => l.StartsWith("./build.sh", StringComparison.Ordinal)).ToList();
            var command = new Regex($"^{Regex.Escape(run).Replace("<n>", "[1-9][0-9]*", StringComparison.Ordinal)}$");
            if (builds is not [var build] || !command.IsMatch(build))
            {
                yield return $"{file}: {job}: must run exactly {run} (no --skip, no other build invocation), runs [{string.Join("; ", builds)}]";
            }
            var granted = YamlScalars(YamlAt(root, "jobs", job, "permissions")).Order(StringComparer.Ordinal).ToList();
            if (!granted.SequenceEqual(permissions))
            {
                yield return $"{file}: {job}: permissions must be [{string.Join(", ", permissions)}]{(permissions.Length == 0 ? " (none: it keeps the workflow's contents: read)" : "")}, has [{string.Join(", ", granted)}]";
            }
            if (YamlAt(root, "jobs", job, "continue-on-error") is not null || steps.Any(s => YamlAt(s, "continue-on-error") is not null))
            {
                yield return $"{file}: {job}: must not set continue-on-error (a failing run would count as a success)";
            }
            if (steps.Any(s => YamlAt(s, "if") is not null && YamlScalars(YamlAt(s, "run")).Any(r => r.Contains("./build.sh", StringComparison.Ordinal))))
            {
                yield return $"{file}: {job}: the ./build.sh step must have no if (a skipped step passes)";
            }
        }
        if (YamlAt(root, "jobs", aggregate) is not null)
        {
            if (!YamlScalars(YamlAt(root, "jobs", aggregate, "needs")).Contains(shard))
            {
                yield return $"{file}: {aggregate}: needs must hold {shard} (it merges every shard's report)";
            }
            if (YamlScalars(YamlAt(root, "jobs", aggregate, "if")) is not ["always()"])
            {
                yield return $"{file}: {aggregate}: if must be always(): after a failed shard a skipped {aggregate} would pass the check";
            }
        }
    }

    // The acceptance checks of M0-15, planted: the skip predicate per trigger class, the plan modes and the baseline
    // eligibility rule, the partitioner, the aggregator (missing shard, overlap, zero mutants, scores at 84.99/85/90), the
    // baseline rerun, and the two hand-written workflows.
    private static List<string> MutationSelfCheck()
    {
        var failures = new List<string>();
        void Expect(bool condition, string message)
        {
            if (!condition)
            {
                failures.Add($"MutationSelfCheck: {message}");
            }
        }
        static string Show<T>(IEnumerable<T>? items) => items is null ? "null" : $"[{string.Join("; ", items)}]";

        // Skip predicate and plan: Ducky active at the base and now, its test projects as in src/Ducky/stryker-config.json.
        var directories = new Dictionary<string, List<string>> { ["Ducky"] = ["test/Ducky.Concurrency.Tests/", "test/Ducky.Tests/"] };
        List<(string Project, string Mode, string Why)>? Plan(string? baselineProblem, params (char Status, string Path)[] changed) =>
            PlanProjects(["Ducky"], ["Ducky"], directories, [.. changed], _ => baselineProblem);
        var irrelevant = new (char, string)[]
        {
            ('M', "docs/spec/SPEC.md"), ('M', "docs/spec/tests.yaml"), ('M', ".github/workflows/ci.yml"), ('M', "build/Build.Gates.cs"),
            ('A', "spikes/S-6-mutation/Plain/Budget.cs"), ('M', "CHANGELOG.md"), ('M', "test/Ducky.Blazor.Tests/SomeTests.cs"),
        };
        Expect(Plan(null, irrelevant) is null, $"a PR changing only {Show(irrelevant)} must skip, planned {Show(Plan(null, irrelevant))}");
        Expect(Plan(null) is null, "an empty diff must skip");
        foreach (var trigger in new[]
                 {
                     "src/Ducky/Dispatcher.cs", "src/Ducky.Draft/Draft.cs", "src/Ducky/Ducky.csproj", "src/Ducky/stryker-config.json",
                     "test/Ducky.Tests/DispatchTests.cs", "test/Ducky.Concurrency.Tests/RaceTests.cs", "test/Shared/Interleaving.cs",
                     "test/BannedSymbols.txt", "Directory.Build.props", "Directory.Packages.props", "src/Directory.Build.targets",
                     "test/Directory.Build.props", "global.json", ".config/dotnet-tools.json", "build/property-seeds.txt",
                     "build/Build.Mutation.cs", "build/Build.MutationShards.cs", ".github/workflows/mutation.yml",
                 })
        {
            Expect(Plan(null, [.. irrelevant, ('M', trigger)]) is not null, $"a change to {trigger} must not skip");
            Expect(Plan(null, [.. irrelevant, ('D', trigger)]) is not null, $"deleting {trigger} must not skip");
        }
        Expect(PlanProjects(["Ducky"], [], directories, [], _ => null) is [("Ducky", FullMode, _)],
            $"a project active now but not at the merge base must get a full run, planned {Show(PlanProjects(["Ducky"], [], directories, [], _ => null))}");
        Expect(PlanProjects([], [], directories, [('M', "src/Ducky/Dispatcher.cs")], _ => null) is null, "no active project must skip");
        (string Case, string? BaselineProblem, (char, string)[] Changed, string Mode)[] modes =
        [
            ("a src change", null, [('M', "src/Ducky/Dispatcher.cs")], SinceMode),
            ("a mutation build change", null, [('M', "build/Build.Mutation.cs")], SinceMode),
            ("a modified test", null, [('M', "test/Ducky.Tests/DispatchTests.cs")], FullMode),
            ("an added test file", null, [('A', "test/Ducky.Tests/NewTests.cs"), ('M', "src/Ducky/Dispatcher.cs")], BaselineMode),
            ("an added concurrency test file", null, [('A', "test/Ducky.Concurrency.Tests/NewRaceTests.cs")], BaselineMode),
            ("an added test file without a usable baseline", "stale", [('A', "test/Ducky.Tests/NewTests.cs")], FullMode),
            ("an added and a modified test", null, [('A', "test/Ducky.Tests/NewTests.cs"), ('M', "test/Ducky.Tests/OldTests.cs")], FullMode),
            ("an added test and a deleted one", null, [('A', "test/Ducky.Tests/NewTests.cs"), ('D', "test/Ducky.Tests/OldTests.cs")], FullMode),
            ("a renamed test (--no-renames: delete and add)", null, [('D', "test/Ducky.Tests/A.cs"), ('A', "test/Ducky.Tests/B.cs")], FullMode),
            ("an added non-C# file in a test project", null, [('A', "test/Ducky.Tests/xunit.runner.json")], FullMode),
            ("an added shared helper", null, [('A', "test/Shared/NewHelper.cs")], FullMode),
            ("an added test and a build input", null, [('A', "test/Ducky.Tests/NewTests.cs"), ('M', "Directory.Build.props")], FullMode),
            ("an added test and a type change", null, [('A', "test/Ducky.Tests/NewTests.cs"), ('T', "test/Ducky.Tests/Link.cs")], FullMode),
        ];
        foreach (var (modeCase, problem, changed, mode) in modes)
        {
            var plan = Plan(problem, changed);
            Expect(plan is [(_, var planned, _)] && planned == mode, $"{modeCase} must plan '{mode}', planned {Show(plan)}");
        }

        // Shards.
        Expect(ParseShard("2/4") == (2, 4) && ParseShard("1/1") == (1, 1) && ParseShard(null) is null, "--shard 2/4 and 1/1 must parse");
        foreach (var bad in new[] { "0/4", "5/4", "4", "a/b", "-1/4", "1/0", "" })
        {
            Expect(ParseShard(bad) is null, $"--shard '{bad}' must be rejected");
        }
        List<(string, int)> sized = [("src/P/A.cs", 10), ("src/P/B.cs", 9), ("src/P/C.cs", 8), ("src/P/D.cs", 1), ("src/P/E.cs", 1), ("src/P/F.cs", 1), ("src/P/Sub/G.cs", 4)];
        var partition = PartitionFiles(sized, 3);
        var shuffled = PartitionFiles([.. sized.AsEnumerable().Reverse()], 3);
        Expect(partition.Count == 3 && partition.Zip(shuffled).All(p => p.First.SequenceEqual(p.Second)),
            $"the partition must not depend on input order: {Show(partition.Select(Show))} vs {Show(shuffled.Select(Show))}");
        var all = partition.SelectMany(p => p).ToList();
        Expect(all.Count == sized.Count && all.ToHashSet().SetEquals(sized.Select(s => s.Item1)), $"the partition must be disjoint and complete: {Show(partition.Select(Show))}");
        var totals = partition.Select(p => p.Sum(f => sized.First(s => s.Item1 == f).Item2)).ToList();
        Expect(totals.Count == 3 && totals.Max() - totals.Min() <= 10, $"the partition must balance by weight, totals {Show(totals)}");
        Expect(PartitionFiles(sized, 1) is [var whole] && whole.Count == sized.Count, "one shard must hold every file");
        var sparse = PartitionFiles([("src/P/A.cs", 3), ("src/P/B.cs", 2)], 4);
        Expect(sparse.Count == 4 && sparse.Count(p => p.Count == 0) == 2, $"two files over four shards must leave two empty shards, got {Show(sparse.Select(Show))}");

        // Reports: one mutant per line, a unique killing test per mutant so the always-failing check stays quiet.
        var nextTest = 0;
        JsonObject Mutant(string status, int line, string? reason = null, string mutator = "Equality") => new()
        {
            ["id"] = $"{line}{mutator}", ["mutatorName"] = mutator, ["replacement"] = "x", ["status"] = status, ["statusReason"] = reason,
            ["location"] = new JsonObject { ["start"] = new JsonObject { ["line"] = line, ["column"] = 5 }, ["end"] = new JsonObject { ["line"] = line, ["column"] = 7 } },
            ["killedBy"] = status == "Killed" ? new JsonArray($"t{nextTest++}") : new JsonArray(),
        };
        static string Source(int lines) => string.Concat(Enumerable.Range(1, lines).Select(i => $"// {i:D3}\n"));
        static JsonObject Report(params (string File, JsonObject[] Mutants)[] files) => new()
        {
            ["schemaVersion"] = "2",
            ["files"] = new JsonObject(files.Select(f => KeyValuePair.Create(f.File, (JsonNode?)new JsonObject
            {
                ["language"] = "cs", ["source"] = Source(Math.Max(1, f.Mutants.Length)), ["mutants"] = new JsonArray([.. f.Mutants]),
            }))),
            ["testFiles"] = new JsonObject(),
        };
        JsonObject[] Mutants(int killed, int survived) =>
            [.. Enumerable.Range(1, killed).Select(i => Mutant("Killed", i)), .. Enumerable.Range(killed + 1, survived).Select(i => Mutant("Survived", i))];
        static JsonObject Shard(string target, int index, int count, string mode, string[] files, JsonObject? report, string? mergeBase = "base", params string[] shardFailures) => new()
        {
            ["index"] = index, ["count"] = count, ["target"] = target, ["project"] = "P", ["commit"] = "head", ["mergeBase"] = mergeBase, ["mode"] = mode,
            ["files"] = new JsonArray([.. files.Select(f => (JsonNode)f)]), ["failures"] = new JsonArray([.. shardFailures.Select(f => (JsonNode)f)]), ["report"] = report,
        };
        List<string> projectFiles = ["src/P/A.cs", "src/P/B.cs", "src/P/C.cs"];
        List<JsonObject> Shards(Action<List<JsonObject>>? plant = null)
        {
            List<JsonObject> shards =
            [
                Shard("MutationPr", 1, 2, FullMode, ["src/P/A.cs", "src/P/C.cs"], Report(("src/P/A.cs", Mutants(9, 1)), ("src/P/C.cs", Mutants(4, 0)), ("src/P/B.cs", [Mutant("Ignored", 1, "Removed by mutate filter")]))),
                Shard("MutationPr", 2, 2, FullMode, ["src/P/B.cs"], Report(("src/P/B.cs", Mutants(5, 1)), ("src/P/A.cs", [Mutant("Ignored", 1, "Removed by mutate filter")]))),
            ];
            plant?.Invoke(shards);
            return shards;
        }
        (JsonObject? Merged, List<string> Violations) Merge(List<JsonObject> shards, string[]? allowed = null, int count = 2)
        {
            var violations = new List<string>();
            return (MergeShards("MutationPr", "P", count, projectFiles, allowed ?? [FullMode, BaselineMode], "base", "head", shards, violations), violations);
        }
        var (merged, mergeViolations) = Merge(Shards());
        var mergedScoreFailures = new List<string>();
        var mergedScore = merged is null ? null : CheckReport("P", null, merged, null, mergedScoreFailures, "report");
        Expect(mergeViolations.Count == 0 && mergedScore is { } s1 && Math.Abs(s1 - (100.0 * 18 / 20)) < 1e-9 && mergedScoreFailures.Count == 0,
            $"two complete shards must merge to 18/20 = 90%, got {mergedScore} with {Show(mergeViolations.Concat(mergedScoreFailures))}");
        Expect(merged?["files"] is JsonObject mergedFiles && mergedFiles.Count == 3 && mergedFiles.All(f => f.Value!["mutants"]!.AsArray().All(m => (string?)m!["status"] != "Ignored")),
            "the merged report must take each file from the shard that owns it");
        (string Case, List<JsonObject> Shards, string[]? Allowed, int Count, string Expected)[] plantedShards =
        [
            ("a missing shard", Shards(s => s.RemoveAt(1)), null, 2, "P: shard 2/2 reported no result"),
            ("a shard reported twice", Shards(s => s.Add(s[1])), null, 2, "P: shard 2/2 reported 2 results"),
            ("a third shard expected", Shards(), null, 3, "P: shard 3/3 reported no result"),
            ("a shard of another count", Shards(s => s[1]["count"] = 3), null, 2, "P: shard 2 ran as one of 3 shards, not 2"),
            ("a shard of another target", Shards(s => s[1]["target"] = "Mutation"), null, 2, "P: shard 2 ran Mutation, not MutationPr"),
            ("a shard of another merge base", Shards(s => s[1]["mergeBase"] = "other"), null, 2, "P: shard 2 used the merge base other, not base"),
            ("a shard of another commit", Shards(s => s[1]["commit"] = "other"), null, 2, "P: shard 2 ran on commit other, not head"),
            ("an owned file missing from its shard's report", Shards(s => s[0]["report"]!["files"]!.AsObject().Remove("src/P/C.cs")), null, 2,
                "P: src/P/C.cs is in shard 1's partition but missing from its report"),
            ("a shard in a mode the plan forbids", Shards(), [SinceMode], 2, "P: shard 1 ran in mode full; the plan allows [since]"),
            ("a failed shard", Shards(s => s[1]["failures"] = new JsonArray("P: Stryker exited with code 1")), null, 2, "P: shard 2: P: Stryker exited with code 1"),
            ("a shard without a report", Shards(s => s[1]["report"] = null), null, 2, "P: shard 2 has no report"),
            ("an overlapping partition", Shards(s => s[1]["files"] = new JsonArray("src/P/B.cs", "src/P/C.cs")), null, 2, "P: src/P/C.cs is in shards 1 and 2"),
            ("a file in no partition", Shards(s => s[1]["files"] = new JsonArray()), null, 2, "P: src/P/B.cs is in no shard"),
            ("a partition naming an unknown file", Shards(s => s[1]["files"] = new JsonArray("src/P/B.cs", "src/P/Gone.cs")), null, 2, "P: src/P/Gone.cs is in a shard but not in the project"),
            ("a report file outside the project", Shards(s => s[0]["report"]!["files"]!.AsObject()["src/P/Other.cs"] = new JsonObject { ["source"] = "", ["mutants"] = new JsonArray() }), null, 2,
                "P: Stryker reported src/P/Other.cs, which is in no partition"),
            ("a mutate filter that matched nothing", Shards(s => s[1]["report"] = Report(("src/P/B.cs", [Mutant("Ignored", 1, "Removed by mutate filter")]))), null, 2,
                "P: src/P/B.cs: 1 mutant(s) of the shard's own file were removed by the mutate filter"),
        ];
        foreach (var (shardCase, shards, allowed, count, expected) in plantedShards)
        {
            var (plantedMerge, violations) = Merge(shards, allowed, count);
            Expect(plantedMerge is null && violations.Any(v => v.Contains(expected, StringComparison.Ordinal)), $"{shardCase} must fail with '{expected}', got {Show(violations)}");
        }

        // The rules on the merged report: break below 85, a full run fails on zero mutants or no score, a --since run may have none.
        foreach (var (killed, survived, breaks) in new[] { (8499, 1501, true), (85, 15, false), (90, 10, false) })
        {
            var scoreFailures = new List<string>();
            var score = CheckReport("P", null, Report(("src/P/A.cs", Mutants(killed, survived))), null, scoreFailures, "report");
            Expect(scoreFailures.Any(f => f.Contains("below the break threshold", StringComparison.Ordinal)) == breaks,
                $"a score of {score:F2} must {(breaks ? "" : "not ")}break, got {Show(scoreFailures)}");
            Expect(OpensScoreIssue(score) == (score < 90), $"a score of {score:F2} must {(score < 90 ? "" : "not ")}open the below-target issue");
        }
        var zeroFull = new List<string>();
        CheckReport("P", null, Report(("src/P/A.cs", [Mutant("Ignored", 1, "Removed by block already covered filter")])), null, zeroFull, "report");
        Expect(zeroFull.Any(f => f.Contains("zero mutants or no score", StringComparison.Ordinal)), $"a full run with zero mutants must fail, got {Show(zeroFull)}");
        var zeroSince = new List<string>();
        CheckReport("P", "base", Report(("src/P/A.cs", [])), [], zeroSince, "report");
        Expect(zeroSince.Count == 0, $"a --since run with zero mutants must pass, got {Show(zeroSince)}");

        // Baseline: eligibility of the stored report, then the rerun of an unchanged file's non-killed mutants by character span.
        const string baselineText = """{"commit":"base","project":"P","report":{"files":{}}}""";
        Expect(ParseBaseline(baselineText, "base") is ({ }, null), "a baseline of the merge base must be usable");
        const string crashedText = """{"commit":"base","report":{"files":{"src/P/A.cs":{"mutants":[{"status":"Killed"},{"status":"RuntimeError"},{"status":"CompileError"},{"status":"Ignored"}]}}}}""";
        Expect(ParseBaseline(crashedText, "base") is ({ }, null), $"a finished run's RuntimeError, CompileError and Ignored mutants must leave its baseline usable, got '{ParseBaseline(crashedText, "base").Problem}'");
        foreach (var (baselineCase, text, expected) in new[]
                 {
                     ("a missing baseline", (string?)null, "no baseline"), ("an unreadable baseline", "{not json", "unreadable"),
                     ("a baseline without files", """{"commit":"base","report":{}}""", "unreadable"), ("a stale baseline", baselineText.Replace("\"base\"", "\"older\"", StringComparison.Ordinal), "stale"),
                     ("a baseline from an aborted run", """{"commit":"base","report":{"files":{"src/P/A.cs":{"mutants":[{"status":"Killed"},{"status":"Pending"}]}}}}""",
                         "src/P/A.cs has a Pending mutant"),
                 })
        {
            var (report, problem) = ParseBaseline(text, "base");
            Expect(report is null && problem?.Contains(expected, StringComparison.Ordinal) == true, $"{baselineCase} must be refused as '{expected}', got '{problem}'");
        }
        const string unchangedSource = "class A\n{\n    bool F(int x) => x > 1;\n}\n";
        JsonObject Located(string status, int line, int start, int end, string mutator) => new()
        {
            ["id"] = $"{mutator}{line}{start}", ["mutatorName"] = mutator, ["replacement"] = "r", ["status"] = status, ["statusReason"] = null,
            ["location"] = new JsonObject { ["start"] = new JsonObject { ["line"] = line, ["column"] = start }, ["end"] = new JsonObject { ["line"] = line, ["column"] = end } },
            ["killedBy"] = status == "Killed" ? new JsonArray($"t{nextTest++}") : new JsonArray(),
        };
        JsonObject Entry(string source, params JsonObject[] mutants) => new() { ["language"] = "cs", ["source"] = source, ["mutants"] = new JsonArray([.. mutants]) };
        var baselineFiles = new JsonObject
        {
            ["src/P/A.cs"] = Entry(unchangedSource, Located("Survived", 3, 22, 27, "Equality"), Located("Killed", 3, 22, 27, "Negate"), Located("NoCoverage", 3, 26, 27, "Number")),
            ["src/P/B.cs"] = Entry("class B { }\n", Located("Killed", 1, 1, 12, "Block")),
            ["src/P/C.cs"] = Entry("class C { int Old; }\n", Located("Survived", 1, 11, 18, "Statement")),
            ["src/P/D.cs"] = Entry("class D { }\n"),
        };
        var current = new Dictionary<string, string>
        {
            ["src/P/A.cs"] = unchangedSource, ["src/P/B.cs"] = "class B { }\n", ["src/P/C.cs"] = "class C { int New; }\n", ["src/P/D.cs"] = "class D { }\n", ["src/P/E.cs"] = "class E { }\n",
        };
        List<string> scope = ["src/P/A.cs", "src/P/B.cs", "src/P/C.cs", "src/P/D.cs", "src/P/E.cs"];
        var rerun = PlanRerun("P", baselineFiles, scope, f => current.GetValueOrDefault(f), ["src/P/D.cs"]);
        // "    bool F(int x) => x > 1;" starts at offset 10: columns 22..27 are offsets 31..36 ("x > 1"), 26..27 are 35..36 ("1").
        Expect(rerun.Filters.Order(StringComparer.Ordinal).SequenceEqual(["A.cs{31..36}", "A.cs{35..36}", "C.cs", "D.cs", "E.cs"]),
            $"the rerun must take A's survived and no-coverage mutants by span and C (edited), D (changed) and E (new) whole, got {Show(rerun.Filters)}");
        Expect(rerun.WholeFiles.SetEquals(["src/P/C.cs", "src/P/D.cs", "src/P/E.cs"]), $"C, D and E must rerun whole, got {Show(rerun.WholeFiles)}");
        var crlf = PlanRerun("P", new JsonObject { ["src/P/A.cs"] = Entry(unchangedSource.ReplaceLineEndings("\r\n"), Located("Survived", 3, 22, 27, "Equality")) },
            ["src/P/A.cs"], _ => unchangedSource.ReplaceLineEndings("\r\n"), []);
        Expect(crlf.Filters is ["A.cs{33..38}"], $"CRLF line ends must count in the offsets, got {Show(crlf.Filters)}");
        var nothing = PlanRerun("P", baselineFiles, ["src/P/B.cs"], f => current.GetValueOrDefault(f), []);
        Expect(nothing.Filters.Count == 0, $"an unchanged file without non-killed mutants must not rerun, got {Show(nothing.Filters)}");

        var rerunReport = new JsonObject
        {
            ["files"] = new JsonObject
            {
                ["src/P/A.cs"] = Entry(unchangedSource, Located("Killed", 3, 22, 27, "Equality"), Located("Killed", 3, 22, 27, "Negate"), Located("Survived", 3, 26, 27, "Number")),
                ["src/P/C.cs"] = Entry(current["src/P/C.cs"], Located("Killed", 1, 11, 18, "Statement")),
                ["src/P/D.cs"] = Entry(current["src/P/D.cs"]),
                ["src/P/E.cs"] = Entry(current["src/P/E.cs"], Located("Killed", 1, 1, 12, "Block")),
            },
            ["testFiles"] = new JsonObject(),
        };
        var rerunFailures = new List<string>();
        var combined = MergeBaseline(new JsonObject { ["files"] = baselineFiles.DeepClone(), ["testFiles"] = new JsonObject() }, rerunReport, scope, rerun, rerunFailures);
        static string Statuses(JsonNode? entry) => string.Join(",", entry?["mutants"]?.AsArray().Select(m => (string?)m!["status"]) ?? []);
        Expect(rerunFailures.Count == 0 && Statuses(combined["files"]?["src/P/A.cs"]) == "Killed,Killed,Survived" && Statuses(combined["files"]?["src/P/B.cs"]) == "Killed"
                && Statuses(combined["files"]?["src/P/C.cs"]) == "Killed" && Statuses(combined["files"]?["src/P/E.cs"]) == "Killed" && combined["files"]!.AsObject().Count == 5,
            $"the merge must take rerun results for the targets and whole files and the baseline for the rest, got {combined.ToJsonString()} {Show(rerunFailures)}");
        var lost = (JsonObject)rerunReport.DeepClone();
        lost["files"]!["src/P/A.cs"]!["mutants"]!.AsArray().RemoveAt(2);
        var lostFailures = new List<string>();
        MergeBaseline(new JsonObject { ["files"] = baselineFiles.DeepClone() }, lost, scope, rerun, lostFailures);
        Expect(lostFailures.Any(f => f.Contains("src/P/A.cs: the baseline's Number mutant at 3:26 was not rerun", StringComparison.Ordinal)),
            $"a target missing from the rerun must fail, got {Show(lostFailures)}");
        var filtered = (JsonObject)rerunReport.DeepClone();
        filtered["files"]!["src/P/E.cs"]!["mutants"]!.AsArray()[0]!["status"] = "Ignored";
        filtered["files"]!["src/P/E.cs"]!["mutants"]!.AsArray()[0]!["statusReason"] = "Removed by mutate filter";
        var filteredFailures = new List<string>();
        MergeBaseline(new JsonObject { ["files"] = baselineFiles.DeepClone() }, filtered, scope, rerun, filteredFailures);
        Expect(filteredFailures.Any(f => f.Contains("src/P/E.cs: 1 mutant(s) of a file rerun whole were removed by the mutate filter", StringComparison.Ordinal)),
            $"a whole-file rerun that the filter missed must fail, got {Show(filteredFailures)}");
        var absent = (JsonObject)rerunReport.DeepClone();
        absent["files"]!.AsObject().Remove("src/P/E.cs");
        var absentFailures = new List<string>();
        MergeBaseline(new JsonObject { ["files"] = baselineFiles.DeepClone() }, absent, scope, rerun, absentFailures);
        Expect(absentFailures.Any(f => f.Contains("src/P/E.cs: rerun whole but missing from the rerun report", StringComparison.Ordinal)),
            $"a whole-file rerun missing from the report must fail, got {Show(absentFailures)}");
        var untested = (JsonObject)rerunReport.DeepClone();
        var untestedMutants = untested["files"]!["src/P/A.cs"]!["mutants"]!.AsArray();
        untestedMutants[0]!["status"] = "CompileError";
        untestedMutants[2]!["status"] = "Ignored";
        untestedMutants[2]!["statusReason"] = "Removed by block already covered filter";
        var untestedFailures = new List<string>();
        MergeBaseline(new JsonObject { ["files"] = baselineFiles.DeepClone() }, untested, scope, rerun, untestedFailures);
        foreach (var expected in new[] { "src/P/A.cs: the baseline's Equality mutant at 3:22 came back CompileError from the rerun", "src/P/A.cs: the baseline's Number mutant at 3:26 came back Ignored from the rerun" })
        {
            Expect(untestedFailures.Any(f => f.Contains(expected, StringComparison.Ordinal)), $"a target the rerun did not test must fail with '{expected}', got {Show(untestedFailures)}");
        }

        // The two hand-written workflows.
        const string prWorkflow = """
            name: mutation
            on:
              pull_request:
                branches: [v2, main]
            permissions:
              contents: read
            jobs:
              mutation-plan:
                name: mutation-plan
                steps:
                  - run: ./build.sh MutationPlan
              mutation-shard:
                name: mutation-shard-${{ matrix.shard }}
                needs: mutation-plan
                if: needs.mutation-plan.outputs.run == 'true'
                strategy:
                  fail-fast: false
                  matrix:
                    shard: [1, 2, 3, 4]
                steps:
                  - run: ./build.sh MutationPr --shard ${{ matrix.shard }}/4
              mutation:
                name: mutation
                needs: mutation-shard
                if: always()
                steps:
                  - run: ./build.sh MutationPrAggregate --shards 4
            """;
        const string nightlyWorkflow = """
            name: nightly-mutation
            on:
              schedule:
                - cron: '0 2 * * *'
              push:
                branches: [v2]
            permissions:
              contents: read
            jobs:
              nightly-mutation-shard:
                name: nightly-mutation-shard-${{ matrix.shard }}
                strategy:
                  fail-fast: false
                  matrix:
                    shard: [1, 2, 3, 4]
                steps:
                  - run: ./build.sh Mutation --shard ${{ matrix.shard }}/4
              nightly-mutation:
                name: nightly-mutation
                needs: [nightly-mutation-shard]
                if: always()
                permissions:
                  contents: read
                  actions: read
                  issues: write
                steps:
                  - run: ./build.sh MutationAggregate --shards 4
            """;
        foreach (var (file, text) in new[] { ("mutation.yml", prWorkflow), ("nightly-mutation.yml", nightlyWorkflow) })
        {
            var unplanted = MutationWorkflowViolations(file, text).ToList();
            Expect(unplanted.Count == 0, $"the unplanted {file} must pass, got {Show(unplanted)}");
        }
        (string Case, string File, string From, string To, string Expected)[] workflows =
        [
            ("an aggregate that a failed shard skips", "mutation.yml", "    if: always()\n", "", "mutation.yml: mutation: if must be always()"),
            ("an aggregate that does not wait for the shards", "mutation.yml", "    needs: mutation-shard\n", "    needs: mutation-plan\n", "mutation.yml: mutation: needs must hold mutation-shard"),
            ("a shard that continues on error", "mutation.yml", "    needs: mutation-plan\n", "    needs: mutation-plan\n    continue-on-error: true\n", "mutation.yml: mutation-shard: must not set continue-on-error"),
            ("a skipped aggregate step", "mutation.yml", "      - run: ./build.sh MutationPrAggregate --shards 4", "      - run: ./build.sh MutationPrAggregate --shards 4\n        if: false",
                "mutation.yml: mutation: the ./build.sh step must have no if"),
            ("an aggregate skipping targets", "mutation.yml", "MutationPrAggregate --shards 4", "MutationPrAggregate --shards 4 --skip", "mutation.yml: mutation: must run exactly ./build.sh MutationPrAggregate --shards <n>"),
            ("shards running the nightly target", "mutation.yml", "./build.sh MutationPr --shard", "./build.sh Mutation --shard", "mutation.yml: mutation-shard: must run exactly ./build.sh MutationPr --shard"),
            ("a renamed required check", "mutation.yml", "    name: mutation\n", "    name: mutation-aggregate\n", "mutation.yml: job 'mutation' must be named 'mutation'"),
            ("a missing aggregate job", "mutation.yml", "  mutation:\n", "  mutation-merge:\n", "mutation.yml must have exactly the jobs"),
            ("a write token on a pull request", "mutation.yml", "  contents: read\n", "  contents: write\n", "mutation.yml: permissions must be [contents: read]"),
            ("job-level permissions on a pull request", "mutation.yml", "    name: mutation-plan\n", "    name: mutation-plan\n    permissions: write-all\n", "mutation.yml: mutation-plan: permissions must be"),
            ("a push trigger on the pull-request workflow", "mutation.yml", "on:\n", "on:\n  push:\n    branches: [v2]\n", "mutation.yml must run on [pull_request] only"),
            ("a nightly shard allowed to write issues", "nightly-mutation.yml", "    strategy:\n", "    permissions:\n      issues: write\n    strategy:\n", "nightly-mutation.yml: nightly-mutation-shard: permissions must be"),
            ("a nightly aggregate that cannot open issues", "nightly-mutation.yml", "      issues: write\n", "", "nightly-mutation.yml: nightly-mutation: permissions must be [actions: read, contents: read, issues: write]"),
            ("a nightly without its push trigger", "nightly-mutation.yml", "  push:\n    branches: [v2]\n", "", "nightly-mutation.yml must run on [push, schedule] only"),
            ("a nightly aggregate that a failed shard skips", "nightly-mutation.yml", "    if: always()\n", "    if: success()\n", "nightly-mutation.yml: nightly-mutation: if must be always()"),
        ];
        foreach (var (workflowCase, file, from, to, expected) in workflows)
        {
            var text = file == "mutation.yml" ? prWorkflow : nightlyWorkflow;
            Expect(text.Contains(from, StringComparison.Ordinal), $"{workflowCase}: the fixture lacks '{from}'");
            var violations = MutationWorkflowViolations(file, text.Replace(from, to, StringComparison.Ordinal)).ToList();
            Expect(violations.Any(v => v.Contains(expected, StringComparison.Ordinal)), $"{workflowCase} must report '{expected}', got {Show(violations)}");
        }
        return failures;
    }
}

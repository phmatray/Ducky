# Spike results (SPEC §23)

Week-1 prototype answers. Each prototype lives under `spikes/`, outside every solution and gate.

## S-2: coverage without exclusions (M0-04)

**Question.** Can the coverage tool reach 100% line and branch without exclusions, with the pinned xunit.v3/MTP
versions?

**Setup.** `spikes/S-2-coverage`, run 2026-10-01 on macOS arm64 with SDK 10.0.401 (global.json 10.0.100,
`latestFeature`), `xunit.v3.mtp-v2` 4.0.1, `Microsoft.Testing.Extensions.CodeCoverage` 18.11.2,
`Microsoft.CodeAnalysis.CSharp` 5.0.0 (the pins of `Directory.Packages.props`). Two runs: the tool's defaults with no
settings file, then `--coverage-settings ../../build/coverage.settings.xml` (commands in the spike's README).

**Answer: yes.** 8 tests pass, no crash. Without any settings file every probe shape is at 100% line and branch:
the only uncovered code is `Probe/Uncovered.g.cs`, a hand-named `*.g.cs` that no test calls on purpose (line 8, 0/2
branches). With the committed settings file that class is gone and both probe assemblies show `line-rate="1"
branch-rate="1"`; every other line has the same hits and branch counts in both runs.

**Choice: `Microsoft.Testing.Extensions.CodeCoverage`** (18.11.2, already pinned). The coverlet.MTP fallback is not
triggered and was not evaluated.

**Tool defaults the gate relies on** (measured in both runs, so passing `build/coverage.settings.xml` keeps them):

- Attribute exclusions merged by default: `DebuggerHiddenAttribute`, `DebuggerNonUserCodeAttribute`,
  `GeneratedCodeAttribute`, `ExcludeFromCodeCoverageAttribute` (the last is a banned API in `src/`, §17.1).
- Compiler-generated code is folded into the user method, not reported separately: async state machines report no
  branch for an await's `IsCompleted` check or for the `await using` null check and dispose. `SyncOnlyAsyncShapes.AddAsync`
  and `UseAsync` repeat those shapes and are called with completed tasks only (`Async_CompletedOnly_FullyCovered`):
  every line is covered and no branch is partial, so await suspension points never demand a pending-task test for
  coverage alone.
- Records count their declaration line (primary constructor and property initialisation); the synthesized `Equals`,
  `GetHashCode`, `PrintMembers`, `ToString`, `Deconstruct` and clone members add no line or branch.
- A lowered `lock` (on `System.Threading.Lock` and on an object with Monitor's `lockTaken` flag) adds no branch.
- Switch expressions report their arms as branches on the discriminating line; the default arm counts. An enum
  switch without a discard arm fails the build (CS8524 under warnings as errors), so its throwing default arm needs a
  test.
- `SkipAutoProperties` is `True` and the MTP extension leaves test assemblies out.
- A module without any sequence point (only a member-less interface such as the tombstone shape, or
  `public sealed class X : Attribute;`) is absent from the report rather than listed at 100%.

**Consequences for `CoverageGate` and `ExclusionGate`.**

- `build/coverage.settings.xml` is the one coverage settings file; its only content is the source exclusion
  `.*\.g\.cs$` (the tool takes ECMAScript regexes, so this is the `**/*.g.cs` of §17.1). `Uncovered.g.cs` proves it
  matches: present and uncovered without the file, absent with it. The `Test` target passes it
  with `--coverage-settings`, and `ExclusionGate` fails on any other element or attribute, or on a second settings
  file.
- A `src/` assembly of `Ducky.slnx` that is absent from the merged report counts as missing unless its Release PDB
  has no non-hidden sequence point outside `*.g.cs`, like the stage-1 skeletons of `Ducky.Draft`
  (`DraftableAttribute`) and `Ducky.Testing` (`DuckyAssertionException`), whose final §13.2/§15 declarations are
  code-less too. With nothing to cover they pass, and the gate logs them. A project without a PDB counts as having
  code. SPEC §17.1, §19 and §24 carry this rule.
- The path exclusion and the default `[GeneratedCode]` exclusion may only ever remove generated code, so
  `ExclusionGate` fails on a committed `*.g.cs` anywhere in the repository (a file outside `src/` can be linked into a
  `src/` project), and on a `[GeneratedCode]` type or method (read from the Release metadata, containing types
  included) with a sequence point outside `*.g.cs`. The tool applies the `<Source>` regex case-insensitively
  (`Uncovered.G.cs` is dropped from the report too), so the gate matches `*.g.cs` case-insensitively as well.
- `#line hidden` hides sequence points (the tool never reports them) and `#line 1 "Fake.g.cs"` remaps them onto the
  path exclusion, so `ExclusionGate` rejects any `#line` directive in `src/`.
- MTP writes each cobertura file twice, in `artifacts/test/` and in `artifacts/test/<machine>_<date>/In/<machine>/`.
  The `**/*.cobertura.xml` glob merges both copies: hit counts double, rates are unchanged.
- ReportGenerator writes `branch="true"` in lowercase; the gate reads `condition-coverage` instead.

Not measured here: Linux and Windows. CoverageGate runs there once the CI workflows arrive (M0-11).

## S-6: Stryker.NET with the MTP test runner (M0-05)

**Question.** Does Stryker.NET with `test-runner: mtp` report a score, with `--since` and with a full run, for a plain
library, a generator and a Razor SDK library shaped like `Ducky.Blazor`, with mutants > 0 per project? Does
`DUCKY_REPEAT` set on the Stryker process reach the test hosts? Does the boolean mutator mutate `ConfigureAwait(false)`?

**Setup.** `spikes/S-6-mutation`, run 2026-10-01 on macOS arm64 with SDK 10.0.401, `dotnet-stryker` 4.16.0 (the tool
manifest pin) and the pins of `Directory.Packages.props`. `Plain` (plain SDK, net10.0), `Gen` (netstandard2.0
incremental generator), `Web` (Razor SDK, browser platform, STJ and `[LoggerMessage]` generators, IVT to `Web.Tests` and
`Web.Concurrency.Tests`). Each `stryker-config.json` has the shape `ExclusionGate` pins for `src/`. `--since` runs were
made in a plain clone with one changed line per project (commands in the spike's README).

**Answer: yes, with two configuration consequences.**

| Project | Full run: mutants created / scored, score | `--since` run: scored, score |
|---|---|---|
| `Plain` | 20 / 13, 100% | 14, 100% |
| `Gen` | 51 / 35 (12 compile errors, Stryker's `Append`→`Prepend` on `StringBuilder`), 100% | 38, 94.74% (2 survivors, both in the untested planted line) |
| `Web` | 18 / 11, 100% | 14, 92.86% (1 survivor, the untested planted condition) |

- **`DUCKY_REPEAT` reaches the test hosts.** Stryker's environment is inherited by every MTP test host it starts. With
  `DUCKY_REPEAT=1` on the Stryker process, the concurrency test ran once per tested mutant plus once for the initial
  run (12 runs for the 11 mutants of the full `Web` run, 15 for the 14 of the `--since` run), each with `repeat=1`.
  The hosts get `STRYKER_MUTANT_FILE` (and `STRYKER_COVERAGE_FILE` for the coverage pass) and are reused across mutants.
- **`ConfigureAwait(false)` is mutated.** The first `Plain` run scored 82.35%: its three survivors were the `Boolean`
  mutants `false`→`true` on a `Task`, a `ValueTask` and an `await using` `ConfigureAwait(false)`, unobservable without
  a `SynchronizationContext`. `ConfigureAwait(ConfigureAwaitOptions.None)` produces no mutant, and CA2007 (raised as in
  `src/`) accepts it: removing it fails the build with CA2007, keeping it builds clean. So `src/` awaits a `Task` with
  `ConfigureAwait(ConfigureAwaitOptions.None)`, and only the `ValueTask` and `await using` lines carry
  `// Stryker disable once Boolean : ConfigureAwait is unobservable without a SynchronizationContext` (Stryker then
  reports them `Ignored` with that reason), as §17.8 planned.
- **Per-test coverage analysis is wrong with several test projects.** With the default `perTest` coverage analysis
  and both `Web` test projects, Stryker kept only the last test project's coverage: 4 mutants covered only by
  `Web.Tests` came back `NoCoverage` (66.67%). With one test project, or with `"coverage-analysis": "off"` (or `"all"`),
  the same mutants are tested and the score is right (83.33% before the fix). Every `stryker-config.json` therefore
  sets `"coverage-analysis": "off"`: every listed test runs against every mutant, which is what §17.8's test-project
  lists are for. The cost is run time (S-9 measures it); per-test analysis can return once the MTP runner merges
  coverage across test projects.
- **A test that fails on every run kills every mutant.** Stryker injects two helper types with static mutable fields
  into the mutated assembly, `Stryker<random>.MutantControl` and `Stryker<random>.MutantContext` (a per-run namespace
  of letters and digits). `NoStaticMutableFields` then fails in every run of that assembly, and the MTP runner flagged
  nothing: all five planted mutants of a `src/Ducky` check came back `Killed` (100%), three of them
  wrongly. The audit now exempts exactly those two type names in a `Stryker<letters and digits>` namespace, as it
  exempts the coverage tracker (§17.5), and the planted check then scores 40% and fails `MutationPr`. A zero-mutant
  project injects no helper, which is why the skeleton runs never showed it.
- **Generators.** The netstandard2.0 generator is mutated and scored like a library. In the Razor SDK project Stryker's
  embedded compiler cannot load the Razor source generator (`Microsoft.CodeAnalysis.Razor.Compiler` references compiler
  5.9, newer than Stryker's): harmless for `Ducky.Blazor`, which has no `.razor` files (§11.2), while the STJ and
  `[LoggerMessage]` generators run (their output compiles into every mutant).
- **`--since` scope.** Stryker rescored every mutant of a changed file, not only the changed lines ("Mutant changed
  compared to target commit"), so `MutationPr` scores the changed files. A diff that changes no file of a project
  reports zero mutants and no score, and Stryker exits 0. That includes a diff that only weakens tests: with
  `coverage-analysis: "off"` Stryker cannot map a changed test file to the mutants it covers (removing an assertion
  from a `Plain.Tests` file logged `Changed file …BudgetTests.cs`, then 18 mutants `Ignored` "Removed by since
  filter", exit 0). Stryker writes `mutation-report.json` even with zero mutants ("a mutant-free world"), and under
  the MTP runner every `Killed` mutant lists the ids of the tests that killed it in `killedBy` (`testFiles` carries
  no names, so the ids are Stryker's hashes).

**Consequences for the build (M0-05).**

- Every `stryker-config.json` holds exactly `test-runner: "mtp"`, the thresholds, `mutate: ["**/*.cs"]`,
  `test-projects`, `additional-timeout` (5000 ms) and `coverage-analysis: "off"`; `ExclusionGate` rejects any other key.
- In a linked git worktree (`.git` is a file) Stryker 4.16's `--since` read the main checkout's working tree and
  listed its files as changed (read-only; nothing was written there). CI clones are plain; elsewhere `MutationPr`
  makes a throwaway plain clone of `HEAD` in `artifacts/mutation/clone` (`git clone --no-local --single-branch`),
  commits the uncommitted changes (tracked and untracked) on top, fetches the base commit and runs Stryker there.
  `--since:HEAD` resolved to the remote `origin/HEAD` in a clone, so the target passes the base as a SHA
  (`git rev-parse`); locally `./build.sh MutationPr --base-ref HEAD` scores the uncommitted work.
- `MutationPr` gives a project a full run, with the zero-mutant check, when a changed path outside its
  `src/<project>/**/*.cs` can change its results (its test projects, `test/Shared/`, `test/*`, the MSBuild and package
  configuration of the root, `src/` and `test/`, `global.json`, the tool manifest, `build/property-seeds.txt`, or a
  non-`.cs` file of `src/<project>/`), so a test-only diff can't pass with zero mutants (SPEC §17.8). A project active
  now but inactive in the merge base's manifest also gets a full run, and a `--since` run fails when any mutant of a
  changed `.cs` file came back "Removed by since filter" (the linked-worktree symptom above; not "every": in that bad
  run the mutants under a disable comment keep the comment's reason, 18 since-filter plus 2 comment reasons on `Plain`).
- Every run fails when the report is missing (a broken run, never zero mutants), and when a `Killed` mutant has no
  `killedBy`. A full run also fails when at least 10 killed mutants in at least 3 files were all killed by one test:
  like the audit above, it fails on every mutated build, and any later whole-assembly reflection test over the mutated
  assembly would do the same. `--since` runs skip that check: one broad test can rightly kill every mutant of one file.
- `ExclusionGate` accepts only `// Stryker disable once <mutators> : <reason>` without `all`, and no
  `// Stryker restore`: a non-once or `all` disable ignores the rest of the file, which `--since` scores as zero.
  `once` covers the next syntax node, not the next line: the same comment above `TryTake` in a copy of `Plain`
  ignored all 10 mutants of the method ("Reason: probe") and the score stayed 100%; above a class it covers the class.
  So `ExclusionGate` also rejects a `disable once` whose next code line opens a type, namespace or member, and every
  run logs the mutants ignored by comments and fails when one ignored a mutant ending more than 5 lines below it.
- The targets call the `dotnet-stryker` local tool through `dotnet stryker` (like `reportgenerator` in `CoverageGate`):
  Fallout's `StrykerTasks` resolves Stryker as a NuGet package of the build project, which would need a second pin.
- SPEC's `--strict` is spelled `--strict-stages`: Fallout 10.4 reserves `--strict` for its own execution planner, which
  then demands a single total order of all targets and fails every run of this build ("Incomplete target definition
  order").
- On the stage-1 skeleton, `Mutation --strict-stages` runs all seven configurations against their real test projects
  (Ducky: 12 tests from six projects) in about a minute: six projects create zero mutants and fail the zero-mutant
  check, `Ducky.Reactive` has one killed mutant. That is why the check is staged (§17.8).

Not measured here: Linux and Windows (the `mutation` and `nightly-mutation` workflows arrive with M0-11), and run times
at scale (S-9).

## S-8: workflow job names from `DuckyGitHubActionsAttribute` (M0-11)

**Question.** Does overriding `GetJobs` give each workflow's job the workflow name without other side effects?

**Setup.** No separate prototype: the answer is the production attribute, `build/DuckyGitHubActionsAttribute.cs`,
checked by `VerifyWorkflows`. Run 2026-10-01 on macOS arm64 with SDK 10.0.401 and Fallout 10.4.0 (`Fallout.Common`,
tool manifest pin), over the nine workflows of `build/Build.CI.cs` (§20.1 without `nightly-audit`, which M16-01 adds).

**Answer: yes.** Without the override, every generated job is keyed and named after its image (`ubuntu-latest:` /
`name: ubuntu-latest`, likewise `windows-latest`, `macos-latest`): `VerifyWorkflows` reported nine S-8 violations and
`ubuntu-latest` shared by seven workflows. With `job.Name = IdPostfix` after `base.GetJobs`, each workflow has one job
keyed and named after the workflow (`ci`, `ci-cross-windows`, `ci-cross-macos`, `e2e`, `aot`, `mutation`,
`nightly-mutation`, `nightly-e2e`, `nightly-props`), unique across `.github/workflows` together with the hand-written
`pr-title`. The rest of each file is unchanged: `GitHubActionsJob.Write` uses `Name` for both the job key and `name:`
and nothing else (steps, `runs-on`, `timeout-minutes`, permissions and triggers come from the other properties);
actionlint 1.7 with shellcheck reports nothing on the generated files.

**Two consequences.**
- **Auto-generation is off** (`AutoGenerate = false` in the attribute's constructor). Fallout regenerates every
  configuration in place on each local build (`InvokeBuildServerConfigurationGenerationAttribute`, skipped on a server
  build), which would undo a hand edit before `VerifyWorkflows` could see it locally, and waits for a key press on an
  interactive console. A workflow is regenerated with
  `./build.sh --generate-configuration GitHubActions_<workflow> --host GitHubActions` (the command each file's header
  names), or by copying the file `VerifyWorkflows` left in `artifacts/workflows`.
- **`VerifyWorkflows` regenerates into `artifacts/workflows`** by running the build assembly once per attribute with
  `--generate-configuration` and `DUCKY_WORKFLOWS_DIR` set, which the attribute's `ConfigurationFile` honours, so the
  gate never writes `.github/workflows`.

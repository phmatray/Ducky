# ADR-0048: Mutation testing runs locally, not in GitHub Actions

- Status: Accepted
- Date: 2026-11-10
- Related: SPEC §17.1, §17.8, §19, §20.1, §20.2, §21, §23 (S-9); owner decision 2026-11-10; CI-MUTATION-LOCAL; supersedes the mutation-in-CI parts of ADR-0035, ADR-0036, ADR-0037 and ADR-0041 (each amended)

## Context

Since M0-15, mutation testing ran in three GitHub workflows: the required `mutation` check on every pull request (four file
shards and an aggregate job, `MutationPlan`, `MutationPr --shard`, `MutationPrAggregate`), `nightly-mutation` (four shards
and `MutationAggregate`, which opened an issue below 90 and cached a baseline report per commit), and the `release-mutation`
job of `release.yml` (`MutationForSha`: reuse the nightly's result for the tagged commit, else run the full `Mutation`).

Spike S-6 forces `coverage-analysis: "off"` in every `stryker-config.json`: Stryker 4.16's MTP runner keeps only the last
test project's per-test coverage, so with per-test coverage the mutants of the other test projects come back `NoCoverage`.
Without it every mutant runs every test of all eight test projects. Once every project held its real logic, the hosted
runners could no longer carry that load. Measured on PR #283:

- each of the four PR shards took 2 h 23 to 3 h 23 on `ubuntu-latest`, about 11.5 runner-hours per run;
- one GitHub shard died twice with "The runner has received a shutdown signal"; the same shard passed locally under act at
  4 CPUs and 16 GB in 2 h 31, at 100%;
- the release job's `MutationForSha` found no finished nightly run for the tag's commit and started a full single-runner run
  that cannot finish inside its 330-minute timeout (the local strict run of the seven projects took over 4 hours on 10 cores).

The owner's words: "il va falloir alléger la mutation. elle ne peut pas passer sur le CI", then, among the options offered,
"Supprimer du CI".

## Decision

No GitHub workflow runs mutation testing: no pull-request check, no nightly, no release job.

- `mutation.yml` and `nightly-mutation.yml` are deleted; `release.yml` keeps two jobs, `release-gates` and `publish`
  (`needs: [release-gates]`).
- The build loses what existed only for CI mutation: `MutationForSha` and its nightly-run query, `MutationPlan`,
  `MutationPrAggregate`, `MutationAggregate`, the `--shard` and `--shards` parameters, the shard partition and merge, the
  additive-test baseline (written by `Mutation`, cached by the nightly, restored by the PR shards), the score issue opened by
  the nightly, and the checks and planted cases of the two workflows and of the release mutation job.
- The local targets stay, with every check they had: `Mutation` (full, `--project`, `--strict-stages`, the score, zero-mutant,
  Safe Mode, disable-reach, `killedBy` and always-failing-test checks; below the 90% target it now logs a warning, and its last
  line lists every project's score) and `MutationPr` (a story's incremental `--since` run with its skip predicate and its
  full-run triggers; `--base-ref HEAD` scores uncommitted work). A project whose only change is an added test file now gets a
  full run, since the baseline is gone.
- `VerifyWorkflows` fails on any committed workflow step that runs a `Mutation*` target through `build.sh`, `build.cmd`,
  `build.ps1` or `dotnet fallout`, runs Stryker, or uses a Stryker action, with a planted self-check, so the check cannot come
  back silently.
- `build/protect-branch.sh` no longer requires a `mutation` check.
- The quality bar is unchanged and becomes a release prerequisite: before creating any `v*` tag the maintainer runs
  `./build.sh Mutation --strict-stages` on the exact commit to tag, on their machine or as the same command under act, and tags
  only when all seven projects score 100% (§21). `ReleaseGates` is otherwise unchanged.

## Consequences

No runner-hours spent on mutation, no required check that cannot finish, and no release job that times out. The cost is
discipline: nothing on GitHub proves that a tag was mutation-tested, so the release runbook (§21) carries the step, and a PR
can merge with a mutation regression that the next pre-tag run finds; `MutationPr --base-ref HEAD` in the PR template keeps
the per-story check. Spike S-9 (the runner budget of a full run) is moot. Mutation can return to CI only through a new ADR,
once the MTP runner keeps every test project's per-test coverage and `coverage-analysis` can be switched back on.

## Alternatives considered

Lightening the CI run instead (more or larger shards, a weekly instead of a nightly run, a self-hosted runner): not chosen;
the owner chose to remove mutation from CI. Any hosted variant still runs every test of eight test projects per mutant, and
neither the runner shutdowns nor the 330-minute release limit can be tuned away. Keeping `MutationForSha` with its local
fallback (rejected: without a nightly it is always the full run, inside a job that cannot finish it).

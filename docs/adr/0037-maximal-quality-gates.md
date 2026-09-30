# ADR-0037: Maximal quality gates

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §17; owner decision; open question 14; review rounds 4, 5, 6 and 7

## Context

The owner set a maximal bar: 1.x had no required CI, no coverage threshold, no mutation or property tests.

## Decision

100% line+branch coverage on every `src/` assembly including generators, measured over deterministic tests only; race-only branches are forbidden (code is restructured, or reaches the branch through an internal IVT-only seam: an interleaving hook that is null in production, or an instance delegate defaulting to the production call, such as the store's `QueueWorkItem` for the overflow-abort pool hop (review round 5) or a delegate for a branch only a trimmed AOT publish reaches; every seam lives on an object the owning package's test project can reach, such as an options object set from the `configure` lambda, never on a store-created middleware, review round 4); the concurrency project runs in its own `ConcurrencyTest` target, outside the coverage run, and the `Test` target fails unless `Ducky.Tests.slnf` holds every test project of `Ducky.slnx` except exactly `Ducky.Concurrency.Tests` and `Ducky.E2E` (review round 7); no `ExcludeFromCodeCoverage`, no reasonless `Stryker disable`, no unjustified `#pragma`, and no coverage-settings, ReportGenerator or Stryker configuration that narrows what is measured or mutated (review round 4); `SampleParallel`, `Task.Run` and `Barrier` are banned in covered test projects, and every invariant but INV-24 has a named test in one; NuGetAudit is enforced only by the nightly `Audit` target, so an advisory never fails PR checks; Stryker break 85 / target 90 (PR diff, nightly and release full), with `mutate: ["**/*.cs"]` (globs are relative to the project under test), `test-projects` equal to every test project referencing the mutated assembly, and a full run failing on zero mutants (review round 5); the test manifest is staged by §24 step, so every story PR passes the full gate for its stage and `--strict` release gates refuse an rc or GA with inactive entries, and `CoverageGate` reads its assembly list from `Ducky.slnx` (review round 5); CsCheck properties; named concurrency tests (10 s timeout × 50); Playwright 100% JS block coverage; AOT/trim gate; PublicAPI; package validation; warnings as errors with Recommended + VSTHRD + BannedApi; required checks. Review round 6 staged the remaining gates: each invariant carries its own stage (`invariants:` in the manifest) and the covered-test rule applies to each invariant whose stage is active, each mutated project carries a stage (`projects:`) and the zero-mutant check skips inactive projects (`--strict` checks all), PackageSmoke consumers join with their features, E2E publishes only existing samples, and every test project gets one real test at step 1 (MTP exits 8 on zero tests); property tests are deterministic wherever a gate measures (review round 7 corrected the round-6 rule: a committed `CsCheck_Seed` still lets CsCheck draw `iter - 1` random cases on several threads): every covered property goes through one helper, `Property.Check`, which in the gated runs (`DUCKY_PROPERTY_SEEDS` set, `CsCheck_Threads=1`) runs each seed of the committed list `build/property-seeds.txt` with `iter: 1` on one thread, `M:CsCheck.Check.Sample*` is banned elsewhere in covered projects, and random seeds run only in the nightly `PropertyLong`, whose failing seeds join the list; `TaskFactory.StartNew`, `new Thread`, `ThreadPool.QueueUserWorkItem`/`UnsafeQueueUserWorkItem` and `Parallel` join the covered-project ban; post-await cancellation re-checks use `ThrowIfCancellationRequested`, never a race-only `if`; the systematic survivors, `ConfigureAwait` literals and descriptor strings, are killed or avoided (`ConfigureAwaitOptions.None`, `Descriptors_MatchCatalogue`), never ignored through `ignore-methods`; spikes are week-1 prototypes whose named tests are regression tests at their stage.

## Consequences

Every public line has a meaningful test; the surface is kept small to make the bar reachable.

## Alternatives considered

Lower thresholds (rejected by the owner).

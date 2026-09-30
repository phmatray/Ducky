# Ducky 2.0 Implementation Plan

Status: plan derived from `SPEC.md` (authoritative, including review rounds "2 (recovered)" through 7 of `REVIEW-LOG.md`) and `adr/0001-0047`. Where this plan and the spec disagree, the spec wins; every planning decision that goes beyond the spec is listed in §3.

152 stories in 17 milestones. Every story is S or M (one agent session), names its dependencies, its **stage** (the §24 step whose manifest entries it activates, P1), an `area` used to avoid parallel conflicts, the invariants it covers, and named acceptance tests. Names without a qualifier are the spec's normative test names (they are in `docs/spec/tests.yaml`); a project in parentheses says where a name runs when it is not the story's default test project; names marked *(non-normative)* are additional tests or checks the story adds.

## 1. Workflow: strict TDD under full gates

Every story after M0-01 is one issue and one squash-merged PR on `v2`, and it follows this loop:

1. **Red.** Write the story's named tests first (the spec names are normative, so use them verbatim). Commit them failing; the PR history shows the red commit.
2. **Green.** Write the least production code that makes them pass. No code without a failing test asking for it; no speculative API.
3. **Refactor.** Clean up with the tests green. Keep `PublicAPI.Unshipped.txt` and XML docs in step.
4. **Gate.** `./build.sh Ci` locally, then CI. The PR merges only when every required check is green.

### Common definition of done (applies to every story)

- The story's named tests exist and pass; their `docs/spec/tests.yaml` entries carry the story's stage, and SpecTraceGate is green at the current `activeStage`. The PR that merges the last open story of a stage bumps `activeStage` (P1).
- CoverageGate: 100% line and 100% branch for every `src/` assembly listed in `Ducky.slnx`, measured without `Ducky.Concurrency.Tests`. No race-only branches: a branch reached only by an interleaving gets a `…_Deterministic` twin in a covered project (FakeTimeProvider, TCS gating, or an internal IVT seam on an object the owning package's test project can reach: `AfterSubscribeHook`, `BeforeNotifyHook`, `BeforeProcessHook`, `QueueWorkItem`, `DevToolsOptions.DescribeFrame`, `BlazorOptions.AfterDeferHook`, `BlazorOptions.AfterSeedIdleHook`). Covered property tests go through `Property.Check` (fixed seeds when gated), and every branch they reach is also reached by an example-based or fixed-seed test.
- MutationPr: Stryker score on the diff >= 85 (target 90) for every active project. Equivalent mutants carry `// Stryker disable once <mutator> : <reason>`. Every EventId is asserted through `FakeLogCollector`.
- Build: zero warnings (warnings are errors, NU1901-NU1904 excepted), zero IL2xxx/IL3xxx, format clean, no banned API (no sleeps, no `DateTime.Now`, no `ExcludeFromCodeCoverage`, no reflection activation, JS interop only inside `JsBridge`, no `Task.Run`/`Barrier`/`SampleParallel`/raw `Sample` in covered test projects), `#pragma warning disable` only with `// justification:`.
- Public surface: `PublicAPI.Unshipped.txt` updated, XML docs on every public member, every new DUCKY code has `docs/diagnostics/{ID}.md`, and an analyzer descriptor has its line in its own assembly's `AnalyzerReleases.Unshipped.md` and a `Descriptors_MatchCatalogue` row.
- Concurrency tests: only real interleavings, in `Ducky.Concurrency.Tests` (run by `ConcurrencyTest`, never measured): 10 s `WaitAsync` bound, Barrier/TCS choreography, green at `DUCKY_REPEAT=50` on Linux, Windows and macOS.
- No mocks: the store is always real; fakes only at I/O boundaries (`FakeJsRuntime`, `FakeJsStreamReference`, `FakeComponentStateStore` driving a real `ComponentStatePersistenceManager`), `MemoryDistributedCache` as-is.
- Staged neighbours: a generator or analyzer arm, and a PackageSmoke consumer or assertion, ship with the feature they target (P10).

## 2. Conventions for parallel agents

- **Areas.** A story's `area` is the directory or file set it mostly touches. Stories whose areas overlap are never run in parallel, even when their dependencies allow it.
- **Append-only hot spots** (touched by many stories; resolve conflicts by taking the union): `src/*/PublicAPI.Unshipped.txt`, `docs/spec/tests.yaml` (stages and `activeStage`), `src/*Generators/AnalyzerReleases.Unshipped.md`, `Directory.Packages.props`, the `[LoggerMessage]` Log classes (reserve EventId ranges per story in the PR description), `Dispatcher.Process` (steps are added by M1-05, M1-05b, M1-06, M2-01, M2-03, M2-04, M2-07, M3-01b, M4-01, M4-01b, M4-08, M4-09: keep each step in its own method), `DispatchAsync_EveryPath_ReturnsExpectedResult` (rows added by M4-10 and M9-01b).
- **Build files.** The Fallout `Build` partial class is split per concern (`Build.cs`, `Build.Gates.cs`, `Build.SpecTrace.cs`, `Build.Mutation.cs`, `Build.Test.cs`, `Build.E2E.cs`, `Build.Aot.cs`, `Build.Release.cs`, `Build.PublicApi.cs`, `Build.Audit.cs`, `Build.CI.cs`) so M0 stories can run in parallel. This refines the §18 file list; the targets are exactly §19's.
- **Single-file lanes.** `ducky.js` is one file: M0-02 (stub), M6-01, M7-01 and M8-01 are serialized by their dependencies. `test/Ducky.AotSmoke`: M0-08, M3-05, M4-07, M9-06, M12-05, M11-04 (and M14-06 for the target). `test/Ducky.PackageSmoke`: M0-07, M13-11, M6-01, M13-04b, M11-05, M10-07.
- **Code navigation.** Agents that touch existing C# use the RoselineMCP tools (`search_symbols`, `get_symbol_info`, `find_references`, `edit_member`) rather than whole-file reads, per the owner's global instructions.

## 3. Planning decisions beyond the spec

| # | Decision | Why |
|---|---|---|
| P1 | `docs/spec/tests.yaml` is staged as §24 requires. Every story carries a stage (a §24 step; the "Stage" line below). Its manifest entries get that stage (an entry listed in several projects takes the stage of the last story that adds it); each invariant's stage is the lowest stage of a story that adds a covered-project test for it; `projects:` follows §17.8. `activeStage` moves to N in the PR that merges the last open story of the stages <= N: it may jump several stages when later ones are already complete, and it never passes a stage with an open story. A story's stage is never lower than its dependencies' (checked with `depends_on`). Stories of a later stage may merge early (the Draft, Reactive and release lanes): their tests run under Test, CoverageGate and the name checks at once, while existence checks, MutationPr and the zero-mutant check of an inactive project wait for the bump. | §24 has each story PR bump `activeStage` to its own step, which assumes one open story per step. With parallel lanes, the first story of step N would activate its siblings' entries before they exist; the bump belongs to the PR that completes the stage. |
| P2 | The step-1 skeleton gives every library project one public type of its final API with a test and every test project one `Skeleton_{Project}_Smoke` test (§24 step 1); the story that first adds real tests to a project deletes its skeleton test and entry (named in that story's Done). | CoverageGate takes its assembly list from `Ducky.slnx` and MTP exits with code 8 on a test project with zero tests, so no placeholder rule is needed. |
| P3 | Every spike has its week-1 prototype in M0 (S-1 M0-01, S-2 M0-04, S-6 M0-05, S-8 M0-11, S-3/S-5/S-7 M0-13, S-4/S-9 M0-14). The regression tests land at their §23 stages: S-4 in M1-14, S-9 re-measured in M12-02 and M10-07 and finalised in M16-02, S-3/S-5/S-7 at stage 18 (M14-03, M14-05, M14-06). | §23: a prototype answers the platform question before the steps that depend on it are designed; the named tests need later features. |
| P4 | The requested 'PackageValidation' and 'PublicApi' targets are realised under the spec's names: `Pack` (EnablePackageValidation and the exact-set check) + `PackageSmoke`, and PublicApiAnalyzers in `Compile` + `ShipPublicApi`/`SetBaseline`. 'Coverage gate' = `CoverageGate`, 'Mutation gate' = `MutationPr` (PR) and `Mutation`/`MutationForSha` (nightly/release). | The spec's target names are normative (§19). |
| P5 | Milestones keep the requested narrative (core, effects, selectors, middleware, Blazor, persistence, cross-tab, DevTools, SliceStore, Draft, Reactive, server persistence, generators, samples, docs, RC), but stages follow §24: middleware (3) before effects (4), generators (M13, 7) before SliceStore (8), Ducky.Testing (9) and Blazor (10+), components (10) before the gate and the prerender handoff (11), server persistence (13) before cross-tab (14). Dependencies and stages, not milestone numbers, drive scheduling. | The staged gates make §24's order binding for the manifest; stable milestone labels keep the existing story ids. |
| P6 | `ActionContext` is introduced with middleware (M4-01), which §24 places before effects; `EffectContext.Trigger` reuses it (M2-01). | Follows §24 step 3 before step 4. |
| P7 | The shared generator helpers (`src/Shared.Generators`) land with Ducky.Generators (M13-01, stage 7), the first generator in §24 order; the Draft generator harness (M10-03) reuses them. | Shared helpers are linked per use (§16.1); CoverageGate catches an over-broad link. |
| P8 | AOT smoke coverage grows in small per-area stories (M3-05, M4-07, M9-06, M12-05, M11-04) that extend the existing `AotSmoke` assertion instead of printing new PASS names; only spec names (`AotSmoke`, `ActionType_AttributeName_SurvivesAot`, `DevToolsPayload_NonEmpty`) are printed. | The AotSmoke target requires the printed PASS set to equal the active Ducky.AotSmoke manifest entries, and every manifest name must appear in SPEC.md. |
| P9 | Samples are coverage-exempt (not `src/`) but under warnings-as-errors and analyzers; they double as the DUCKY001-003 false-positive corpus. | §17.1 measures `src/` only. |
| P10 | A generator or analyzer arm that targets another package's API, or a later step's Ducky API, ships in its own S story right after the story that ships that API, at the same stage (M9-01c, M9-04c, M13-09, M13-04, M13-04b, M11-05, M10-07b). Each PackageSmoke consumer or assertion ships with its feature likewise (M13-11, M6-01, M13-04b, M11-05, M10-07). | §24 puts the arm in the story that ships the API; a separate S story that depends on it and shares its stage keeps stories S/M, while no emit or analyzer branch is ever unreachable or stubbed (GEN-06) and CoverageGate holds from step 7. |

## 4. Milestones

| Milestone | Title | Stories | Goal |
|---|---|---|---|
| M0 | Engineering skeleton | 14 | Orphan v2 branch with every gate live before any feature code (§24 step 1): locked restore, warnings-as-errors analyzers, signing, Fallout targets (Restore, Format, Compile, Test, ConcurrencyTest, CoverageGate 100%, MutationPr/Mutation, SpecTraceGate over the staged manifest, ExclusionGate, AotSmoke, E2E with the ducky.js block gate over a stub, Pack + package validation + staged PackageSmoke, PublicAPI tracking, VerifyWorkflows, Docs fence check), generated GitHub Actions calling Fallout, and branch protection. Every library has one public type with a test and every test project one skeleton test. S-1, S-2, S-6, S-8 closed; week-1 prototypes for S-3, S-4, S-5, S-7 and S-9. |
| M1 | Core store | 15 | Slices, slice keys, snapshot, single-drainer dispatcher, drain catch-all and SafeLogger, per-store causal scope, failure router, builder and aggregated configuration errors, DI lifetimes, init buffer with zero middleware, core dispose, WhenIdleAsync, and TST-03 concurrency items 1-5, 7, 11-13 (S-4 regression). |
| M2 | Effects | 10 | Effect<T>, EffectGroup, EffectContext and ctx.Run; Merge/Switch/Exhaust/Queue with the slot compare-and-remove rule, failure observation, quiescence with CountedForIdle, the run registry and effect-aware dispose (phase 5b), the policy matrix and model-based property (INV-11), keyed DUCKY309. |
| M3 | Selectors and subscriptions | 6 | SubscriberList, IStore.Select, Selection<T>, the typed fast path, Selector.Define memoization (INV-09, INV-21), subscriber isolation in covered tests, and the AOT smoke for the core. |
| M4 | Middleware, init lifecycle and core contract | 14 | Middleware hooks, veto and ActionContext, the Process failure rule, DispatchSystem, middleware init with timeout and the QueueWorkItem overflow abort, disposal during init with per-middleware bounds, materialization versus disposal, the JSON gateway, action names, unhandled check, tracing/metrics with SafeTelemetry and the never-fault contract. The Ducky core API is complete after M4. |
| M5 | Blazor components and interactivity | 5 | Ducky.Blazor foundation (AddBlazor, JsBridge, fakes on a real ComponentStatePersistenceManager), SubscriptionCore, DuckyComponent/Layout/Select/Initializer (§24 step 10), then the InteractivityGate and first-toucher hand-off (step 11, INV-19, INV-20), built on the week-1 S-3/S-5/S-7 prototypes. |
| M6 | Browser persistence and prerender | 15 | Prerender<T> and the prerender handoff (seed take, write, browser wait; step 11), then ducky.js storage, envelopes, Persist<T>, the persistence slice, hydration with exactly-one-terminal, PersistenceWriter with the race-proof defer, retry and the phase-5a flush, ClearPersistedState, seed/storage precedence and pause seeds with dirty keys and versions (INV-14, INV-15, INV-16). |
| M7 | Cross-tab | 4 | ducky.js watch/relay/resync with pruning and repair, the .NET CrossTabSync with echo, removal and convergence guards, and scope-aware receipts (INV-18). |
| M8 | DevTools | 4 | Redux DevTools extension bridge: outbound FSA with sanitizers, failure projections and trace, immutable history, inbound jump/reset/import with pulled large messages and declared types, and the time-travel veto (INV-28). |
| M9 | SliceStore, entities and Ducky.Testing | 10 | SliceStore<TState> (INV-30) inside runs and across stores and its Blazor-host integration, EntityState (INV-32), TestStore/SliceTest/EffectTest with the wall-clock dispose bound, the SliceStore and TestStore analyzer arms, AOT smoke extension. |
| M10 | Ducky.Draft | 8 | Draft runtime collections, the Draft generator with symbol-based mapping on the shared helpers, INV-26 properties, DUCKY101-109, the DUCKY003 Draft-receiver exemption, multi-assembly fixture, DUCKY901 and the Draft PackageSmoke consumer. |
| M11 | Ducky.Reactive | 7 | R3 host, bridge and middleware; failures as system failure actions; resubscribe with backoff and its races; excluded from quiescence (INV-27); Reactive contract twins, generator arms and PackageSmoke consumer; AOT smoke for Draft and Reactive. |
| M12 | Server persistence | 9 | PersistStorage.Server over IDistributedCache, scopes, retries and the phase-5a flush, the auth-driven scope switch with epochs, init-phase deadlines, reconnect absorption and attempt supersession (INV-17), static SSR pins, pause seeds across scopes; AOT smoke for server persistence and DevTools payload. |
| M13 | Generators, analyzers and tombstones | 12 | Shared generator helpers, GEN-01 dispatch helpers, GEN-02 core registration (step 7) and its Blazor arms (Prerender<T> at stage 11, PersistAttributeDefaults<T> and Ducky.Blazor.Generated.Tests at stage 12), multi-assembly fixture (INV-25), the stage-7 analyzers (DUCKY001-025 arms whose APIs exist by step 6), DUCKYM001-M008 tombstones, stage-7 PackageSmoke assertions. |
| M14 | Samples, E2E and AOT | 6 | Samples.Wasm and Samples.Server (Interactive Auto), every Playwright test of §17.6 at stage 18, and the trimmed AOT-WASM smoke that can't skip in the aot job. |
| M15 | Docs and migration guides | 7 | docfx with compiled-snippet regions, user guides, threading.md, a complete diagnostics catalogue checked by Docs, and the seven migration guides with compiled before/after samples and the 1.x API list check. |
| M16 | Release candidate | 6 | Nightly audit/benchmarks, mutation budget (S-9) and score >= 90, the three-job release pipeline (trusted publishing), spec closure (activeStage 19, SpecTraceGate --strict), v2.0.0-rc.1 with the two consumer migrations, and the GA runbook. |

## 5. Scheduling

Longest dependency chain (36 stories): M0-01 → M0-02 → M0-03 → M0-04 → M0-11 → M0-12 → M1-01 → M1-02 → M1-04 → M1-05 → M1-06 → M1-07 → M1-10 → M1-11 → M1-12 → M4-01 → M2-01 → M2-02 → M2-03 → M2-04 → M2-05 → M2-06 → M3-05 → M4-07 → M4-10 → M13-01 → M13-03 → M13-08 → M13-11 → M13-04b → M14-01 → M14-03 → M16-02 → M16-04 → M16-05 → M16-06.

Stories per stage (P1: `activeStage` reaches N once every story listed at N or below is merged):

| Stage | §24 step | Stories |
|---|---|---|
| 1 | skeleton, gates, spike prototypes | M0-01, M0-02, M0-03, M0-04, M0-05, M0-06, M0-07, M0-08, M0-09, M0-10, M0-11, M0-12, M0-13, M0-14 |
| 2 | slice, snapshot, dispatcher, causal scope, TST-03 | M1-01, M1-02, M1-03, M1-04, M1-05, M1-05b, M1-06, M1-08, M1-09, M1-13, M1-14 |
| 3 | init lifecycle, middleware, failure router, dispose | M1-07, M1-10, M1-11, M1-12, M4-01, M4-02, M4-03, M4-03b, M4-03c, M4-04 |
| 4 | effects and policies | M2-01, M2-02, M2-03, M2-03b, M2-04, M2-05, M2-06, M2-07, M2-08, M2-09, M4-05, M4-05b |
| 5 | selectors and subscribers | M3-01, M3-01b, M3-02, M3-03, M3-04, M3-05, M4-01b |
| 6 | JSON gateway, action names, diagnostics, unhandled check | M4-06, M4-07, M4-08, M4-09, M4-10 |
| 7 | generators, analyzers, tombstones | M13-01, M13-02, M13-03, M13-05, M13-06, M13-07, M13-08, M13-10, M13-11 |
| 8 | SliceStore, EntityState | M9-01, M9-01b, M9-01c, M9-03, M9-06 |
| 9 | Ducky.Testing | M9-04, M9-04b, M9-04c, M9-05 |
| 10 | Blazor subscription core and components | M5-02, M5-03, M5-05, M5-06, M13-09 |
| 11 | interactivity gate and prerender handoff | M5-04, M6-04, M6-05, M6-06, M13-04 |
| 12 | browser persistence | M6-01, M6-02, M6-03, M6-07, M6-08, M6-09, M6-10, M6-10b, M6-11, M6-12, M6-13, M6-13b, M9-02, M13-04b |
| 13 | server IDistributedCache provider | M12-01, M12-02, M12-03, M12-03b, M12-03c, M12-04, M12-06, M12-07 |
| 14 | cross-tab | M7-01, M7-02, M7-02b, M7-03 |
| 15 | DevTools | M8-01, M8-02, M8-03, M8-03b, M12-05 |
| 16 | Ducky.Reactive | M11-01, M11-01b, M11-02, M11-02b, M11-03, M11-05 |
| 17 | Ducky.Draft | M10-01, M10-02, M10-03, M10-04, M10-05, M10-06, M10-07, M10-07b, M11-04 |
| 18 | AOT/trim smoke, E2E, docs, samples, migration guides | M14-01, M14-02, M14-03, M14-04, M14-05, M14-06, M15-01, M15-02, M15-03, M15-04, M15-05, M15-06, M15-07 |
| 19 | RC and consumer migrations; GA | M16-01, M16-02, M16-03, M16-04, M16-05, M16-06 |

Parallel lanes:

- **Core lane (stages 2-6):** M1 → M4 middleware and init (3) → M2 effects (4) → M3 selectors (5) → M4 JSON, names, contract (6). M4-10 closes the core API.
- **After M4-10:** generators and analyzers (M13, stage 7) in parallel with SliceStore/EntityState (M9, 8), Ducky.Testing (M9-04, 9) and the Blazor foundation (M5-02/M5-03, 10); the stage bumps still follow 7 → 8 → 9 → 10.
- **Blazor lane:** M5 → M5-04 and M6 prerender (11) → M6 persistence (12) → M12 server persistence (13) → M7 cross-tab (14) → M8 DevTools (15), strictly ordered by persistence internals.
- **Reactive lane:** M11 after M4-10 (stage 16; may merge early, P1); its generator arms (M11-05) after M13-03.
- **Draft lane:** M10-01/M10-02 depend only on M0 (Ducky.Draft does not reference Ducky); the Draft generator starts after M13-01 (stage 17; may merge early, P1).
- **Release lane:** M16-03 (release pipeline) and M16-01 (audit/benchmarks) can land early to publish milestone alphas (`v2.0.0-alpha.N`) of the active stage.

## 6. Stories

### M0 — Engineering skeleton

Orphan v2 branch with every gate live before any feature code (§24 step 1): locked restore, warnings-as-errors analyzers, signing, Fallout targets (Restore, Format, Compile, Test, ConcurrencyTest, CoverageGate 100%, MutationPr/Mutation, SpecTraceGate over the staged manifest, ExclusionGate, AotSmoke, E2E with the ducky.js block gate over a stub, Pack + package validation + staged PackageSmoke, PublicAPI tracking, VerifyWorkflows, Docs fence check), generated GitHub Actions calling Fallout, and branch protection. Every library has one public type with a test and every test project one skeleton test. S-1, S-2, S-6, S-8 closed; week-1 prototypes for S-3, S-4, S-5, S-7 and S-9.

#### M0-01 Orphan v2 branch and repository root (M)

- **Stage:** 1
- **Depends on:** —
- **Unblocks:** M0-02, M0-13
- **Area:** `repo-root`
- **Invariants:** —
- **Scope:** `git switch --orphan v2` in phmatray/Ducky; the owner makes v2 the default branch (§18). Root files: global.json (SDK 10.0.100, rollForward latestFeature, allowPrerelease false, test runner MTP), .editorconfig, .gitignore, .gitattributes, LICENSE (Apache-2.0), NOTICE, CHANGELOG.md stub, ducky.snk, Directory.Build.props (LangVersion latest, Nullable, ImplicitUsings, TreatWarningsAsErrors with WarningsNotAsErrors NU1901-NU1904, AnalysisLevel latest-recommended, EnforceCodeStyleInBuild, GenerateDocumentationFile for src, Deterministic, ContinuousIntegrationBuild and RestoreLockedMode in CI, IsPackable=false by default (R-PKG-6), RepoRoot=$(MSBuildThisFileDirectory), PackageOutputPath=$(RepoRoot)artifacts/packages/, SignAssembly with ducky.snk for src AND test projects, DuckyPublicKey defined once), Directory.Build.targets (PublicApiAnalyzers, BannedApiAnalyzers, VSTHRD analyzers, CA2007 for src; test defaults; test/BannedSymbols.covered.txt for every test project except Ducky.Concurrency.Tests; the DuckyUseGenerators/DuckyUseDraftGenerator switches for in-repo consumers, §18), Directory.Packages.props (CPM + CentralPackageTransitivePinningEnabled; Roslyn pinned per S-1; R3 1.3.x as a plain version, pinned by the lock file, packed as R3 >= 1.3.x), BannedSymbols.txt (§10: JsonSerializer without JsonTypeInfo, reflection activation, the four JSInterop extension types and every InvokeAsync/Invoke overload by documentation ID, the Task.Delay/Thread.Sleep/DateTime(Offset).Now overloads by exact ID, StackFrame.GetMethod, ExcludeFromCodeCoverageAttribute), test/BannedSymbols.txt (§17.1 no-sleeps; Task.Delay(TimeSpan, TimeProvider[, CancellationToken]) allowed), test/BannedSymbols.covered.txt (SampleParallel, M:CsCheck.Check.Sample*, Task.Run, Barrier, TaskFactory.StartNew*, Thread.#ctor*, ThreadPool.QueueUserWorkItem*/UnsafeQueueUserWorkItem*, Parallel), build/property-seeds.txt, empty Ducky.slnx. Copy SPEC.md and adr/ to docs/spec and docs/adr. Spike S-1: find the Microsoft.CodeAnalysis.CSharp version shipped with SDK 10.0.100, pin it in CPM with a Renovate ignore rule.
- **Acceptance tests:**
  - `dotnet build Ducky.slnx` succeeds on a clean clone with SDK 10.0.100 and no workload
  - `git log v2` has no ancestor from main (orphan)
  - S-1: Roslyn version recorded in Directory.Packages.props with a comment, and a consumer build pinned to SDK 10.0.100 compiles
  - Planted check: Task.Run in a covered test project fails Compile (test/BannedSymbols.covered.txt)
- **Done:** Branch pushed, v2 is the default branch, S-1 closed with the pinned version. No production code yet (TDD starts at M0-02).

#### M0-02 Project skeletons, one public type per library, one test per test project (M)

- **Stage:** 1
- **Depends on:** M0-01
- **Unblocks:** M0-03, M0-06, M0-09, M13-10
- **Area:** `src/*/*.csproj, src/*/Skeleton*, test/*, test/Shared/Property.cs, Ducky.slnx, Ducky.Tests.slnf`
- **Invariants:** INV-22
- **Scope:** Projects per §18, all in Ducky.slnx from day one. src: Ducky, Ducky.Reactive, Ducky.Draft, Ducky.Testing (net10.0, IsAotCompatible, strong-named, IsPackable=true, PublicAPI.Shipped/Unshipped.txt); Ducky.Blazor on Microsoft.NET.Sdk.Razor with SupportedPlatform browser and a one-export stub wwwroot/ducky.js; Ducky.Generators and Ducky.Draft.Generators (netstandard2.0, IncludeBuildOutput=false, packed into analyzers/dotnet/cs of Ducky / Ducky.Draft); Ducky.Generator.Tombstone (Microsoft.Build.NoTargets, MinVerSkip, Version 2.0.0, IncludeSymbols=false, SuppressDependenciesWhenPacking, PackageOutputPath $(RepoRoot)artifacts/tombstone/, buildTransitive only); buildTransitive/Ducky.targets (DUCKY900; DUCKY902 after ResolvePackageAssets for a Ducky.Blazor package with major < 2; the CompilerVisibleProperty items, §3) and Ducky.Draft.targets (DUCKY901). Ducky.Blazor/Reactive/Testing reference Ducky with PrivateAssets=none (R-PKG-5). Each library project gets one public type of its final §5-§15 API with a test, so CoverageGate holds at 100% from the first PR (§24 step 1). Test projects (xUnit v3 on MTP v2, Shouldly, CsCheck, FakeLogCollector, no mocks), signed with ducky.snk: Ducky.Tests, Ducky.Concurrency.Tests, Ducky.Blazor.Tests, Ducky.Reactive.Tests, Ducky.Draft.Tests, Ducky.Testing.Tests, Ducky.Generators.Tests, Ducky.Draft.Generators.Tests, Ducky.Docs.Tests and Ducky.E2E; each receives IVT only from its own package, every item written with Key="$(DuckyPublicKey)" (R-PKG-2; Ducky.Concurrency.Tests from Ducky and Ducky.Blazor). Every test project has one real test, Skeleton_{Project}_Smoke (stage 1, no invariant), deleted with its manifest entry by the story that replaces it: a Barrier test over the step-1 type in Ducky.Concurrency.Tests, a region test in Ducky.Docs.Tests, a harness spec over the stub ducky.js in Ducky.E2E (MTP exit code 8 is never ignored). test/Shared/Property.cs (Property.Check: with DUCKY_PROPERTY_SEEDS set, each seed of build/property-seeds.txt with iter 1 and threads 1; the one RS0030-justified CsCheck Sample call) linked into every covered test project. Red first: a linked StaticFieldAudit helper (§17.5 allow-list, [CompilerGenerated] types exempt) and a fixture assembly with a planted mutable static, a static readonly DiagnosticDescriptor and a non-capturing lambda; then NoStaticMutableFields in every package test project. Ducky.Tests.slnf lists every test project except Ducky.Concurrency.Tests and Ducky.E2E. Any CS8002 is suppressed only in the referencing csproj with a justification, and the list is recorded.
- **Acceptance tests:**
  - `NoStaticMutableFields` (one per package test project, 7 projects)
  - `NoStaticMutableFields_DetectsPlantedMutableStatic`
  - `Skeleton_{Project}_Smoke` (one per test project)
  - `dotnet test --solution Ducky.Tests.slnf` green; `dotnet pack` puts Ducky.Generators.dll under analyzers/dotnet/cs of Ducky
  - A signed test project receiving IVT with the public key builds (no CS1726)
- **Done:** Common DoD (PLAN.md §1; gates that exist so far). Every package project and its test project exist; the static-state audit is the first red-green test.

#### M0-03 Fallout build: core targets (M)

- **Stage:** 1
- **Depends on:** M0-02
- **Unblocks:** M0-04, M0-05, M0-06, M0-07, M0-08, M0-09, M0-10
- **Area:** `build/Build.cs, build/_build.csproj, build.sh/ps1/cmd, .config/dotnet-tools.json`
- **Invariants:** —
- **Scope:** build/_build.csproj on Fallout.Common 10.4.0 (exact), tool manifest (Fallout.GlobalTool 10.4.0, dotnet-stryker, reportgenerator, docfx). `partial class Build : FalloutBuild, IHasSolution, IConfigureGitHubActions` (no ITest/IReportCoverage/IPack, §19) with the ReleaseBranch, BaseRef and ReleaseVersion parameters. Targets: Clean, Restore (locked mode, packages.lock.json committed), Format, Compile (Release, plus a check that fails when a packable net10.0 src project lacks IsAotCompatible), Test (fails first unless the test projects of Ducky.Tests.slnf equal those of Ducky.slnx minus the coverage exclusion list {Ducky.Concurrency.Tests, Ducky.E2E}; then MTP syntax with DUCKY_PROPERTY_SEEDS=1 and CsCheck_Threads=1, cobertura + trx into artifacts/test, DUCKY_REPEAT passthrough), ConcurrencyTest (Ducky.Concurrency.Tests without coverage into artifacts/concurrency), ExclusionGate text checks in build/Build.Gates.cs (reasonless `Stryker disable`, unjustified `#pragma warning disable` in src), and an initial Ci aggregate.
- **Acceptance tests:**
  - `./build.sh Ci` green on the skeleton
  - Planted check: removing IsAotCompatible from src/Ducky.Blazor fails Compile
  - Planted check: `// Stryker disable all` without `: reason` fails ExclusionGate; `#pragma warning disable` without `// justification:` fails ExclusionGate
  - Planted check: `Task.Delay` in a test file fails Compile (test/BannedSymbols.txt)
  - Planted check: a stale packages.lock.json fails Restore
  - Planted check: a test project added to Ducky.slnx but not to Ducky.Tests.slnf fails Test
  - `ConcurrencyTest` runs the Ducky.Concurrency.Tests skeleton test without writing a cobertura file
- **Done:** Targets are thin dotnet CLI wrappers; each planted check is shown failing in the PR description, then reverted.

#### M0-04 CoverageGate (100% line and branch) and spike S-2 (M)

- **Stage:** 1
- **Depends on:** M0-03
- **Unblocks:** M0-11
- **Area:** `build/Build.Gates.cs`
- **Invariants:** —
- **Scope:** CoverageGate: ReportGenerator merges every artifacts/test/**/*.cobertura.xml (Ducky.Concurrency.Tests never writes there) into artifacts/coverage (Cobertura + HTML); fails when any assembly's line-rate or branch-rate < 1.0 (prints uncovered file:line) or when the report's assembly set differs from the src projects listed in Ducky.slnx (a missing assembly fails; the NoTargets tombstone has no assembly and is excluded). No ReportGenerator filter beyond that assembly list. ExclusionGate pins exactly one committed coverage settings file whose only source exclusion is **/*.g.cs (plus the default CompilerGenerated/GeneratedCode handling S-2 records). Spike S-2 as a week-1 prototype under spikes/ (async state machines, records, lowered lock, await using, switch expressions, a member-less error-level [Obsolete] interface, a generator run through CSharpGeneratorDriver): reach 100% with no exclusion and no crash on the pinned xunit.v3/MTP versions; pick MTP CodeCoverage or coverlet.MTP and record the choice in docs/spec/spikes.md.
- **Acceptance tests:**
  - Planted check: an uncovered branch in src/Ducky fails CoverageGate and prints its file:line
  - Planted check: a src project of Ducky.slnx missing from the merged report fails CoverageGate
  - Planted check: an extra exclusion in the coverage settings file fails ExclusionGate
  - S-2 prototype reaches 100% line and branch with no exclusion (recorded in docs/spec/spikes.md)
- **Done:** Common DoD. Coverage tool choice recorded; gate added to Ci.

#### M0-05 Mutation gates and spike S-6 (M)

- **Stage:** 1
- **Depends on:** M0-03
- **Unblocks:** M0-11, M0-14
- **Area:** `build/Build.Mutation.cs, src/*/stryker-config.json`
- **Invariants:** —
- **Scope:** One stryker-config.json per mutated project (Ducky, Ducky.Blazor, Ducky.Reactive, Ducky.Draft, Ducky.Testing, Ducky.Generators, Ducky.Draft.Generators): test-runner mtp, thresholds 90/85/85, mutate exactly ["**/*.cs"] with no ! pattern, test-projects = every covered test project referencing the assembly plus Ducky.Concurrency.Tests for Ducky, Ducky.Blazor, Ducky.Reactive and Ducky.Testing, additional-timeout, no mutation-level or ignore-* keys; ExclusionGate checks those keys and the computed test-projects lists. Targets (Stryker process env DUCKY_REPEAT=1, DUCKY_PROPERTY_SEEDS=1, CsCheck_Threads=1): MutationPr (--since BaseRef per active project, break 85, zero mutants allowed), Mutation (full per active project from the manifest's projects: stages, optional repeatable --project <name>, --strict runs all seven, fails on zero mutants or no score for an active project, HTML to artifacts/mutation, opens an issue below 90), PropertyLong (CsCheck_Iter=100000, DUCKY_REPEAT=1, DUCKY_PROPERTY_SEEDS unset). Spike S-6 as a week-1 prototype under spikes/: --since and full runs on a plain library, a generator and a Razor SDK library shaped like Ducky.Blazor (STJ and [LoggerMessage] generators, IVT to two test projects), mutants > 0 per project, a concurrency test observed running once per mutant, and the ConfigureAwait answer recorded (if the boolean literal is mutated: ConfigureAwaitOptions.None on Task awaits, reasoned inline disables only on ValueTask/await using lines).
- **Acceptance tests:**
  - Planted check: a PR adding a surviving mutant breaks MutationPr
  - `MutationPr` and Mutation pass on the skeleton (every mutated project is inactive at stage 1)
  - Planted check: a `!` pattern or an ignore-methods key in a stryker-config.json fails ExclusionGate
  - S-6 prototype: scores reported for the three prototype projects with mutants > 0 and the ConfigureAwait answer (docs/spec/spikes.md)
- **Done:** Common DoD. MutationPr becomes part of the `mutation` workflow in M0-11.

#### M0-06 SpecTraceGate and the staged docs/spec/tests.yaml (M)

- **Stage:** 1
- **Depends on:** M0-02, M0-03
- **Unblocks:** M0-11
- **Area:** `build/Build.SpecTrace.cs, docs/spec/tests.yaml`
- **Invariants:** —
- **Scope:** Write docs/spec/tests.yaml from §4, §14 and §17: every named test with name, project(s), invariant and stage (P1: the stage of the story below that implements it; a name listed in several projects takes the stage of the last story that adds it), the invariants: map (each invariant's stage = the lowest stage of a story adding a covered-project test for it), the projects: map of §17.8 (Ducky 2, Ducky.Generators 7, Ducky.Testing 9, Ducky.Blazor 10, Ducky.Reactive 16, Ducky.Draft 17, Ducky.Draft.Generators 17) and activeStage: 1. SpecTraceGate (§19): runs --list-tests on every .NET test project of Ducky.slnx (Ducky.E2E included); always fails when a manifest name is absent from docs/spec/SPEC.md or a §4 'Named tests' token is missing from the manifest; for active entries (stage <= activeStage) fails when a name is missing from any of its projects (Ducky.AotSmoke names excepted: the AotSmoke target checks them) and when an invariant other than INV-24 whose stage is active has no active entry in a covered project; --strict also fails on any inactive entry and checks all 32 invariants. One matching rule: a {Placeholder} matches \w+ at any position; a listed test matches on its method name before any '('.
- **Acceptance tests:**
  - Planted check: deleting a §4 name from tests.yaml fails SpecTraceGate
  - Planted check: renaming an active test fails SpecTraceGate
  - Planted check: an active invariant whose only active test is in Ducky.Concurrency.Tests fails SpecTraceGate
  - `--strict` fails while any entry is inactive
  - `Caching_{Step}_Cached` matches `Caching_Pipeline_Cached` and `Skeleton_{Project}_Smoke` matches `Skeleton_DuckyTests_Smoke`
- **Done:** Common DoD. Gate added to Ci; the stage and activeStage rules of P1 are documented in docs/spec/README.md.

#### M0-07 Pack, MinVer, package validation and PackageSmoke skeleton (M)

- **Stage:** 1
- **Depends on:** M0-03
- **Unblocks:** M0-11
- **Area:** `build/Build.Release.cs, test/Ducky.PackageSmoke, Ducky.PackageSmoke.slnx`
- **Invariants:** —
- **Scope:** MinVer via GlobalPackageReference (tag prefix v, MinVerMinimumMajorMinor 2.0, alpha.0); IsPackable=true only in the six shipping projects. Pack target: deletes artifacts/packages and artifacts/tombstone, `dotnet pack Ducky.slnx -c Release --no-build` (PackageOutputPath from Directory.Build.props, the tombstone into artifacts/tombstone), EnablePackageValidation on the library packages (not the tombstone; no baseline until GA), then fails unless artifacts/packages holds exactly the five library IDs plus five .snupkg at $(MinVerVersion) and artifacts/tombstone exactly Ducky.Generator.2.0.0.nupkg. PackageSmoke target (deletes artifacts/smoke-packages first) and Ducky.PackageSmoke.slnx (unlocked, CPM off, -p:DuckyVersion) with its own nuget.config (nuget.org plus artifacts/packages, artifacts/tombstone and the committed feed/, packageSourceMapping routing Ducky, Ducky.*, Ducky.Generator and Mutty only to the local feeds), RestorePackagesPath=artifacts/smoke-packages, committed fakes Ducky.Generator.1.0.292.nupkg and Ducky.Blazor.1.0.292.nupkg. Only the step-1 checks (§17.9 is staged): nuspec checks (Ducky: exactly the two Extensions abstractions; no exclude on any Ducky dependency, R-PKG-5; analyzers/dotnet/cs/Ducky.Generators.dll present; Ducky.Draft with zero dependencies, Ducky.Draft.Generators.dll and Ducky.Draft.targets; tombstone without dependency groups; no Ducky.Generator.*.nupkg in artifacts/packages; no bracketed exact range), the transitive scan for R-PKG-1 IDs, DUCKY900 with the fake 1.0.292 and with the 2.0.0 tombstone, DUCKY902 with the fake Ducky.Blazor 1.0.292. Minimal WASM, Server (implements Microsoft.AspNetCore.Http.IMiddleware, global using Ducky) and Blazor-only consumers compile with TreatWarningsAsErrors. Later consumers and assertions arrive with their features: generator consumers and DUCKY004 (M13-11), the ducky.js publish check (M6-01), [Persist]/[Prerender] slices (M13-04b), the Reactive consumer (M11-05), the Draft consumer and DUCKY901 (M10-07).
- **Acceptance tests:**
  - `PackageSmoke` green on the skeleton packages
  - Planted check: adding `exclude="Build,Analyzers"` to Ducky.Blazor's Ducky dependency fails PackageSmoke
  - Planted check: a PackageReference to xunit in src/Ducky fails the R-PKG-1 scan
  - Consumer referencing fake Ducky.Generator 1.0.292 fails with DUCKY900; consumer referencing the 2.0.0 tombstone fails with DUCKY900
  - Planted check: an extra nupkg in artifacts/packages fails Pack's exact-set check
  - Consumer referencing the fake Ducky.Blazor 1.0.292 fails with DUCKY902
- **Done:** Common DoD. Versions come from MinVer; package validation runs inside Pack.

#### M0-08 AotSmoke target (NativeAOT console) (S)

- **Stage:** 1
- **Depends on:** M0-03
- **Unblocks:** M0-11, M14-06
- **Area:** `build/Build.Aot.cs, test/Ducky.AotSmoke, Ducky.Aot.slnx`
- **Invariants:** INV-24
- **Scope:** test/Ducky.AotSmoke (PublishAot console) in its own Ducky.Aot.slnx, restored unlocked. A run prints one PASS <name>/FAIL <name> line per named assertion and exits non-zero on any FAIL; every printed name is a manifest name that appears in SPEC.md, so area stories extend existing assertions instead of printing new names (P8). First assertion `AotSmoke` (references Ducky). AotSmoke target (Linux): wasm-tools install, unlocked restore, publish -r linux-x64 -p:PublishAot=true, run, require only PASS lines and a printed PASS set equal to the active manifest entries mapped to Ducky.AotSmoke (no --list: SpecTraceGate never runs the binary). The trimmed AOT-WASM step joins at stage 18 (M14-06).
- **Acceptance tests:**
  - `AotSmoke`
  - Planted check: a FAIL line makes the target fail
  - Planted check: a printed PASS name that is not an active Ducky.AotSmoke manifest entry fails the target
- **Done:** Common DoD. Target runs in the `aot` workflow.

#### M0-09 E2E skeleton and ducky.js block-coverage gate (M)

- **Stage:** 1
- **Depends on:** M0-03, M0-02
- **Unblocks:** M0-11, M6-01
- **Area:** `build/Build.E2E.cs, test/Ducky.E2E`
- **Invariants:** —
- **Scope:** test/Ducky.E2E with Microsoft.Playwright (.NET, an xUnit v3 project on MTP, listed by --list-tests): harness/ static page, Kestrel static-file host, published-dll host fixture (--urls http://127.0.0.1:0, port from stdout, health poll, kill on teardown), browser selected by DUCKY_BROWSER, Playwright's clock installed before the module loads, and the ~60-line CDP precise-coverage helper (every page and context, ranges keyed to ducky.js, merged counts, fail on a zero merged block). Targets E2E (publishes only the samples that exist in Ducky.slnx, Chromium, MTP syntax, then the JS coverage gate) and E2EAllBrowsers (Firefox/WebKit functional). From day one the gate measures the one-export stub ducky.js of M0-02 through the Ducky.E2E skeleton spec, so it is never vacuous; the real module replaces the stub in M6-01.
- **Acceptance tests:**
  - `Skeleton_{Project}_Smoke` (the Ducky.E2E harness spec over the stub ducky.js) passes in Chromium
  - Planted check: an unexecuted block in the stub ducky.js fails the coverage gate
- **Done:** Common DoD. Target runs in the `e2e` workflow.

#### M0-10 Public API tracking and release-tracking targets (S)

- **Stage:** 1
- **Depends on:** M0-03
- **Unblocks:** M0-11
- **Area:** `build/Build.PublicApi.cs, src/*/PublicAPI.*.txt, src/*Generators/AnalyzerReleases.*.md`
- **Invariants:** —
- **Scope:** Confirm RS0016/RS0017 are errors in every packable project; AnalyzerReleases.Shipped/Unshipped.md in both generator projects. Targets ShipPublicApi (Unshipped to Shipped for PublicAPI and AnalyzerReleases, never touches the package-validation baseline) and SetBaseline (writes PackageValidationBaselineVersion={latest GA on nuget.org} into Directory.Build.props). Together with Pack's EnablePackageValidation this realises the requested 'PackageValidation' and 'PublicApi' targets under the spec's names. Each generator assembly's AnalyzerReleases lists exactly its own descriptors (RS2002 otherwise); runtime, MSBuild and obsolete IDs are covered by the Docs page check (M15-04).
- **Acceptance tests:**
  - Planted check: a public type without a PublicAPI.Unshipped.txt line fails Compile (RS0016)
  - `ShipPublicApi` moves lines on a scratch branch and leaves Directory.Build.props untouched
  - `SetBaseline` writes the property (dry run against a fake feed)
- **Done:** Common DoD.

#### M0-11 GitHub Actions generated by Fallout, and spike S-8 (M)

- **Stage:** 1
- **Depends on:** M0-04, M0-05, M0-06, M0-07, M0-08, M0-09, M0-10
- **Unblocks:** M0-12, M15-01, M16-01, M16-03
- **Area:** `.github/, build/Build.CI.cs, build/DuckyGitHubActionsAttribute.cs`
- **Invariants:** —
- **Scope:** DuckyGitHubActionsAttribute overrides GetJobs so each job is named after its workflow (S-8). Build.CI.cs attributes (§20.1) for ci (Ci), ci-cross-windows and ci-cross-macos (Test and ConcurrencyTest), e2e (E2E), aot (AotSmoke), mutation (MutationPr), nightly-mutation, nightly-e2e, nightly-props (nightly-audit is added in M16-01); the nightly attributes set EnableGitHubToken, WritePermissions Issues and ReadPermissions Contents/Actions. ConfigureSteps PreRun (wasm-tools for aot, Playwright deps for e2e); JobEnd step opens or updates an issue on nightly failure. VerifyWorkflows target (regenerate to temp, diff, unique job names). Docs target v0 = fence check only (no ```csharp outside docs/spec and docs/adr). Final Ci aggregate: Format, ExclusionGate, CoverageGate, ConcurrencyTest, SpecTraceGate, PackageSmoke, VerifyWorkflows, Docs. Hand-written pr-title.yml, renovate.json (Roslyn and R3 majors ignored, automerge minor/patch dev deps), CODEOWNERS, pull_request_template.md (links the story issue, red-green-refactor checklist, the story's stage and whether it completes a stage, P1).
- **Acceptance tests:**
  - Generated YAML: every job name equals its workflow name (S-8 closed)
  - Planted check: a hand edit to ci.yml fails VerifyWorkflows
  - Planted check: a fenced csharp block in docs/guides fails Docs
  - A PR to v2 reports ci, ci-cross-windows, ci-cross-macos, e2e, aot, mutation, pr-title, all green
- **Done:** Common DoD. Every required check reports on a real PR.

#### M0-12 Branch protection and first tag (S)

- **Stage:** 1
- **Depends on:** M0-11
- **Unblocks:** M1-01, M1-03, M10-01, M10-03
- **Area:** `build/protect-branch.sh`
- **Invariants:** —
- **Scope:** Idempotent build/protect-branch.sh (gh api, §20.2): PR required, 0 approvals, strict required checks ci, ci-cross-windows, ci-cross-macos, e2e, aot, mutation, pr-title; linear history, squash only, no force-push or deletion, conversation resolution, admins included; tag ruleset on v* (maintainer only). Owner runs it for v2 and pushes tag v2.0.0-alpha.0.
- **Acceptance tests:**
  - `Running` the script twice changes nothing the second time
  - A direct push to v2 is rejected
  - A PR with a red required check cannot be merged
- **Done:** Owner-run; protection verified. From here on every story lands behind the full gate set.

#### M0-13 Week-1 Blazor spike prototypes S-3, S-5, S-7 (M)

- **Stage:** 1
- **Depends on:** M0-01
- **Unblocks:** M5-02
- **Area:** `spikes/blazor, docs/spec/spikes.md`
- **Invariants:** —
- **Scope:** Throwaway prototypes under spikes/ (outside every solution and gate, exempt from TDD, §23), answering the platform questions before the Blazor steps are designed: S-3 a trimmed AOT WASM app persists and takes a byte[] seed with zero IL warnings (JsonTypeInfo-based API preferred, audited byte[] suppression as fallback); S-5 a Server app round-trips 2 MB from localStorage through IJSStreamReference without closing the circuit, and a pull export returning an empty Uint8Array yields a zero-length reference; S-7 WASM, Server and Auto apps print the answers to §23: root/store-scope identity of PersistentComponentState and IJSRuntime, AuthenticationStateProvider lifetime, a synchronous InvalidOperationException from the prerender import, ducky:seed delivery to a scoped TryTakeFromJson after pause/resume, RegisterOnPersisting(…, RenderMode.InteractiveAuto) in a single-mode and a both-modes app, and whether ConnectCircuit/ResumeCircuit raise AuthenticationStateChanged on a live store with DuckyScopes.NameIdentifier completing synchronously. Any 'no' enables the DuckyComponent hand-off fallback (§6.10) before M5-04 designs the gate.
- **Acceptance tests:**
  - docs/spec/spikes.md records the S-3, S-5 and S-7 prototype answers and the fallbacks they trigger
- **Done:** Only docs/spec/spikes.md is merged; the prototypes stay under spikes/ and no gate builds them. Regression tests at stage 18: WasmTrimmedSmoke, Persist_LargeSlice_OnServer_RoundTrips and the S-7 E2E tests.

#### M0-14 Week-1 spike prototypes S-4 and S-9 (S)

- **Stage:** 1
- **Depends on:** M0-05
- **Unblocks:** M1-14
- **Area:** `spikes/core, docs/spec/spikes.md`
- **Invariants:** —
- **Scope:** S-4: a dispatcher stub under spikes/ (a lock-protected queue with one drainer) runs a CsCheck SampleParallel property at 50 repetitions under 3 minutes; the tuned operation counts are recorded and re-checked against the real dispatcher in M1-14. S-9: the per-project split is designed up front (Mutation --project <name>), and the S-6 prototypes' per-project times give the first timeout-minutes of nightly-mutation and release-mutation.
- **Acceptance tests:**
  - docs/spec/spikes.md records the S-4 operation counts and the S-9 per-project timings
- **Done:** Only docs/spec/spikes.md and the timeout-minutes values are merged. S-4 re-runs at stage 2 (Linearizability_DispatchVsModel); S-9 is re-measured at stages 13 and 17.

### M1 — Core store

Slices, slice keys, snapshot, single-drainer dispatcher, drain catch-all and SafeLogger, per-store causal scope, failure router, builder and aggregated configuration errors, DI lifetimes, init buffer with zero middleware, core dispose, WhenIdleAsync, and TST-03 concurrency items 1-5, 7, 11-13 (S-4 regression).

#### M1-01 SliceKey derivation and validation (S)

- **Stage:** 2
- **Depends on:** M0-12
- **Unblocks:** M1-02
- **Area:** `src/Ducky/Slices/SliceKey.cs`
- **Invariants:** —
- **Scope:** SliceKey.FromType (§6.1): type name, nested prefix joined with '-', strip one Slice/Store/Reducers/Reducer suffix, kebab-case with acronyms. Key regex `^(@ducky/)?[a-z0-9]+(-[a-z0-9]+)*$`; @ducky/ allowed only for assemblies with Ducky.dll's public key token. Pure functions, internal.
- **Acceptance tests:**
  - `SliceKey_FromType_Table`
  - SliceKey_Validation_Table (non-normative: regex and reserved prefix cases)
- **Done:** Common DoD. Deletes the Ducky.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M1-02 Slice<TState> reducers with exact-type matching (S)

- **Stage:** 2
- **Depends on:** M1-01
- **Unblocks:** M1-04
- **Area:** `src/Ducky/Slices`
- **Invariants:** —
- **Scope:** Slice (private protected ctor, virtual cached Key, StateType), Slice<TState> (protected Initial read once, sealed StateType = typeof(TState), On<TAction> both overloads). On<T> throws on duplicate T, non-concrete T (abstract/interface/object), or after freeze. Freeze to FrozenDictionary keyed by exact runtime type (ADR-0008). Internal InitialState, CanHandle, TryReduce.
- **Acceptance tests:**
  - `DuplicateHandler_ThrowsAtConstruction`
  - `OnAfterFreeze_Throws`
  - `ExactTypeMatching_DerivedActionNotHandledByBaseHandler`
- **Done:** Common DoD; PublicAPI.Unshipped updated.

#### M1-03 Error model, library actions and runtime codes (S)

- **Stage:** 2
- **Depends on:** M0-12
- **Unblocks:** M1-04, M1-08
- **Area:** `src/Ducky/Errors, docs/diagnostics`
- **Invariants:** INV-31
- **Scope:** DuckyError, DuckyConfigurationException (Errors list), internal error catalogue building every Ducky runtime code (DUCKY300-309, 350, 351 as a DuckyConfigurationException, 353) with the four-part template (what, types/keys, fix, link, §8.3). Library records StoreInitialized, ReducerFailed, EffectFailed; DispatchLoopException, UnhandledActionException; enums DispatchResult and Origin. One docs/diagnostics/{code}.md page per code (prose only; snippets arrive in M15).
- **Acceptance tests:**
  - `EveryErrorCode_FollowsMessageTemplate`
- **Done:** Common DoD; every code has a docs page.

#### M1-04 StateSnapshot and Registry (S)

- **Stage:** 2
- **Depends on:** M1-02, M1-03
- **Unblocks:** M1-05, M3-03
- **Area:** `src/Ducky/Store/StateSnapshot.cs`
- **Invariants:** INV-07
- **Scope:** Frozen Registry (keys, by state type, by key, slices), StateSnapshot (object[] states by ordinal, ulong[] restored bitset, Version): Get<T> (DUCKY350 naming AddDuckyGenerated_{Asm}), TryGet, Get(key) (KeyNotFoundException), Keys, WasRestored<T>/WasRestored(key). Internal commit helper that clones only when a slot changed.
- **Acceptance tests:**
  - Snapshot_GetUnregisteredType_ThrowsDucky350 (non-normative)
  - Snapshot_CommitWithoutChange_ReturnsSameInstance (non-normative)
  - Snapshot_WasRestored_BitsetCopiedOnlyOnHydration (non-normative)
- **Done:** Common DoD.

#### M1-05 Single-drainer dispatcher and store facade (M)

- **Stage:** 2
- **Depends on:** M1-04
- **Unblocks:** M1-05b, M1-06, M1-08
- **Area:** `src/Ducky/Store`
- **Invariants:** INV-01, INV-02, INV-03, INV-07, INV-08, INV-10
- **Scope:** Dispatcher per §6.3 (Lock _gate, queue, _draining, Ids under _gate starting at 1 in enqueue order, _drainExited), Pending, Drain loop with empty-check and release in one critical section. Process steps 6-8: reduce into a scratch array, atomic commit (INV-08), Volatile.Write, complete TCS (RunContinuationsAsynchronously, never under the lock); a dispatch from a reducer is queued, never nested. DuckyStore facade: Dispatch, DispatchAsync, State, InitialState and Slices (IReadOnlyList<Slice> in registration order: registry data, no init, no materialization, readable after disposal), with internal construction for now. [LoggerMessage] Log class (EventIds 1000+). Reducer failure completes Failed (failure actions arrive in M1-07). The catch-all and SafeLogger come in M1-05b.
- **Acceptance tests:**
  - `ReducerThrow_InOneSlice_CommitsNoSliceForThatAction`
  - DispatchAsync_Reduced_CompletesAfterCommit (non-normative)
  - Dispatch_CallerBecomesDrainer_ReadYourWrites (non-normative)
  - `Reducers_DispatchFromReducer_QueuedNotNested`
  - `Snapshot_DispatchAsyncReduced_StateVersionAtLeastCommitVersion`
- **Done:** Common DoD; no user code or completion runs under _gate (code review checklist).

#### M1-05b Drain catch-all, BeforeProcessHook and SafeLogger (S)

- **Stage:** 2
- **Depends on:** M1-05
- **Unblocks:** M4-03c, M4-09
- **Area:** `src/Ducky/Store/Drain.cs, src/Ducky/Diagnostics/SafeLogger.cs`
- **Invariants:** INV-03
- **Scope:** Internal SafeLogger : ILogger in Ducky, catching every non-fatal exception from Log, IsEnabled and BeginScope, wrapped once where each component obtains its logger (it replaces any per-call 'swallows its own throw' code). Catch-all around Process: it completes p first, then logs EventId 1012 inside its own try/catch, so a fatal rethrow from a logging provider still leaves the action completed Failed, the next action drained and _drainExited completed. Internal IVT seam BeforeProcessHook on the store (an instance Action?, null in production), from which the tests throw.
- **Acceptance tests:**
  - `Drain_ProcessThrowsUnexpectedly_NextActionStillDrained`
  - `Drain_ProcessThrowsAndLogRethrowsFatal_ActionCompletedAndNextDrained`
  - `SafeLogger_ProviderThrows_NeverEscapes` (Ducky.Tests)
- **Done:** Common DoD.

#### M1-06 CausalScope and depth guard (S)

- **Stage:** 2
- **Depends on:** M1-05
- **Unblocks:** M1-07, M1-13
- **Area:** `src/Ducky/Store/CausalScope.cs`
- **Invariants:** INV-06
- **Scope:** Per-store causal scope: a private readonly AsyncLocal<Cause?> _causal instance field on the dispatcher (never static; a scope never crosses stores, §6.5) holding Seq, Depth, CorrelationId and InFailure, set in Process step 2 and restored in finally with _processingSeq reset to 0. Enqueue depth rule for every origin: null scope => depth 0 new chain; Seq == _processingSeq => child depth+1; otherwise async continuation depth 0 keeping correlation. Step 1: depth > MaxDispatchDepth => Dropped.
- **Acceptance tests:**
  - `SelfDispatchLoop_IsStoppedByDepthGuard`
  - CausalScope_NeverLeftSetInCallerContext (non-normative)
- **Done:** Common DoD.

#### M1-07 FailureRouter (S)

- **Stage:** 3
- **Depends on:** M1-06
- **Unblocks:** M1-10
- **Area:** `src/Ducky/Store/FailureRouter.cs`
- **Invariants:** INV-06, INV-12
- **Scope:** Core failure actions enqueued at depth 0 with the parent correlation and IsFailure; log-only when the current scope has InFailure. Depth-drop enqueues ReducerFailed(DispatchLoopException) so it is itself reduced. Reducer throw now enqueues exactly one ReducerFailed. The loop-guard ReducerFailed takes p.CorrelationId/p.InFailure explicitly, never the ambient scope.
- **Acceptance tests:**
  - `ReducerFailed_ExactlyOnce`
  - `FailureWhileReducingFailure_LogsOnly`
  - `LoopGuardFailure_IsReducedNotDropped`
  - `LoopGuardFailure_ReactorRedispatches_Terminates`
- **Done:** Common DoD.

#### M1-08 DuckyBuilder, AddDucky and aggregated validation (M)

- **Stage:** 2
- **Depends on:** M1-05, M1-03
- **Unblocks:** M1-09, M2-08, M4-06
- **Area:** `src/Ducky/Configuration`
- **Invariants:** INV-31
- **Scope:** DuckyBuilder (§5.1): Services, Lifetime backed by ServiceLifetime?, MaxDispatchDepth, ThrowOnUnhandledAction, InitBufferCapacity, InitTimeout, DisposeTimeout, AddSlice (throwaway instance in try/catch => DUCKY307/308), AddValidation (no AddError), internal settable IsBrowser (single RS0030-justified read). AddDucky throws only DUCKY300; everything else (DUCKY301-305, custom rules) is aggregated into one DuckyConfigurationException at first store resolution. UseJson/RequireJsonTypeInfo/AddEffect/Use are added by later stories. Later packages register their configuration codes as AddValidation rules evaluated on the final composed options at first resolution.
- **Acceptance tests:**
  - `Build_WithFiveMisconfigurations_ReportsAllFive`
  - `Build_SelfContainedAndDiDependentErrors_ReportedTogether`
  - `AddDucky_Twice_Throws`
- **Done:** Common DoD.

#### M1-09 DI lifetimes and store identity (S)

- **Stage:** 2
- **Depends on:** M1-08
- **Unblocks:** M1-10, M4-05b
- **Area:** `src/Ducky/Configuration/Registration.cs`
- **Invariants:** INV-22
- **Scope:** AddDucky registers IStore through DuckyStore.Create(sp, config), IDispatcher forwarding, each slice type resolving to the store-owned instance, TryAddSingleton(TimeProvider.System). Lifetime resolved after configure: IsBrowser ? Singleton : Scoped; Transient => DUCKY301. Store-owned AsyncServiceScope for effects/middleware. Singleton on a non-browser host logs Warning 1004.
- **Acceptance tests:**
  - `BrowserLifetime_RootAndScopes_ResolveOneStore`
  - `ServerLifetime_TwoScopes_TwoStores`
  - `ServerLifetime_HandlerScope_GetsDistinctStore`
  - `Lifetime_Default_FollowsIsBrowserSetInConfigure`
- **Done:** Common DoD.

#### M1-10 Init buffer, StoreInitialized and Restore (M)

- **Stage:** 3
- **Depends on:** M1-07, M1-09
- **Unblocks:** M1-11, M4-06
- **Area:** `src/Ducky/Store/Init`
- **Invariants:** INV-04, INV-13
- **Scope:** StoreState machine, InitCoordinator happy path with no middleware (NotStarted to Completed, Start never under _gate), init buffer for Local/Effect before Ready, MarkReady appends StoreInitialized then the buffer under one lock, shared init task, InitializeAsync(token) = WaitAsync. Init auto-starts on Dispatch, DispatchAsync and State reads. IStore.Restore(values, origin) for Hydration/CrossTab/DevTools (internal HydrateSlices, TState values; JsonElement in M4-06), WasRestored set only by Hydration, ArgumentOutOfRangeException for Local/Effect/System. The shared init TCS travels in StoreInitialized's Pending, so InitializeAsync completes when StoreInitialized is processed (or Failed, or at disposal), never from MarkReady.
- **Acceptance tests:**
  - `Init_AutoStartsOnFirstDispatch`
  - `Init_StoreInitializedExactlyOnce`
  - `PreInitDispatches_AreBufferedAndReplayedInOrder`
  - `Restore_BypassesInitBuffer`
  - `InitBuffer_ActionsAfterReady_FollowBufferedActions`
- **Done:** Common DoD.

#### M1-11 Core dispose (M)

- **Stage:** 3
- **Depends on:** M1-10
- **Unblocks:** M1-12, M4-01
- **Area:** `src/Ducky/Store/Dispose.cs`
- **Invariants:** INV-10, INV-13, INV-29
- **Scope:** DisposeAsync publishes its TCS through Interlocked.CompareExchange before step 1 and returns that task to every caller, re-entrant ones included; it never faults. Step 1 under _gate: mark Disposed (the volatile _disposal read by later call-time checks), detach pendings and idle waiters, complete them Disposed outside the lock together with the shared init task if never Ready; step 2 cancels the lifetime CTS (callbacks may overlap an in-flight drain; AggregateException logged, EventId 1013); step 3 awaits _drainExited bounded by DisposeTimeout (Warning 1011): DisposeAsync completes at the bound and later phases chain on _drainExited. Sync Dispose() is `_ = DisposeAsync();` plus Warning 1010. Post-dispose behaviour of Dispatch, DispatchAsync, InitializeAsync, State and Restore.
- **Acceptance tests:**
  - `Dispose_Twice_IsNoOp`
  - `SyncDispose_LogsWarningAndDelegates`
  - `InitializeAsync_DisposedBeforeReady_Completes`
  - `Dispose_ReentrantFromLifetimeCallback_RunsOnce`
- **Done:** Common DoD.

#### M1-12 WhenIdleAsync (S)

- **Stage:** 3
- **Depends on:** M1-11
- **Unblocks:** M2-01, M3-01, M4-01, M9-03
- **Area:** `src/Ducky/Store/Idle.cs`
- **Invariants:** INV-13
- **Scope:** Idle waiters (Ready, queue empty, not draining; effects join in M2-03), TakeIdleWaitersIfIdleLocked detached under _gate and completed outside, auto-start of init, only the caller's token cancels, idle waiters are completed in dispose step 1, after the lock is released.
- **Acceptance tests:**
  - WhenIdle_CompletesAfterDrainExit (non-normative)
  - WhenIdle_CallerTokenCancels_OnlyThatWait (non-normative)
  - WhenIdle_StartsInit (non-normative)
- **Done:** Common DoD.

#### M1-13 Concurrency suite I (M)

- **Stage:** 2
- **Depends on:** M1-06
- **Unblocks:** M1-14, M2-06, M3-04, M4-04
- **Area:** `test/Ducky.Concurrency.Tests`
- **Invariants:** INV-01, INV-02, INV-03, INV-04, INV-05, INV-07
- **Scope:** Replace the Ducky.Concurrency.Tests skeleton test (the project exists since M0-02, outside coverage, run by ConcurrencyTest): Repeat MemberData driven by DUCKY_REPEAT (default 50), 10 s WaitAsync with a thread-dump failure, Task.Run with Barrier/TCS choreography only. TST-03 items 1-5 and 7 (item 8 moved to M4-04, after dispose).
- **Acceptance tests:**
  - `Dispatch_FromNThreads_AppliesEveryActionExactlyOnce`
  - `Reducers_NeverRunConcurrently`
  - `Dispatch_PreservesPerProducerOrder`
  - `Dispatch_AfterDrainRelease_NoStrandedAction`
  - `Dispatch_OwnerBlocksOnCrossThreadDispatch_DoesNotDeadlock`
  - `Snapshot_VersionStrictlyMonotonic`
  - `Snapshot_NeverTorn`
  - `Snapshot_AfterConcurrentReduces_IsNeverStale`
- **Done:** Common DoD; 50 repetitions green locally and on all three OS jobs. Deletes the Ducky.Concurrency.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M1-14 Concurrency suite II and linearizability (spike S-4) (M)

- **Stage:** 2
- **Depends on:** M1-13, M0-14
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Concurrency.Tests/Linearizability`
- **Invariants:** INV-01, INV-02, INV-03, INV-04, INV-07, INV-10
- **Scope:** TST-03 items 11-13. CsCheck SampleParallel over awaited DispatchAsync, ReadState and (later) Selection.Value against a sequential reducer model; sync Dispatch checked separately for exactly-once and per-producer order after WhenIdleAsync. S-4 regression: re-run against the real dispatcher, resetting the operation counts measured by the M0-14 prototype so the suite stays under 3 minutes at 50 repetitions.
- **Acceptance tests:**
  - `DispatchAsync_QueuedBehindOtherThreadsDrain_CompletesAfterReduce`
  - `DispatchAsync_ContinuationNeverRunsInlineOnDrainer`
  - `Linearizability_DispatchVsModel`
- **Done:** Common DoD; S-4 budget recorded in docs/spec/spikes.md.

### M2 — Effects

Effect<T>, EffectGroup, EffectContext and ctx.Run; Merge/Switch/Exhaust/Queue with the slot compare-and-remove rule, failure observation, quiescence with CountedForIdle, the run registry and effect-aware dispose (phase 5b), the policy matrix and model-based property (INV-11), keyed DUCKY309.

#### M2-01 Effect<T>, Merge policy, EffectContext and materialization (M)

- **Stage:** 4
- **Depends on:** M1-12, M4-01
- **Unblocks:** M2-02, M2-08, M3-01b, M4-01b, M4-08
- **Area:** `src/Ducky/Effects`
- **Invariants:** INV-11, INV-31
- **Scope:** Effect (LongRunning), Effect<TAction> (Policy, ConcurrencyKey, Handle), Concurrency enum, AddEffect<[DAM]T>() and AddEffect(instance) (never disposed): one registration per effect type, the instance wins whatever the order and the replaced type is excluded from DUCKY309. Materialization on first store use via Lazy(ExecutionAndPublication) with ActivatorUtilities from the store scope; a throwing constructor caches one DuckyConfigurationException (DUCKY353) rethrown by every call. Exact-type effect index; Process step 11 starts runs inline to the first await. EffectContext.Trigger carries the ActionContext of M4-01 (P6). EffectContext (live State, Trigger, Time, Dispatch/DispatchAsync with Origin.Effect). Merge runs on the store-lifetime token. Store owns and disposes store-created effects.
- **Acceptance tests:**
  - `Effect_RegisteredInDi_StillStoreOwnedPerStore`
  - `Effect_PolicyCancellation_NeverEffectFailed`
  - `Materialization_CtorThrows_EveryCallRethrowsSameConfigurationException`
  - Effect_AddEffectInstance_NeverDisposed (non-normative)
  - `AddEffect_InstanceAndType_SameEffect_OneRunner`
- **Done:** Common DoD.

#### M2-02 Effect failure observation (S)

- **Stage:** 4
- **Depends on:** M2-01
- **Unblocks:** M2-03, M2-09
- **Area:** `src/Ducky/Effects/EffectRun.cs`
- **Invariants:** INV-11, INV-12
- **Scope:** async RunAsync observation: an OperationCanceledException is cancellation iff its token is our run token or the run token is cancelled; anything else (including a synchronous throw before the first await) goes to FailureRouter as EffectFailed at depth 0, or is logged only when the trigger was a failure action. The observer never uses the cancellable token.
- **Acceptance tests:**
  - `Effect_ForeignOce_IsEffectFailed`
  - `Effect_FaultDuringDispose_StillObserved`
  - `EffectOnEffectFailed_ThrowsSync_LogsOnly_NoLoop`
  - `EffectOnEffectFailed_ThrowsAfterAwait_LogsOnly`
- **Done:** Common DoD.

#### M2-03 Quiescence, per-store effect-run scope and CountedForIdle (M)

- **Stage:** 4
- **Depends on:** M2-02
- **Unblocks:** M2-03b, M2-04, M2-07
- **Area:** `src/Ducky/Effects/Quiescence.cs`
- **Invariants:** INV-10, INV-11, INV-13
- **Scope:** _runningEffects (non-LongRunning) incremented under _gate when RunAsync is created and decremented last in the run's finally (after the slot compare-and-remove, Done and CTS disposal), completing idle waiters at zero; WhenIdleAsync now waits for runs. Per-store private readonly AsyncLocal<EffectRunToken?> _effectRun instance field, set only by RunAsync; Process step 2 sets it to null for each action and restores it in finally. EffectRunToken.CountedForIdle: WhenIdleAsync (hence TestStore.Settled) throws InvalidOperationException synchronously from a non-LongRunning run of the same store instead of waiting for itself.
- **Acceptance tests:**
  - `WhenIdle_NeverCompletesWhileEffectCausedActionPending`
  - `WhenIdle_IgnoresLongRunningEffects`
  - `WhenIdle_FromNonLongRunningEffectRun_ThrowsNotHangs`
- **Done:** Common DoD.

#### M2-03b Run registry and effect-aware dispose (phase 5b) (M)

- **Stage:** 4
- **Depends on:** M2-03, M4-01
- **Unblocks:** M4-05b, M6-11
- **Area:** `src/Ducky/Store/Dispose.cs, src/Ducky/Effects/RunRegistry.cs, test/Ducky.Concurrency.Tests/Dispose`
- **Invariants:** INV-11, INV-29
- **Scope:** Per-store run registry _runs covering every policy (LongRunning included), filled by the drainer and emptied by a continuation. Every EffectRunToken is a new object (run id + token) with a DisposeCalled TCS completed by any DisposeAsync call made from that run; dispose step 4 awaits Task.WhenAny(run.Task, run.Token.DisposeCalled.Task) per run, bounded by DisposeTimeout and chained past it; phase 5b (effects, reactive effects, store scope) runs after phase 5a and the run wait, so a hung run delays only effect and scope disposal. Process step 11 checks the store-lifetime token once, before any run, and starts none once disposal began (Debug log).
- **Acceptance tests:**
  - `Dispose_AwaitsMergeAndLongRunningRuns`
  - `Dispose_SecondCallFromEffectRun_DoesNotWaitForItself`
  - `Dispose_FromAfterReduce_NoEffectStartedForThatAction`
  - `Dispose_FromRacingEffectContinuation_WaitsForDrain` (Ducky.Concurrency.Tests)
- **Done:** Common DoD.

#### M2-04 Switch policy, EffectRunToken and the run check (M)

- **Stage:** 4
- **Depends on:** M2-03
- **Unblocks:** M2-05, M2-07, M9-01b
- **Area:** `src/Ducky/Effects/Policies/Switch.cs`
- **Invariants:** INV-11
- **Scope:** EffectRunner with ConcurrentDictionary<object, Slot> (NullKey sentinel), immutable Slot per start, AddOrUpdate install with factories that only return the pre-built slot and record prev, compare-and-remove of the own slot, CTS ownership rule (CancelAsync on the drainer, dispose in a continuation, Warning 1014 on callback fault). EffectRunToken (a new object per run) checked at call time and stored in Pending.Run for Process step 3. EffectContext.Dispatch/DispatchAsync first read the store's disposal state (one volatile read) and complete Disposed, uncounted, once disposal began; only then does the run check drop a superseded Switch run's dispatch (Dropped, counted); step 3 applies the same precedence. Internal SlotCount.
- **Acceptance tests:**
  - `Switch_OldRunCompletion_DoesNotRemoveNewSlot`
  - `Switch_SupersededRunIgnoringToken_CannotDispatch`
  - `Switch_StaleResultQueuedBeforeSupersession_IsDropped`
  - `Switch_CancelCallbackThrows_DrainerSurvives`
  - `Effect_KeyedSwitch_ProductB_DoesNotCancelProductA`
- **Done:** Common DoD; every test asserts SlotCount == 0 after WhenIdleAsync.

#### M2-05 Exhaust and Queue policies (M)

- **Stage:** 4
- **Depends on:** M2-04
- **Unblocks:** M2-06, M4-09
- **Area:** `src/Ducky/Effects/Policies/ExhaustQueue.cs`
- **Invariants:** INV-11
- **Scope:** Exhaust: TryAdd running marker, drop with Debug log and a drop hook for ducky.effect.dropped. Queue: Done TCS (RunContinuationsAsynchronously), run awaits prev.Done.Task.WaitAsync(runToken), handler never invoked when cancelled while waiting, finally compare-and-removes its own slot then completes its own Done. The Exhaust run token is the store-lifetime token, like Merge. Normative finally order: slot compare-and-remove, Done, CTS disposal, then the idle decrement; the Queue test completes its last run on a pool thread through a RunContinuationsAsynchronously gate.
- **Acceptance tests:**
  - `Exhaust_SecondWhileRunning_DroppedAndCounted`
  - `Queue_ThreeRuns_NeverOverlap_SlotEmptyAtIdle`
  - `Queue_DisposeFromRunWithQueuedSuccessor_CompletesWithoutTimeout`
  - `Queue_AfterDispose_HandlerNeverInvoked`
- **Done:** Common DoD.

#### M2-06 Policy matrix and model-based effect property (M)

- **Stage:** 4
- **Depends on:** M2-05, M1-13
- **Unblocks:** M3-05, M4-10
- **Area:** `test/Ducky.Tests/Effects/Matrix, test/Ducky.Concurrency.Tests/Effects`
- **Invariants:** INV-11
- **Scope:** 4 policies x {global, key A, key B} x {complete, fault, foreign OCE, policy cancel, store dispose} theory with TCS-gated fakes (dispatched actions, EffectFailed count, SlotCount == 0 at idle). CsCheck model property through Property.Check (fixed seeds when gated) over random streams and completion orders. Concurrency: install racing completion-remove.
- **Acceptance tests:**
  - EffectPolicyMatrix (theory, 60 cases; non-normative name for the §17.4 matrix)
  - `EffectPolicies_ModelBased`
  - `Queue_InstallRacesCompletionRemove_HandlerRunsOncePerAction`
- **Done:** Common DoD.

#### M2-07 EffectGroup and ctx.Run (S)

- **Stage:** 4
- **Depends on:** M2-03, M2-04
- **Unblocks:** M16-04
- **Area:** `src/Ducky/Effects/EffectGroup.cs`
- **Invariants:** INV-11, INV-31
- **Scope:** EffectGroup.On<T>(handler, policy, key) constructor-only; duplicate or non-concrete T throws, surfacing as DUCKY353 naming DUCKY308/307. EffectContextExtensions.Run (started, work, succeeded, failed; our-token cancellation propagates, HttpClient timeout is a failure). Process step 11 computes each effect's ConcurrencyKey or EffectGroup key inside that effect's try/catch: a throw routes EffectFailed for that effect only and the others still start.
- **Acceptance tests:**
  - `EffectContext_Run_HttpTimeout_DispatchesFailed`
  - EffectGroup_DuplicateOn_Ducky353NamesDucky308 (non-normative)
  - EffectGroup_PolicyPerHandler (non-normative)
  - `Effect_ConcurrencyKeyThrows_EffectFailedOtherEffectsStart`
- **Done:** Common DoD.

#### M2-08 Constructor dependency check (DUCKY309) (S)

- **Stage:** 4
- **Depends on:** M2-01, M1-08
- **Unblocks:** M4-05, M11-01
- **Area:** `src/Ducky/Configuration/CtorCheck.cs`
- **Invariants:** INV-31
- **Scope:** At first resolution, for each registered effect (middleware in M4-05, reactive in M11-01): take the ActivatorUtilitiesConstructor or the public constructors of the [DAM] type; pass if one candidate has every non-default parameter resolvable per IServiceProviderIsService (IStore, IDispatcher, slice and SliceStore types count as resolvable). Skip with a Debug log when the container has no IServiceProviderIsService. [FromKeyedServices(key)] parameters are checked with IServiceProviderIsKeyedService.IsKeyedService (skipped with the Debug log when absent); [ServiceKey] parameters are unresolvable. Public DuckyBuilder.RequireResolvableConstructor([DAM] Type type, string requiredBy) queues any type for DUCKY309 (AddReactiveEffect uses it).
- **Acceptance tests:**
  - `Build_EffectCtorDependencyMissing_ReportedWithOtherErrors`
  - CtorCheck_NoIsServiceProvider_SkippedWithDebugLog (non-normative)
  - `Build_EffectCtorKeyedDependency_NotReported`
  - `Build_EffectCtorKeyedDependencyMissingKey_Reported`
- **Done:** Common DoD.

#### M2-09 Effect-driven causal depth (S)

- **Stage:** 4
- **Depends on:** M2-02
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Tests/CausalDepth`
- **Invariants:** INV-06
- **Scope:** Single-threaded tests in Ducky.Tests (covered; §17.3 keeps the INV-06 depth tests out of Ducky.Concurrency.Tests) with FakeTimeProvider: synchronous ping-pong stops at 64, a polling effect runs 200 ticks through Task.Delay(TimeSpan, TimeProvider), a continuation driven by FakeTimeProvider.Advance on the test thread after the drain exited starts at depth 0 with the old correlation.
- **Acceptance tests:**
  - `EffectPingPong_SynchronousPrefix_IsStoppedByDepthGuard`
  - `PollingEffect_RunsBeyondMaxDepthTicks`
  - `AsyncDispatchWhileStoreIdle_StartsNewChainAtDepthZero`
- **Done:** Common DoD.

### M3 — Selectors and subscriptions

SubscriberList, IStore.Select, Selection<T>, the typed fast path, Selector.Define memoization (INV-09, INV-21), subscriber isolation in covered tests, and the AOT smoke for the core.

#### M3-01 SubscriberList, IStore.Select and Selection<T> (M)

- **Stage:** 5
- **Depends on:** M1-12
- **Unblocks:** M3-01b, M3-02, M3-04, M4-01b, M4-10
- **Area:** `src/Ducky/Selectors/Subscriptions`
- **Invariants:** INV-09, INV-13, INV-21
- **Scope:** SubscriberList (ImmutableArray swapped with ImmutableInterlocked, idempotent unsubscribe, snapshot iteration, each call isolated and logged). IStore.Select order (§6.8): start init, add with last = Unset, read State after the add and CAS, return Selection; drainer CAS-or-compare then onChange. Internal AfterSubscribeHook. Selection<T>.Create/Value (evaluate on read)/implicit/ToString/idempotent Dispose; inert after store dispose. Process step 10 notify.
- **Acceptance tests:**
  - `Select_CommitBetweenAddAndRead_InstalledByDrainer`
  - `Select_DedupesOnProjectedValue`
  - Select_AfterDispose_InertReadsLastSnapshot (non-normative)
- **Done:** Common DoD.

#### M3-01b Subscriber isolation and re-entrancy in covered tests (S)

- **Stage:** 5
- **Depends on:** M3-01, M2-01
- **Unblocks:** M4-10
- **Area:** `src/Ducky/Selectors/Subscriptions/Notify.cs, test/Ducky.Tests/Subscriptions`
- **Invariants:** INV-02, INV-09
- **Scope:** Process step 10 runs each subscription's selector, comparer and onChange in its own try/catch (last unchanged, subscription kept, logged), so one throw never skips the others or the effect start. A volatile _disposed flag on Subscription, set by Dispose and checked right before the selector (a running callback may finish after Dispose returns; Dispose never waits), and the internal IVT seam BeforeNotifyHook between capture and iteration. The INV-09 subscriber tests live here, in a covered project (§17.3).
- **Acceptance tests:**
  - `Subscriber_UnsubscribesItselfDuringNotify`
  - `Subscriber_Throws_OthersStillNotified_StaysSubscribed`
  - `Subscriber_SelectorThrows_OthersNotifiedAndEffectsStillStart`
  - `Subscriber_DisposedBeforeNotify_NotInvoked`
  - `Dispatch_ReentrantFromSubscriberAndEffectPrefix_EachProcessedOnce`
- **Done:** Common DoD.

#### M3-02 Typed Select fast path (S)

- **Stage:** 5
- **Depends on:** M3-01
- **Unblocks:** M16-04
- **Area:** `src/Ducky/Selectors/StoreExtensions.cs`
- **Invariants:** INV-21
- **Scope:** StoreExtensions.Select<TState,T>: skip the projector when the TState slot is ReferenceEquals to the cached one; one immutable Entry(slot, result) swapped with Volatile.Write.
- **Acceptance tests:**
  - `TypedSelect_FastPath_SkipsProjectorWhenSlotUnchanged`
- **Done:** Common DoD.

#### M3-03 Selector.Define and SelectorDef memoization (M)

- **Stage:** 5
- **Depends on:** M1-04
- **Unblocks:** M3-05, M13-06
- **Area:** `src/Ducky/Selectors/Definitions`
- **Invariants:** INV-21
- **Scope:** All Selector.Define overloads (1-4 inputs, with and without TArg), SelectorDef<T> (Evaluate, Create) and SelectorDef<TArg,T> (Evaluate, Create(argument, comparer)); closures over an immutable Entry(inputs, result); ReferenceEquals for references, EqualityComparer.Default for value types; no static cache.
- **Acceptance tests:**
  - `Memoized_EqualsUnmemoized`
  - `SameInputRefs_SameOutputInstance`
  - `ArgSelector_ArgChange_Recomputes`
  - `TwoStores_ShareDefinition_NoSharedCache`
- **Done:** Common DoD.

#### M3-04 Subscriber concurrency tests (S)

- **Stage:** 5
- **Depends on:** M3-01, M1-13
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Concurrency.Tests/Subscriptions`
- **Invariants:** INV-05, INV-09
- **Scope:** TST-03 item 10 and the Select half of item 18 in Ducky.Concurrency.Tests (the other INV-09 subscriber tests are single-threaded and live in M3-01b).
- **Acceptance tests:**
  - `Subscriber_ReadsStateAndDispatchesDuringNotify_NoDeadlock`
  - `Select_ConcurrentWithCommit_NoLostOnChange`
- **Done:** Common DoD.

#### M3-05 AOT smoke: store, effects, selectors (S)

- **Stage:** 5
- **Depends on:** M2-06, M3-03
- **Unblocks:** M4-07, M9-06, M11-04, M12-05
- **Area:** `test/Ducky.AotSmoke`
- **Invariants:** INV-24
- **Scope:** Extend the `AotSmoke` assertion (no new PASS names, P8) with dispatch/reduce, all four policies, Select and SelectorDef.
- **Acceptance tests:**
  - `AotSmoke` (now also covering dispatch/reduce, the four policies, Select and SelectorDef)
- **Done:** Common DoD; aot job green with zero IL warnings.

### M4 — Middleware, init lifecycle and core contract

Middleware hooks, veto and ActionContext, the Process failure rule, DispatchSystem, middleware init with timeout and the QueueWorkItem overflow abort, disposal during init with per-middleware bounds, materialization versus disposal, the JSON gateway, action names, unhandled check, tracing/metrics with SafeTelemetry and the never-fault contract. The Ducky core API is complete after M4.

#### M4-01 Middleware hooks, veto and dispose ordering (M)

- **Stage:** 3
- **Depends on:** M1-12, M1-11
- **Unblocks:** M2-01, M2-03b, M4-01b, M4-02, M4-03c, M4-05, M4-08
- **Area:** `src/Ducky/Middleware`
- **Invariants:** INV-12, INV-29
- **Scope:** Middleware base (Store and a protected read-only DisposeTimeout attached before InitializeAsync, InvalidOperationException before), Use<[DAM]T>() idempotent with first-registration position. Immutable ActionContext (Action, ActionType, Origin, Depth, Id, CorrelationId, PreviousState, State, ChangedKeys) is introduced here (P6). Process steps 4 (MayDispatch only for Local/Effect; a veto or a throwing MayDispatch ends Process), 5 (BeforeReduce) and 9 (AfterReduce, each middleware isolated and logged). Dispose phase 5a: middleware in reverse registration order after drain exit, each DisposeAsync in its own try/catch (Error 1015), chained after _drainExited when step 3 timed out; lifetime callbacks that throw never stop it.
- **Acceptance tests:**
  - `Veto_NeverAppliesToSystemOrRestoreOrigins`
  - `Dispose_FromReducer_MiddlewareDisposedAfterDrainExit`
  - `Dispose_DrainTimeout_MiddlewareDisposedAfterDrainExit`
  - `Dispose_FromDrainer_DoesNotDeadlock`
  - `Dispose_MiddlewareDisposeThrows_OthersStillDisposed`
  - `Dispose_ThrowingLifetimeCallback_CompletesAndDisposesMiddleware`
  - Middleware_AfterReduceThrows_OthersStillRun (non-normative)
- **Done:** Common DoD.

#### M4-01b Process failure rule: effects react to the action, not to the commit (S)

- **Stage:** 5
- **Depends on:** M4-01, M2-01, M3-01
- **Unblocks:** M4-10, M11-01b
- **Area:** `src/Ducky/Store/Process.cs, test/Ducky.Tests/Pipeline`
- **Invariants:** INV-08, INV-13
- **Scope:** The general rule of §6.4: a veto, or a throwing MayDispatch, ends Process; a BeforeReduce or reducer failure commits nothing, completes Failed, skips steps 7, 10 and 12, and still runs step 9 (State == PreviousState, empty ChangedKeys) and step 11, so StoreInitialized's load effects always start.
- **Acceptance tests:**
  - `BeforeReduceThrows_NoCommitNoNotify_EffectsStart`
  - `ReducerThrowsOnStoreInitialized_LoadEffectAndReactiveHostStillStart` (Ducky.Tests; the Ducky.Reactive.Tests twin is M11-01b)
- **Done:** Common DoD.

#### M4-02 DispatchSystem (S)

- **Stage:** 3
- **Depends on:** M4-01
- **Unblocks:** M4-03
- **Area:** `src/Ducky/Middleware/DispatchSystem.cs`
- **Invariants:** INV-06, INV-12
- **Scope:** Middleware.DispatchSystem(action, isFailure): Origin.System, bypasses buffer and veto, normal depth rule, isFailure marks a failure action.
- **Acceptance tests:**
  - `DispatchSystemLoopFromAfterReduce_IsStoppedByDepthGuard`
  - DispatchSystem_IsFailure_FailuresUnderItLogOnly (non-normative)
- **Done:** Common DoD.

#### M4-03 Middleware init, timeout and overflow abort (M)

- **Stage:** 3
- **Depends on:** M4-02
- **Unblocks:** M4-03b, M4-03c, M4-04, M4-10
- **Area:** `src/Ducky/Store/Init`
- **Invariants:** INV-05, INV-13
- **Scope:** InitCoordinator per §6.7: Start checks the state before each InitializeAsync and stops after Retire; each call is `try { t = mw.InitializeAsync(tok).AsTask(); } catch (Exception ex) { t = Task.FromException(ex); }`, appended to the per-middleware InitTasks (by registration index) and observed individually; CAS Starting to Running; _prefixDone (RunContinuationsAsynchronously) completed whatever the CAS outcome; InitTimeout armed on TimeProvider; WhenAll with Error logs; Complete. When every started InitializeAsync returned a completed task, steps 4-5 run synchronously and the triggering Dispatch returns after its action is reduced. Abort is one CAS Running to Completed (dispose timer, Cancel _initCts outside locks with AggregateException log 1013, StoreInitAborted once, MarkReady). The overflow path is M4-03b, disposal during init M4-03c.
- **Acceptance tests:**
  - `Init_MiddlewareSyncPrefixesAllRunBeforeStartReturns`
  - `Init_HangingMiddleware_TimesOutAndReleasesBuffer`
  - `Init_Completed_TimerNeverAborts`
  - `Init_AbortCallbackThrows_StoreStillReady`
  - `Init_MiddlewareAwaitsDispatchAsync_TimesOutNotHangs`
  - `Init_MiddlewareAwaitsWhenIdle_TimesOutNotHangs`
  - `Init_MiddlewareInitializeThrowsSynchronously_StoreStillReady`
  - `Init_AllInitsCompleteSynchronously_FirstDispatchReducedInline`
- **Done:** Common DoD.

#### M4-03b Overflow abort through QueueWorkItem (S)

- **Stage:** 3
- **Depends on:** M4-03
- **Unblocks:** M4-04, M4-10, M6-06
- **Area:** `src/Ducky/Store/Init/Overflow.cs`
- **Invariants:** INV-05, INV-13
- **Scope:** RequestOverflowAbort() queues Abort(BufferOverflow) through the store's internal QueueWorkItem seam (an instance delegate defaulting to ThreadPool.UnsafeQueueUserWorkItem) from Enqueue and Start step 3, only from Running and once (a flag), so the caller never blocks and repeated overflow enqueues stay one volatile read; ordering and 'nothing dropped' unchanged. Ducky.Tests replaces the seam with a capturing queue to run the abort at a chosen point.
- **Acceptance tests:**
  - `Init_BufferOverflow_AbortsInitDropsNothing`
  - `Init_OverflowDuringSyncPrefix_AbortsFromRunning`
  - `Init_OverflowAbort_NeverRunsOnCaller`
  - `Init_OverflowTwiceBeforeQueuedAbortRuns_QueuedOnce`
  - `Init_QueuedOverflowAbortRunsAfterComplete_NoOp`
- **Done:** Common DoD.

#### M4-03c Disposal during init and per-middleware dispose bounds (M)

- **Stage:** 3
- **Depends on:** M4-03, M4-01, M1-05b
- **Unblocks:** M4-10, M6-11
- **Area:** `src/Ducky/Store/Dispose.cs, src/Ducky/Store/Init/Retire.cs`
- **Invariants:** INV-13, INV-29
- **Scope:** InitCoordinator.Retire() in dispose step 1 (it returns _prefixDone.Task when it replaced Starting), so the init timer never fires after disposal and no middleware init starts after it. Each middleware's dispose waits only for its own init task; one whose init wait expired has its DisposeAsync chained on that task alone while phase 5a moves on; each DisposeAsync is awaited with WaitAsync(DisposeTimeout, TimeProvider), Warning EventId 1016 on expiry, then the next middleware. DisposeAsync completes once every phase finished or reached its bound. A throwing logging provider can stop neither MarkReady nor disposal (SafeLogger).
- **Acceptance tests:**
  - `Dispose_DuringInit_TimerNeverFires_NoAbortLog`
  - `Dispose_MiddlewareInitInFlight_DisposeAsyncAfterInitTaskEnds`
  - `Dispose_DuringStartSyncPrefix_AwaitsStartedInitsAndStartsNoMore`
  - `Dispose_InitAwaitingWhenIdle_MiddlewareStillDisposed`
  - `Dispose_HungMiddlewareDispose_OthersDisposedAndDisposeAsyncCompletes`
  - `Lifecycle_ThrowingLoggerProvider_StoreReadyAndDisposeComplete`
- **Done:** Common DoD.

#### M4-04 Init and dispose concurrency tests (S)

- **Stage:** 3
- **Depends on:** M4-03, M1-13, M4-03b
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Concurrency.Tests/Lifecycle`
- **Invariants:** INV-04, INV-05, INV-13, INV-29
- **Scope:** TST-03 items 8, 14 and 16 in Ducky.Concurrency.Tests (Dispose_FromDrainer_DoesNotDeadlock is single-threaded and lives in M4-01).
- **Acceptance tests:**
  - `InitBuffer_PreservesUserActionOrderAcrossGate`
  - `InitStart_NeverRunsUnderGate`
  - `Init_OverflowBetweenFlagAndStart_MiddlewareInitStillRuns`
  - `Dispose_WaitsForInFlightDrainBeforeMiddlewareDispose`
  - `SyncDispose_DuringDrain_NoAfterReduceOnDisposedMiddleware`
  - `InitializeAsync_WithConcurrentDrainer_CompletesAfterStoreInitializedReduced`
  - `Dispose_DuringConcurrentDispatch_NoExceptionNoLeak_AllTasksComplete`
- **Done:** Common DoD.

#### M4-05 Middleware constructor check and materialization (S)

- **Stage:** 4
- **Depends on:** M4-01, M2-08
- **Unblocks:** M4-05b
- **Area:** `src/Ducky/Configuration/CtorCheck.cs`
- **Invariants:** INV-31
- **Scope:** Extend DUCKY309 and DUCKY353 to middleware; a constructor that uses the store re-enters materialization and throws InvalidOperationException.
- **Acceptance tests:**
  - Build_MiddlewareCtorDependencyMissing_Reported (non-normative)
  - Materialization_MiddlewareCtorUsesStore_Throws (non-normative)
- **Done:** Common DoD.

#### M4-05b Materialization versus disposal (M)

- **Stage:** 4
- **Depends on:** M4-05, M2-03b, M1-09
- **Unblocks:** M4-10
- **Area:** `src/Ducky/Store/Materialization.cs, test/Ducky.Concurrency.Tests/Materialization`
- **Invariants:** INV-05, INV-29, INV-31
- **Scope:** Every materializing entry point first calls BeginMaterialize(), which under _gate returns false once the store is Disposed (the caller takes its after-disposal path and constructs nothing) and otherwise sets _materializationStarted; dispose step 1 sets Disposed in the same critical section, then awaits the factory's RunContinuationsAsynchronously _materialized TCS within DisposeTimeout (chained past it) and disposes what it built. A throwing factory disposes the instances already built in reverse order, each in its own try/catch (1015), before caching DUCKY353; DisposeAsync never rethrows DUCKY353. Internal scoped StoreDisposeHook, resolved last at materialization (skipped once disposal began) and disposed first by the server scope, disposes the store before its effects' scoped dependencies (lazily resolved dependencies documented as not covered).
- **Acceptance tests:**
  - `Dispose_NeverMaterialized_ConstructsNothing`
  - `Dispatch_AfterDisposeOnUnmaterializedStore_IgnoredNotThrown`
  - `Dispose_ConcurrentWithFirstUse_EveryConstructedMiddlewareDisposed_Deterministic`
  - `Dispose_ConcurrentWithFirstUse_EveryConstructedMiddlewareDisposed` (Ducky.Concurrency.Tests)
  - `Materialization_ConcurrentFirstUse_SecondCallerWaitsThenSucceeds` (Ducky.Concurrency.Tests)
  - `Materialization_CtorThrows_EarlierInstancesDisposed`
  - `ServerScopeDispose_EffectScopedDependencyDisposedAfterStoreDispose`
- **Done:** Common DoD.

#### M4-06 JSON gateway (UseJson, DuckyJson) and JsonElement restore (M)

- **Stage:** 6
- **Depends on:** M1-10, M1-08
- **Unblocks:** M4-07, M6-02, M6-04
- **Area:** `src/Ducky/Json`
- **Invariants:** INV-23
- **Scope:** UseJson(resolver): a JsonSerializerContext's own options are copied (new JsonSerializerOptions(ctx.Options) { TypeInfoResolver = resolver }), any other resolver gets fresh options; UseJson(options) only stores the reference: a null TypeInfoResolver is DUCKY306 at first resolution with the other errors, and only after validation a copy is made and frozen (the caller's instance is never frozen); last call wins. RequireJsonTypeInfo => DUCKY306 with the [JsonSerializable] line. DuckyJson (§10): per-store cache where only missing type info is cached; TrySerialize/ToNode return false/null on missing info or any non-fatal exception (Debug once per type per store); TryDeserialize(JsonElement, Type, out object?, string? key = null) catches every non-fatal exception and logs a Warning naming the key and type. No DuckyWireContext (Ducky core sends nothing over a wire). IStore.Json. Restore accepts JsonElement (cloned before enqueue); TryRestore uses the declared StateType and passes the key; a failing slice keeps its state while others restore.
- **Acceptance tests:**
  - `Restore_JsonElementFromDisposedDocument_WhileOtherThreadDrains_Restores` (Ducky.Concurrency.Tests)
  - `Restore_JsonElementFromDisposedDocument_WhileOtherThreadDrains_Restores_Deterministic`
  - DuckyJson_CtorThrowingPayload_ReturnsFalse (non-normative)
  - UseJson_Context_CopiesContextOptions (non-normative)
  - RequireJsonTypeInfo_Missing_Ducky306 (non-normative)
  - UseJson_OptionsWithoutResolver_Ducky306_CallerInstanceNotFrozen (non-normative; also a case of Build_WithFiveMisconfigurations_ReportsAllFive)
- **Done:** Common DoD.

#### M4-07 Action type names and unserializable degrade (S)

- **Stage:** 6
- **Depends on:** M4-06, M3-05
- **Unblocks:** M4-10, M8-02, M9-06
- **Area:** `src/Ducky/Diagnostics/ActionTypes.cs, test/Ducky.AotSmoke`
- **Invariants:** INV-23, INV-24
- **Scope:** ActionTypeAttribute, per-store cached names (§9): attribute, SetState => {sliceKey}/{name}, HydrateSlices => @ducky/restore, otherwise Namespace.Name with Name<Arg> and Outer.Inner. Unserializable action payloads degrade to name only, logged once per type per store. [ActionType] is the one attribute read at run time: once per action type per store, with GetCustomAttribute<ActionTypeAttribute>(inherit: false). Ducky.AotSmoke gains the ActionType_AttributeName_SurvivesAot assertion.
- **Acceptance tests:**
  - `ActionType_Names_Table`
  - `Unserializable_Action_DegradesToNameOnce`
  - `ActionType_AttributeName_SurvivesAot` (Ducky.AotSmoke)
- **Done:** Common DoD.

#### M4-08 Unhandled action check (S)

- **Stage:** 6
- **Depends on:** M2-01, M4-01
- **Unblocks:** M4-10
- **Area:** `src/Ducky/Store/Unhandled.cs`
- **Invariants:** —
- **Scope:** Process step 12 for Local/Effect only: no slice handles and no effect matched => Debug log guarded by IsEnabled, or ReducerFailed(UnhandledActionException) when ThrowOnUnhandledAction. Library traffic never checked.
- **Acceptance tests:**
  - `UnhandledAction_DefaultLogsDebug`
  - `UnhandledAction_StrictMode_ReducerFailed`
- **Done:** Common DoD.

#### M4-09 Tracing and metrics (M)

- **Stage:** 6
- **Depends on:** M2-05, M1-05b
- **Unblocks:** M4-10
- **Area:** `src/Ducky/Diagnostics/Telemetry.cs`
- **Invariants:** INV-02, INV-03, INV-22
- **Scope:** static readonly ActivitySource("Ducky"); ducky.dispatch activity with type/origin/depth/correlation tags when listened, parented on Pending.ParentActivity (Activity.Current?.Context captured on the producer's flow); step 2 saves Activity.Current and the finally that restores the causal scope stops the activity and restores the saved value. Meter only through IMeterFactory: ducky.dispatch.duration, ducky.dispatch.dropped (tag ducky.drop.reason = depth or run; counted at step 1, step 3 and the call-time drops of EffectContext and SliceStore, never for Disposed), ducky.effect.failures, ducky.effect.dropped. Internal SafeTelemetry wraps StartActivity, Stop, Add and Record (Warning 1017 through SafeLogger). No static Meter.
- **Acceptance tests:**
  - `Tracing_ActionFromOtherThread_ParentIsProducerActivity`
  - `Tracing_ActivityCurrentRestoredAfterDispatch`
  - `Telemetry_ListenerThrows_LaterStepsStillRun` (Ducky.Tests)
  - Metrics_DroppedAndEffectCounters (non-normative)
  - Metrics_NoMeterFactory_NoMeterCreated (non-normative)
- **Done:** Common DoD; NoStaticMutableFields still green.

#### M4-10 Core contract closure (S)

- **Stage:** 6
- **Depends on:** M4-03, M4-07, M4-08, M2-06, M3-01, M4-01b, M4-03b, M4-03c, M4-05b, M4-09, M3-01b
- **Unblocks:** M9-01, M9-01b, M9-04, M11-01, M13-01, M13-10, M16-01
- **Area:** `test/Ducky.Tests/Contract, src/Ducky/IDispatcher.cs`
- **Invariants:** INV-10
- **Scope:** Complete DispatchAsync result theory: Reduced, Vetoed, Dropped (depth, step 3 and the call-time run check, each asserting ducky.dispatch.dropped with its reason), Failed, Disposed (including an EffectContext.DispatchAsync from a Merge run after DisposeAsync began and a run-tagged action processed after dispose steps 1-2 through BeforeProcessHook, uncounted; the SliceStore rows arrive in M9-01b). Api_AllAsyncMembers_NeverFault over exactly the INV-10 member set of §8.1 with its named carve-outs (synchronous init throws injected); Api_CallerTokenCancelled_OnlyThatWaitCanceled; replay determinism (the concurrency property and its single-threaded twin); the §7 threading contract verbatim in IDispatcher XML docs.
- **Acceptance tests:**
  - `DispatchAsync_EveryPath_ReturnsExpectedResult`
  - `Api_AllAsyncMembers_NeverFault`
  - `Api_{Member}_NeverFaults` (one per public async member)
  - `Api_CallerTokenCancelled_OnlyThatWaitCanceled`
  - `ReplayDeterminism_SameActionsSameSnapshot` (Ducky.Concurrency.Tests)
  - `ReplayDeterminism_SameActionsSameSnapshot_Deterministic`
- **Done:** Common DoD. Ducky core public API equals §5 minus SliceStore/EntityState/tombstones.

### M5 — Blazor components and interactivity

Ducky.Blazor foundation (AddBlazor, JsBridge, fakes on a real ComponentStatePersistenceManager), SubscriptionCore, DuckyComponent/Layout/Select/Initializer (§24 step 10), then the InteractivityGate and first-toucher hand-off (step 11, INV-19, INV-20), built on the week-1 S-3/S-5/S-7 prototypes.

#### M5-02 AddBlazor, BlazorOptions, JsBridge and test fakes (M)

- **Stage:** 10
- **Depends on:** M0-13
- **Unblocks:** M5-03, M5-04, M6-01, M6-02
- **Area:** `src/Ducky.Blazor/Core, test/Ducky.Blazor.Tests/Fakes`
- **Invariants:** INV-03, INV-22, INV-23
- **Scope:** DuckyBlazorBuilderExtensions.AddBlazor (idempotent; configure delegates compose in call order into one BlazorOptions; Use<PrerenderHandoff>, Use<PersistenceMiddleware> placeholders inert), BlazorOptions with internal IsBrowser (single justified read). JsBridge as the only interop caller (the JSInterop extension types and InvokeAsync/Invoke overloads are banned elsewhere; each JsBridge call site carries the RS0030 justification) accepting string/int/long/bool/DotNetObjectReference, lazy module import that caches only a successful import task, JSDisconnected/TaskCanceled catches. Internal BlazorWireContext; internal SafeLogger for Ducky.Blazor; EventIds 2000+. Test fakes: FakeJsRuntime (records every argument), FakeJsStreamReference and FakeComponentStateStore : IPersistentComponentStateStore driving a real ComponentStatePersistenceManager (PersistentComponentState is never faked; the fake store's SupportsRenderMode accepts only InteractiveAutoRenderMode).
- **Acceptance tests:**
  - `JsBridge_OnlyStringAndPrimitiveArgsCrossBoundary`
  - `NoStaticState_TwoCircuitsIsolated`
  - `DuplicateRegistration_AddSliceTwice_OneSlice`
  - AddBlazor_ConfigureDelegatesCompose (non-normative)
  - `SafeLogger_ProviderThrows_NeverEscapes` (Ducky.Blazor.Tests)
- **Done:** Common DoD. Deletes the Ducky.Blazor.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M5-03 SubscriptionCore (M)

- **Stage:** 10
- **Depends on:** M5-02
- **Unblocks:** M5-05
- **Area:** `src/Ducky.Blazor/Components/SubscriptionCore.cs`
- **Invariants:** INV-19
- **Scope:** One store subscription (static s => s, ReferenceEqualityComparer); OnCommit CAS on _scheduled; Schedule under ExecutionContext.SuppressFlow with await Task.Yield() inside; Evaluate catches its own exceptions after step 1, so only a failure before Evaluate starts resets the flag, with CompareExchange(ref _scheduled, 0, 1) (logged), ObjectDisposedException swallowed; Evaluate resets the flag first, returns if disposed, recomputes selections against the last renderer-observed values, one StateHasChanged.
- **Acceptance tests:**
  - `Schedule_InvokeAsyncThrows_FlagReset`
  - `Schedule_DoesNotFlowCausalScope`
  - `Schedule_DrainerOnRendererThread_InlineInvoke_DoesNotFlowCausalScope`
  - `SubscriptionCore_RandomPublishInterleavings_LastPublishAlwaysEvaluated`
  - `Evaluate_SelectorThrows_AtMostOnePendingInvoke`
- **Done:** Common DoD.

#### M5-05 DuckyComponent, DuckyComponent<TState>, DuckyLayout (M)

- **Stage:** 10
- **Depends on:** M5-03
- **Unblocks:** M5-06, M13-09
- **Area:** `src/Ducky.Blazor/Components/DuckyComponent*.cs`
- **Invariants:** INV-19, INV-20
- **Scope:** C# components (no .razor): Select overloads through SubscriptionCore, Dispatch/DispatchAsync, IHandleAfterRender re-implementation closing registration (DUCKY352), idempotent Dispose. DuckyComponent<TState>.State created on first access or in the after-render hook just before closing. The interactivity hand-off is added by the gate story (M5-04). Ducky.Blazor's EveryErrorCode_FollowsMessageTemplate starts here with DUCKY352.
- **Acceptance tests:**
  - `Component_ParameterChangeWithoutDispatch_ShowsNewItem`
  - `Component_ChangeToOtherItem_NoRerender`
  - `Component_ChangeToSelectedItem_Rerenders`
  - `Component_ParamChangeThenStoreChangeBackToOldValue_Rerenders`
  - `Select_AfterFirstRender_Throws`
  - `Select_AfterFirstRender_ThrowsEvenIfOverrideSkipsBase`
  - `Dispose_ThenStoreChange_NoStateHasChanged`
  - `ManyChangedSelections_OneRender`
  - `DuckyComponentOfT_ConditionalStateAccessAfterFirstRender_Works`
- **Done:** Common DoD.

#### M5-06 DuckySelect, StoreSelectionExtensions, DuckyInitializer (M)

- **Stage:** 10
- **Depends on:** M5-05
- **Unblocks:** M5-04, M6-04, M12-06
- **Area:** `src/Ducky.Blazor/Components/Select+Initializer`
- **Invariants:** INV-19
- **Scope:** DuckySelect<TResult> (renders ChildContent(value), re-subscribes on Selector reference change in SetParametersAsync, disposes). StoreSelectionExtensions.Select(store, selector, invokeAsync, stateHasChanged, rendererInfo, IServiceProvider services) for foreign base classes. DuckyInitializer awaits Store.InitializeAsync() in OnInitializedAsync (normative, §11.1), Loading then ChildContent. Interactivity recording and the service hand-off of all three touchers arrive with the gate (M5-04).
- **Acceptance tests:**
  - `DuckySelect_OnlyFragmentRerenders`
  - `DuckySelect_SelectorParameterChange_Reevaluates`
  - `ForeignBase_StoreSelect_Works`
  - `DuckyInitializer_ShowsLoadingUntilReady`
- **Done:** Common DoD.

#### M5-04 InteractivityGate and the first-toucher hand-off (S)

- **Stage:** 11
- **Depends on:** M5-02, M5-06
- **Unblocks:** M6-04, M6-07, M8-02
- **Area:** `src/Ducky.Blazor/Interactivity`
- **Invariants:** INV-15, INV-19
- **Scope:** Internal gate, one per store, reached through the store-owned PersistenceSlice registration (it follows the store's lifetime; never a scoped service): Unknown/Interactive/NonInteractive; IsBrowser => Interactive; the first toucher (DuckyComponent, DuckySelect and StoreSelectionExtensions.Select through its services parameter, DuckyInitializer) records RendererInfo.IsInteractive and hands over its scoped IServiceProvider; one synchronous probe through JsBridge when still Unknown (a synchronous InvalidOperationException => NonInteractive, else Interactive and keep the module task); a synchronous OnFirstRegistration(Action) callback run outside locks. SubscriptionCore and DuckyInitializer resolve the gate with GetService and skip recording and hand-off when AddBlazor() was not called. Built on the S-7 prototype answers (M0-13).
- **Acceptance tests:**
  - `Gate_Unknown_ProbeDetectsPrerender` (bUnit; its E2E twin is in M14-03)
  - `Gate_Wasm_ComponentRegistrationVisibleToMiddleware`
  - `Component_WithoutAddBlazor_SelectsAndRerenders`
  - `StoreSelectionExtensions_Wasm_HandsOverScopedServices`
  - Gate_OnFirstRegistration_RunsSynchronouslyInsideSelect (non-normative)
- **Done:** Common DoD.

### M6 — Browser persistence and prerender

Prerender<T> and the prerender handoff (seed take, write, browser wait; step 11), then ducky.js storage, envelopes, Persist<T>, the persistence slice, hydration with exactly-one-terminal, PersistenceWriter with the race-proof defer, retry and the phase-5a flush, ClearPersistedState, seed/storage precedence and pause seeds with dirty keys and versions (INV-14, INV-15, INV-16).

#### M6-01 ducky.js storage exports (M)

- **Stage:** 12
- **Depends on:** M0-09, M5-02
- **Unblocks:** M6-07, M7-01
- **Area:** `src/Ducky.Blazor/wwwroot/ducky.js, test/Ducky.E2E/harness, test/Ducky.PackageSmoke`
- **Invariants:** INV-23
- **Scope:** Replace the M0-02 stub with the real module (one plain ES module): storageGet(area, key, maxInlineBytes) measuring UTF-8 of JSON.stringify and returning the too-large sentinel, storageGetStream returning an empty Uint8Array when nothing is kept, storageSet/storageRemove(area, key, id) returning false for an unregistered id (relay to other ids comes in M7-01). Harness specs in Ducky.E2E on Playwright's clock; the JS gate measures the real ducky.js at 100%. PackageSmoke step 5: publish the WASM and Server consumers from packages and assert ducky.js in the static web assets output (§17.9).
- **Acceptance tests:**
  - DuckyJs_StorageGet_ReturnsSentinelAboveUtf8Budget (non-normative harness spec)
  - DuckyJs_StorageSetGetRemove_RoundTrip (non-normative harness spec)
  - `DuckyJs_PullExports_ReturnEmptyWhenGone` (storageGetStream; extended to devtoolsTakeMessage in M8-01)
  - `E2E` JS gate: 100% block coverage of ducky.js
  - `PackageSmoke`: the published WASM and Server consumers serve ducky.js
- **Done:** Common DoD including the JS coverage gate. Deletes the Ducky.E2E Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M6-02 Envelope reader and writer (M)

- **Stage:** 12
- **Depends on:** M5-02, M4-06
- **Unblocks:** M6-03
- **Area:** `src/Ducky.Blazor/Persistence/Envelope`
- **Invariants:** INV-14
- **Scope:** EnvelopeWriter (Utf8JsonWriter, {v, at, s}, no AQN). EnvelopeReader shared with cross-tab: v > Version => not found (Warning), v < Version => Migrate steps over JsonNode, MaxAge discard, deserialize s through Store.Json with the declared StateType, malformed or throwing => keep state (Warning); 1.x data discarded (Debug).
- **Acceptance tests:**
  - `Persist_Envelope_Migrations_Ttl_NewerVersionIgnored`
  - `Persist_EnvelopeRoundTrip_Property`
  - `Persist_OneXFormat_Discarded`
  - `Persist_PolymorphicState_RoundTripsViaDeclaredType`
  - `Persist_ContextWithCamelCaseAndEnumStringConverter_RoundTripsUsingContextOptions`
- **Done:** Common DoD.

#### M6-03 Persist<T> API, PersistenceSlice and validation (M)

- **Stage:** 12
- **Depends on:** M6-02, M6-04
- **Unblocks:** M6-07, M13-04b
- **Area:** `src/Ducky.Blazor/Persistence/Registration`
- **Invariants:** INV-14, INV-31
- **Scope:** Persist<TSlice>(configure) composing into one PersistOptions per slice (Storage, Version, Migrate, MaxAge, Debounce, SyncAcrossTabs, Scope: browser keys are scoped only when Scope is set explicitly), RequireJsonTypeInfo. Hidden PersistAttributeDefaults<T> for generated code (emitted in M13-04b): registers T as persisted exactly like Persist<T>() (AddBlazor(), RequireJsonTypeInfo, the same validations) and records the attribute values as the base layer applied before every Persist<T> delegate, so the builder always wins. Public actions HydrationCompleted, HydrationFailed, PersistenceFailed, ClearPersistedState; PersistenceState/PersistenceStatus; internal PersistenceSlice (key @ducky/persistence, Initial Hydrated, ignores terminals with another ScopeEpoch, no-op ClearPersistedState). DUCKY310, 311, 312 and 316 as AddValidation rules on the final composed options at first resolution; Ducky.Blazor's EveryErrorCode_FollowsMessageTemplate extended.
- **Acceptance tests:**
  - `ClearPersistedState_Strict_NoFailure`
  - `Hydration_StaleEpochTerminal_IgnoredBySlice`
  - Persist_ConfigureComposesInCallOrder (non-normative)
  - Validation_Ducky310_311_312_316_Reported (non-normative)
- **Done:** Common DoD.

#### M6-04 Prerender<T> and the handoff: seed take and SeedSettled (M)

- **Stage:** 11
- **Depends on:** M5-06, M5-04, M4-06
- **Unblocks:** M6-03, M6-05, M6-06, M6-07, M13-04
- **Area:** `src/Ducky.Blazor/Prerender/Take, src/Ducky.Blazor/Prerender/Registration`
- **Invariants:** INV-15
- **Scope:** Prerender<TSlice>() and Prerender<TSlice, TState>(Func<TState, bool> include) (a slice is seeded only while the predicate holds, for error or incomplete states), both calling RequireJsonTypeInfo. PrerenderHandoff middleware (inert without Prerender slices). Outside the browser, synchronous prefix: TryTakeFromJson<byte[]>("ducky:seed") (path per S-3), parse the envelope through BlazorWireContext, Store.Restore(Hydration), complete SeedSettled (carrying pause-seed keys); RegisterOnPersisting(…, RenderMode.InteractiveAuto) always (writing in M6-05; the WASM copy of the seed is documented as not data-protected). Tests persist and restore through FakeComponentStateStore and a real ComponentStatePersistenceManager.
- **Acceptance tests:**
  - `PrerenderSeed_AppliedBeforeFirstInteractiveRender`
  - `Prerender_InitStartedByDuckyInitializer_SeedAppliedBeforeFirstRender`
  - `Circuit_Resume_SeedRestoresPrerenderSlices` (bUnit; its E2E twin is in M14-03)
  - `Prerender_RestoredSlice_LoadEffectRunsOnceAcrossHandoff`
  - `Prerender_OnPersistingRegisteredWithInteractiveAutoMode`
- **Done:** Common DoD.

#### M6-05 Prerender handoff: seed writing and wire budget (M)

- **Stage:** 11
- **Depends on:** M6-04
- **Unblocks:** M6-13b, M11-03
- **Area:** `src/Ducky.Blazor/Prerender/Write`
- **Invariants:** INV-15
- **Scope:** OnPersisting: WhenIdleAsync bounded by PrerenderIdleTimeout; on idle take one snap = Store.State and serialize Prerender slices in registration order into the UTF-8 envelope, omitting a slice whose inclusion exceeds PrerenderSeedMaxWireBytes by the §11.4 estimate (Warning 2021 once per slice); timeout persists nothing (Warning 2014 once per store); src prerender or pause. A Prerender<TSlice, TState> include predicate is evaluated on that snapshot.
- **Acceptance tests:**
  - `Prerender_LoadEffectSlowerThanRender_SeedWaitsForIdle`
  - `Prerender_IdleTimeout_PersistsNoSeed_LogsOnce`
  - `Prerender_LongRunningEffect_DoesNotBlockSeed`
  - `PrerenderSeed_AboveBudget_OmitsSliceAndLogs`
  - `PrerenderSeed_WireEstimate_NeverBelowActualEncoding`
  - `DuckyInitializerFirstToucher_SeedWrittenOnPrerenderAndAppliedBeforeFirstInteractiveRender`
  - `Prerender_LoadFailedDuringPrerender_InteractiveSideRetries`
- **Done:** Common DoD.

#### M6-06 Browser seed wait (S)

- **Stage:** 11
- **Depends on:** M6-04, M4-03b
- **Unblocks:** M6-13
- **Area:** `src/Ducky.Blazor/Prerender/BrowserWait`
- **Invariants:** INV-15
- **Scope:** In the browser: take synchronously; otherwise the gate's synchronous OnFirstRegistration callback takes the seed behind a once-flag (RegisterOnRestoring is not used: it fires at registration, not at the initial restore). InitializeAsync awaits SeedSettled.Task.WaitAsync(PrerenderSeedWaitTimeout, TimeProvider, initToken); on timeout take once more, Warning 2022 once (naming the awaited-preload fix), complete SeedSettled. When the init token is cancelled (overflow abort), both settle paths skip the restore (Debug log) and still complete SeedSettled, and PrerenderHandoff returns normally. No OnPersisting in the browser; later seeds are never read.
- **Acceptance tests:**
  - `Prerender_WasmPreloadBeforeRun_SeedStillApplied`
  - `Prerender_EnhancedNavLaterSeed_Ignored`
  - Prerender_BrowserWaitTimeout_WarnsOnce (non-normative)
  - `Prerender_WasmOverflowAbortBeforeFirstRegistration_SeedIgnored`
- **Done:** Common DoD.

#### M6-07 Hydration: attempt, _issue lock, exactly one terminal (M)

- **Stage:** 12
- **Depends on:** M6-01, M6-03, M6-04, M5-04
- **Unblocks:** M6-08, M6-09, M6-10
- **Area:** `src/Ducky.Blazor/Persistence/Hydration`
- **Invariants:** INV-14
- **Scope:** PersistenceMiddleware + internal BrowserStorageProvider over JsBridge. Synchronous prefix: readable slices by gate (probe if Unknown); none => Status stays Hydrated (NotStarted only in a NonInteractive store with no readable slice); else attempt 0 under _issue (register on the init token, arm the init-phase HydrationTimeout deadline, Hydrating restore), and epoch 0 records its key set. Async: resolve the initial scope for epoch 0 (the hand-off wait, scope resolution and reads share that one HydrationTimeout budget) and record it, which publishes the epoch-to-scope map; read keys through EnvelopeReader (passing the key); await SeedSettled; under _issue claim the terminal, set baselines, one restore, HydrationCompleted with epoch, IssueTerminal. Nothing is written before the first read (deferred keys).
- **Acceptance tests:**
  - `Hydration_Restored_OneTerminal_StatusOrder`
  - `Hydration_Empty_OneTerminal`
  - `Hydration_NothingPersisted_Inert`
  - `Hydration_TerminalActions_Paired`
  - `Hydration_OneSliceCtorThrows_OtherSlicesStillRestored`
  - `Hydration_ServerInteractive_BrowserReadBeforeStoreInitialized`
- **Done:** Common DoD.

#### M6-08 Hydration failures, timeout and init-abort races (M)

- **Stage:** 12
- **Depends on:** M6-07
- **Unblocks:** M12-03b
- **Area:** `src/Ducky.Blazor/Persistence/Hydration/Failure, test/Ducky.Concurrency.Tests/Persistence`
- **Invariants:** INV-14
- **Scope:** Exception, timeout and init-token paths claim and issue HydrationFailed (isFailure) under _issue; the init-token callback always calls IssueTerminal under _issue so the terminal precedes StoreInitialized; late results discarded; persistence resumes after failure. Deterministic TCS-gated twins cover every branch; the CsCheck SampleParallel test runs the real race.
- **Acceptance tests:**
  - `Hydration_LoadThrows_FailedGateReleasedPersistenceResumes`
  - `Hydration_Timeout_Failed`
  - `Hydration_InitAborted_OneTerminal`
  - `Hydration_ResultAfterTimeout_IsDiscarded`
  - `Hydration_ReadCompletesConcurrentlyWithInitAbort_TerminalPrecedesStoreInitialized` (Ducky.Concurrency.Tests)
  - `Hydration_ReadCompletesConcurrentlyWithInitAbort_TerminalPrecedesStoreInitialized_Deterministic`
- **Done:** Common DoD.

#### M6-09 Large reads through IJSStreamReference (S)

- **Stage:** 12
- **Depends on:** M6-07
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Blazor/Persistence/Providers/Stream`
- **Invariants:** INV-23
- **Scope:** The too-large sentinel switches to storageGetStream with maxAllowedSize MaxPayloadBytes (D11, path per S-5). Every stream reference is read under await using, the over-limit path included; a zero-length reference is not found; a value above MaxPayloadBytes is not found for that key, with Warning 2023, and never fails the attempt or escapes the [JSInvokable] handler.
- **Acceptance tests:**
  - `LargeRead_UsesStreamAboveInlineLimit`
  - `LargeRead_MaxPayloadBytesBoundary`
  - `Hydration_OneOversizedKey_OtherSlicesRestored`
- **Done:** Common DoD.

#### M6-10 PersistenceWriter (M)

- **Stage:** 12
- **Depends on:** M6-07
- **Unblocks:** M6-10b, M6-11, M6-12, M6-13, M7-02
- **Area:** `src/Ducky.Blazor/Persistence/Writer`
- **Invariants:** INV-16
- **Scope:** AfterReduce signals changed keys unless the origin is Hydration/CrossTab/DevTools. One loop per key on its own CTS: Channel<bool>(1, DropOldest) wake-up, debounce on TimeProvider (defaults per §11.1), one snapshot read, hydration skip (Hydrating and key in the epoch's key set) into _deferred, scope/key derivation from the snapshot epoch, serialize with the declared type, dedupe against the per-storage-key LastKnownPayload (always a local serialization), write, baseline on success. Deferred keys are signalled at the terminal (TryRemove per key). PersistenceFailed once per failure streak (isFailure); a serialization failure reports it and consumes the signal, so the next valid value is written. The race-proof skip is M6-10b.
- **Acceptance tests:**
  - `Writes_NeverOverlapPerKey`
  - `LastWrite_EqualsFinalState`
  - `NoWrite_ForHydrationCrossTabDevToolsOrigins`
  - `PersistenceFailed_OncePerStreak`
  - `Persist_TransientNaN_NextValidValueIsWritten`
- **Done:** Common DoD.

#### M6-10b Writer defer race and the ShouldSkip predicate (S)

- **Stage:** 12
- **Depends on:** M6-10
- **Unblocks:** M12-03
- **Area:** `src/Ducky.Blazor/Persistence/Writer/Defer, test/Ducky.Concurrency.Tests/Persistence`
- **Invariants:** INV-16
- **Scope:** One predicate ShouldSkip(snap, key) (Hydrating and key in the epoch's key set, or scoped with the epoch's scope still unknown) for the skip and for add-then-recheck: after adding a key to _deferred the writer re-reads Store.State and the epoch-to-scope map and, if the skip no longer holds, TryRemoves it and re-signals itself; AfterReduce removes per key with TryRemove and signals what it removed, so each key is signalled exactly once; recording a scope under _issue publishes the map and drains _deferred. Internal IVT hook BlazorOptions.AfterDeferHook(string key, DeferPoint point) with BeforeAdd and AfterAdd.
- **Acceptance tests:**
  - `Write_SkipRacesTerminal_DeferredKeyStillWritten` (Ducky.Concurrency.Tests)
  - `Write_TerminalBetweenReadAndDefer_WriterRechecksAndResignals_Deterministic`
  - `Write_TerminalAfterDeferAdd_AfterReduceSignalsOnce_Deterministic`
  - `Write_TerminalBeforeScopeResolved_NoSpin_WrittenOnceScopeRecorded`
  - `Wasm_ScopeWithoutHandOff_ScopedChange_NoWriterSpin`
- **Done:** Common DoD.

#### M6-11 Retry on provider failure, flush in phase 5a, loss counting (M)

- **Stage:** 12
- **Depends on:** M6-10, M2-03b, M4-03c
- **Unblocks:** M6-12, M12-01, M12-03c
- **Area:** `src/Ducky.Blazor/Persistence/Writer/Retry`
- **Invariants:** INV-03, INV-16, INV-29
- **Scope:** Every provider failure while the store is alive (JSDisconnected/TaskCanceled for browser storage) keeps the key dirty with its baseline untouched and retries with the capped 1 s to 30 s backoff on TimeProvider; PersistenceFailed once at the start of a streak (disconnects stay Debug-only); a failed module import is re-imported on the next call and the watchStorage registration re-issued. Dispose flush in phase 5a (after drain exit and the init wait only, so a hung effect run or middleware init never blocks it): cancel pending debounce and backoff, one immediate attempt per dirty key (the hydration skip still applies), bounded by Middleware.DisposeTimeout, then cancel the loop CTSs; every key still dirty is counted ducky.persistence.lost. Ducky.Blazor's SafeTelemetry wraps the meter (Warning 2024).
- **Acceptance tests:**
  - `ServerBrowserStorage_TransientDisconnect_WriteRetriedAfterReconnect`
  - `ServerBrowserStorage_ChangeInsideDebounceThenCircuitClose_IsLostAndCounted`
  - `ServerBrowserStorage_ImportFailsOnDisconnect_ReimportedAfterReconnect`
  - `Dispose_HungEffectRun_PersistenceStillFlushed`
  - `Dispose_HungMiddlewareInit_PersistenceStillFlushed`
  - `Telemetry_ListenerThrows_LaterStepsStillRun` (Ducky.Blazor.Tests)
- **Done:** Common DoD.

#### M6-12 ClearPersistedState removal command (M)

- **Stage:** 12
- **Depends on:** M6-10, M6-11
- **Unblocks:** M7-02b, M12-03, M12-03c
- **Area:** `src/Ducky.Blazor/Persistence/Writer/Clear`
- **Invariants:** INV-16
- **Scope:** AfterReduce posts a removal to each persisted slice's writer, key from the clear's snapshot epoch; the loop runs it after any in-flight write, discards signals stamped before the clear, removes the exact key, resets the baseline to absent. No prefix clear. Posting a command ends a backoff sleep: the loop makes one immediate attempt at its head item, then runs the command; a failed attempt resumes the backoff at the step it reached.
- **Acceptance tests:**
  - `Write_AfterClear_SameContent_IsWritten`
  - `Clear_RemovesOnlyPersistedKeysOfThisScope`
  - `Clear_DuringInFlightWrite_KeyStaysRemoved`
  - `Clear_WithPendingDebouncedChange_KeyStaysRemoved`
  - `Clear_WriterInRetryBackoff_RemovedImmediately`
- **Done:** Common DoD.

#### M6-13 Seed/storage precedence and pause seeds (M)

- **Stage:** 12
- **Depends on:** M6-06, M6-10
- **Unblocks:** M6-13b, M9-02, M12-01
- **Area:** `src/Ducky.Blazor/Persistence/Precedence`
- **Invariants:** INV-15
- **Scope:** Storage restore enqueued only after SeedSettled; a valid storage envelope wins over a prerender seed regardless of age (ADR-0011); an awaited WASM preload warns and never inverts the precedence. Pause seeds are M6-13b.
- **Acceptance tests:**
  - `Precedence_ValidStorageEnvelope_WinsOverSeed`
  - `Precedence_OlderStorageEnvelope_StillWins`
  - `Precedence_PrerenderSeed_StorageStillWins`
  - `Prerender_WasmAwaitedPreload_WarnsAndDoesNotInvertPrecedence`
- **Done:** Common DoD.

#### M6-13b Pause seeds: dirty keys and the version map (S)

- **Stage:** 12
- **Depends on:** M6-13, M6-05
- **Unblocks:** M12-07
- **Area:** `src/Ducky.Blazor/Prerender/PauseSeed`
- **Invariants:** INV-15
- **Scope:** A pause seed (src pause) derives everything from one snap = Store.State after the idle wait: values, the left-out keys of an attempt still Hydrating, and a dirty array listing each carried browser-storage key whose serialization differs from LastKnownPayload (absent compares as the initial state). Every seed carries ver = {slice key: PersistOptions.Version} for each persisted key. On restore a key whose recorded version differs or is missing is dropped (Debug log) from the synchronous restore and the dirty set, so storage (migrated by EnvelopeReader) or the initial state applies; only dirty keys take the 'pause seed wins' path (read with baseline set, restore skipped, deferred and rewritten after the terminal), clean keys follow prerender precedence. The pause override never applies to Server storage (M12-07).
- **Acceptance tests:**
  - `Circuit_ResumeAfterOtherTabWrote_CleanKey_StorageWins`
  - `Circuit_ResumeAfterVersionBump_DirtySeedKeyDropped_StorageMigrated`
  - PauseSeed_DirtyKeysSkipStorageRestoreAndAreRewritten (non-normative)
- **Done:** Common DoD.

### M7 — Cross-tab

ducky.js watch/relay/resync with pruning and repair, the .NET CrossTabSync with echo, removal and convergence guards, and scope-aware receipts (INV-18).

#### M7-01 ducky.js watch, relay and resync (M)

- **Stage:** 14
- **Depends on:** M6-01
- **Unblocks:** M7-02b, M8-01
- **Area:** `src/Ducky.Blazor/wwwroot/ducky.js, test/Ducky.E2E/harness`
- **Invariants:** INV-18
- **Scope:** watchStorage(id, ref, prefix, crossTab, pruneAfterMs) idempotent per id, unwatchStorage; window storage listener for crossTab ids (localStorage, prefix filter, UTF-8 size decides tooLarge); same-document relay from storageSet/storageRemove to other ids with the same prefix; a rejected invokeMethodAsync records the failed (source, area) pair per id and resyncs each pair once on the first success, with 1 s to 30 s backoff; an id is pruned after pruneAfterMs (a fixed 10 minutes passed by .NET) of continuous delivery failure, the untracked-object match kept as a fast path; storageSet/storageRemove return false for a pruned id.
- **Acceptance tests:**
  - DuckyJs_Relay_SkipsWriterId (non-normative harness spec)
  - `DuckyJs_ResyncBackoff_CapsAt30s_ThenReReadsAll`
  - `DuckyJs_PrunedIdReRegisteredOnNextWrite`
  - DuckyJs_UntrackedObject_PrunesId (non-normative harness spec)
  - `E2E` JS gate: 100% block coverage of ducky.js
- **Done:** Common DoD including the JS coverage gate.

#### M7-02 CrossTabSync (.NET) (M)

- **Stage:** 14
- **Depends on:** M6-10
- **Unblocks:** M7-02b, M7-03, M14-01
- **Area:** `src/Ducky.Blazor/CrossTab`
- **Invariants:** INV-18, INV-23
- **Scope:** CrossTabSync is an internal component owned by PersistenceMiddleware (its restores need _issue). Store id Guid N; register once before the hydration read, unwatch on dispose; OnStorageChanged: accept exact current keys (tab: synced slices; doc: area-matching persisted slices), 50 ms per-key debounce, removal => initial state (CrossTab), else EnvelopeReader, drop if local re-serialization equals the baseline, else under _issue bump sequence, Restore(CrossTab), set baseline, signal writer; key null => re-read every accepted key; tooLarge => stream re-read. A value receipt for a key in the key set of an attempt without a terminal is restored with Origin.Hydration (WasRestored<T>() true).
- **Acceptance tests:**
  - `CrossTab_OriginNeverPersisted`
  - `CrossTab_EchoDropped`
  - `CrossTab_TooLarge_ReReadViaStream`
  - `CrossTab_NonAsciiValueUnderCharLimitOverByteLimit_UsesStream`
  - `CrossTab_OtherScopeKey_Ignored`
  - `CrossTab_NewerVersionEnvelope_Ignored`
  - `CrossTab_CtorThrowingPayload_Ignored`
  - `CrossTab_EventDuringHydration_NewestWins`
- **Done:** Common DoD.

#### M7-02b Cross-tab receipts, repair and scope events (M)

- **Stage:** 14
- **Depends on:** M7-02, M7-01, M12-03, M6-12
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Blazor/CrossTab/Receipts`
- **Invariants:** INV-16, INV-17, INV-18
- **Scope:** Absence is a payload for the echo guard: a removal or missing-value receipt (the re-read-all path included) bumps the key's sequence number under _issue first, so it orders against hydration, then restores the initial state; a null receipt sets the baseline to the local serialization of the initial state and signals, so a pending write dedupes away; a receipt for a key whose baseline is already absent restores nothing. A zero-length stream reference reads as a removal. storageSet/storageRemove returning false re-issues watchStorage and runs the re-read-all path per registered area. Scoped keys are matched against the scope of the current epoch under _issue (none accepted between an epoch bump and the new scope's recording), re-checked when the restore is issued.
- **Acceptance tests:**
  - `CrossTab_RemovalReceipt_NoWriteBack`
  - `CrossTab_RemovalDuringHydration_RemovalWins`
  - `CrossTab_ResyncAfterDisconnect_UnwrittenLocalChangeOnOtherKey_Kept`
  - `CrossTab_TooLargeKeyRemovedBeforePull_ResetsSlice`
  - `CrossTab_StorageSetReportsUnknownId_ReWatchesAndReReads`
  - `CrossTab_OldScopeEventDuringSwitch_Ignored`
- **Done:** Common DoD.

#### M7-03 Cross-tab convergence properties (S)

- **Stage:** 14
- **Depends on:** M7-02
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Blazor.Tests/CrossTab/Convergence`
- **Invariants:** INV-16, INV-18
- **Scope:** Two stores whose serializers emit different element orders; CsCheck property through Property.Check of remote changes during a local pending write.
- **Acceptance tests:**
  - `CrossTab_HashOrderedCollection_DifferentProcessOrder_Converges`
  - `CrossTab_RemoteChangeDuringLocalPendingWrite_TabsConverge`
- **Done:** Common DoD.

### M8 — DevTools

Redux DevTools extension bridge: outbound FSA with sanitizers, failure projections and trace, immutable history, inbound jump/reset/import with pulled large messages and declared types, and the time-travel veto (INV-28).

#### M8-01 ducky.js DevTools exports (S)

- **Stage:** 15
- **Depends on:** M7-01
- **Unblocks:** M8-03b
- **Area:** `src/Ducky.Blazor/wwwroot/ducky.js, test/Ducky.E2E/harness`
- **Invariants:** INV-23, INV-28
- **Scope:** devtoolsConnect(id, ref, configJson, pruneAfterMs) => bool (false without the extension), devtoolsInit, devtoolsSend (false for an unregistered id), devtoolsDisconnect; inbound messages forwarded as strings with state stripped from JUMP_*; larger messages use the pull pattern: devtoolsTakeMessage(id, seq) returns the kept message, or an empty Uint8Array when none is kept.
- **Acceptance tests:**
  - DuckyJs_DevtoolsConnect_NoExtension_ReturnsFalse (non-normative harness spec)
  - `DuckyJs_DevToolsLargeMessage_PullBranch`
  - `DuckyJs_PullExports_ReturnEmptyWhenGone` (extended to devtoolsTakeMessage)
  - `E2E` JS gate: 100% block coverage of ducky.js
- **Done:** Common DoD including the JS coverage gate.

#### M8-02 DevToolsMiddleware outbound (M)

- **Stage:** 15
- **Depends on:** M5-04, M4-07
- **Unblocks:** M8-03, M12-05
- **Area:** `src/Ducky.Blazor/DevTools/Outbound`
- **Invariants:** INV-23, INV-28
- **Scope:** UseDevTools(configure) (explicit, composing), DevToolsOptions with the internal DescribeFrame seam (settable inside UseDevTools through Ducky.Blazor's IVT). InitializeAsync reads the gate synchronously (the shared one-time probe when Unknown, never awaiting a registration), connects with a pre-serialized config bounded by HydrationTimeout, and goes inert with a Warning when the extension is absent or devtoolsSend reports an unknown id; sends sanitized @@INIT. AfterReduce: Filter, FSA via Store.Json.ToNode(action, runtime type) then ActionSanitizer (null => meta.unserializable); library actions and failures projected through BlazorWireContext as {type, message, sliceKey|effectType} before the user resolver; per-slice state through the declared StateType taken from IStore.Slices, then StateSanitizer (@ducky/* slices through BlazorWireContext); Trace through DescribeFrame (DiagnosticMethodInfo, <unknown> when trimmed); DevTools-local counter.
- **Acceptance tests:**
  - `DevTools_SanitizerAppliedToInitAndEverySend`
  - `DevTools_OptionsReachExtension`
  - `DevTools_FilterExcludesAction`
  - `DevTools_ExtensionAbsent_Inert`
  - `DevTools_Trace_UsesDiagnosticMethodInfo`
  - `DevTools_GateUnknown_ProbesAndDoesNotDelayInit`
  - `DevTools_FailureAction_ShowsTypeAndMessage`
  - `DevTools_Trace_TrimmedFrame_RendersUnknown`
- **Done:** Common DoD.

#### M8-03 DevTools history, inbound and time travel (M)

- **Stage:** 15
- **Depends on:** M8-02
- **Unblocks:** M8-03b, M14-01, M14-02
- **Area:** `src/Ducky.Blazor/DevTools/Inbound`
- **Invariants:** INV-28
- **Scope:** History: an immutable array of (devToolsIndex, StateSnapshot) capped at MaxAge, replaced only by the drainer with Volatile.Write; [JSInvokable] OnMessage(string? json, long seq, bool tooLarge) parsed with BlazorWireContext reads it once; the time-travel flag is volatile. JUMP_TO_STATE/JUMP_TO_ACTION restore snapshot slots except @ducky/* (DevTools origin) and veto Local/Effect while not at latest; RESET to initial states; IMPORT_STATE per slice through Store.Json with the key; others ignored (Debug); evicted index ignored.
- **Acceptance tests:**
  - `DevTools_JumpToRecordState_RestoresReferenceEqualSlices`
  - `DevTools_DuringTimeTravel_UserActionVetoed`
  - `DevTools_JumpAfterFilteredActions_RestoresMatchingEntry`
  - `DevTools_Reset_RestoresInitialStates`
- **Done:** Common DoD.

#### M8-03b DevTools inbound: large messages, concurrency and declared types (M)

- **Stage:** 15
- **Depends on:** M8-03, M8-01
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Blazor/DevTools/Inbound/Pull, test/Ducky.Concurrency.Tests/DevTools`
- **Invariants:** INV-23, INV-28
- **Scope:** tooLarge messages are pulled with devtoolsTakeMessage through IJSStreamReference under await using (above MaxPayloadBytes ignored with Warning 2023; a zero-length reference ignored with a Debug log); JUMP_* works from the stripped inline message; IMPORT_STATE deserializes each key with its declared StateType from IStore.Slices; jumps delivered concurrently with the drainer's sends restore the entry their index names.
- **Acceptance tests:**
  - `DevTools_JumpMessage_StateStrippedInline`
  - `DevTools_LargeImport_ReadViaStream`
  - `DevTools_PolymorphicState_SerializedAndImportedViaDeclaredType`
  - `DevTools_JumpConcurrentWithSend_RestoresMatchingEntry` (Ducky.Concurrency.Tests)
  - `DevTools_JumpConcurrentWithSend_RestoresMatchingEntry_Deterministic`
- **Done:** Common DoD.

### M9 — SliceStore, entities and Ducky.Testing

SliceStore<TState> (INV-30) inside runs and across stores and its Blazor-host integration, EntityState (INV-32), TestStore/SliceTest/EffectTest with the wall-clock dispose bound, the SliceStore and TestStore analyzer arms, AOT smoke extension.

#### M9-01 SliceStore<TState> (M)

- **Stage:** 8
- **Depends on:** M4-10
- **Unblocks:** M9-01b, M9-01c, M9-02, M9-04, M9-05, M9-06, M14-01
- **Area:** `src/Ducky/SliceStore`
- **Invariants:** INV-11, INV-30
- **Scope:** Internal SetState<TState>(Name, Mutator) registered by the constructor; Set/SetAsync through the full pipeline (Origin.Local outside a run; Origin.Effect with Pending.Run inside a run of the owning store); both read the store's disposal state first (Disposed, uncounted) and then the run token (Dropped, counted with reason run); State via IStore.State (starts init, a point-in-time read); DUCKY351 (a DuckyConfigurationException, SetAsync included) before attach; action type {key}/{name}; the injected instance is the store-owned slice. No StoreLifetime member.
- **Acceptance tests:**
  - `SliceStore_InjectedInstance_IsStoreOwnedSlice`
  - `SliceStore_SameReference_NoCommit`
  - `SliceStore_StateBeforeAttach_Throws`
  - `Effect_InjectsSliceStore_Resolves`
- **Done:** Common DoD.

#### M9-01b SliceStore inside effect runs and across stores (S)

- **Stage:** 8
- **Depends on:** M9-01, M2-04, M4-10
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Tests/SliceStore/Runs, test/Ducky.Concurrency.Tests/SliceStore`
- **Invariants:** INV-06, INV-10, INV-11, INV-30
- **Scope:** Pin Set/SetAsync against the owning store's per-store flow scopes: inside a Switch run they are Origin.Effect with Pending.Run (dropped when superseded); from a subscriber during a drain owned by an effect continuation they are Local; from another store's run they are Local at depth 0 on a new chain. Add the SliceStore rows to DispatchAsync_EveryPath_ReturnsExpectedResult (a Set in a superseded run: Dropped with reason run; a SetAsync in a Switch run after DisposeAsync began: Disposed, uncounted).
- **Acceptance tests:**
  - `Switch_SupersededRunCallingSliceStoreSet_IsDropped`
  - `SliceStore_SetFromSubscriberDuringDrainOwnedByEffectContinuation_IsLocal`
  - `TwoStores_EffectRunOfA_DoesNotScopeDispatchSetOrWhenIdleOfB`
  - `SliceStore_ConcurrentSetFromNThreads_AppliesAll` (Ducky.Concurrency.Tests)
  - `DispatchAsync_EveryPath_ReturnsExpectedResult` (SliceStore rows)
- **Done:** Common DoD.

#### M9-01c SliceStore analyzer arms: DUCKY001-003 mutators, DUCKY010 SetAsync (S)

- **Stage:** 8
- **Depends on:** M9-01, M13-06, M13-08
- **Unblocks:** M15-04
- **Area:** `src/Ducky.Generators/Analyzers/Purity, src/Ducky.Generators/Analyzers/Usage`
- **Invariants:** —
- **Scope:** The analyzer arms that target the SliceStore API ship with it (§24 step 8, P10): DUCKY001-003 analyse SliceStore.Set/SetAsync mutator lambdas and DUCKY010 reports blocking on SetAsync, with verifier tests against the real Ducky assembly (no stub symbols).
- **Acceptance tests:**
  - DUCKY001-003 mutator-arm verifier tests (non-normative)
  - DUCKY010 SetAsync-arm verifier tests (non-normative)
- **Done:** Common DoD; AnalyzerReleases unchanged (no new descriptor).

#### M9-02 SliceStore in Blazor hosts (S)

- **Stage:** 12
- **Depends on:** M9-01, M6-13
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Blazor.Tests/SliceStore`
- **Invariants:** INV-15
- **Scope:** A SliceStore read first (State) starts init so seed and storage apply; plain-component WASM apps never stall on the seed wait. SliceStore.State is a point-in-time read: markup that must follow changes goes through DuckyComponent.Select, <DuckySelect> or StoreSelectionExtensions.Select.
- **Acceptance tests:**
  - `SliceStore_StateReadFirst_SeedAndStorageApplied`
  - `Prerender_PlainComponentsOnlyWasm_NoInitStall`
  - `SliceStore_RenderedThroughSelect_RerendersOnStorageRestore`
- **Done:** Common DoD.

#### M9-03 EntityState<TKey,TEntity> (M)

- **Stage:** 8
- **Depends on:** M1-12
- **Unblocks:** M9-06
- **Area:** `src/Ducky/Entities`
- **Invariants:** INV-32
- **Scope:** IEntity<TKey>, MergeStrategy, EntityState per §5.10: Empty, JsonConstructor (duplicate ids throw), Items only serialized, lazily built FrozenDictionary index cached per instance, SetAll/Add/Upsert/Update/Map/Remove/RemoveWhere/Merge returning this when nothing changes.
- **Acceptance tests:**
  - `EntityState_ModelBased`
  - `EntityState_NoOp_ReturnsSameInstance`
  - `EntityState_JsonRoundTrip`
- **Done:** Common DoD.

#### M9-04 Ducky.Testing: TestStore (M)

- **Stage:** 9
- **Depends on:** M4-10, M9-01
- **Unblocks:** M9-04b, M9-04c, M9-05, M11-02
- **Area:** `src/Ducky.Testing/TestStore`
- **Invariants:** —
- **Scope:** TestStore.Create(configure, services, failureTypes), public API only (ADR-0039): FakeTimeProvider registered, ThrowOnUnhandledAction = true before configure, recording middleware for Processed (failed actions included) and Failures (ReducerFailed, EffectFailed, listed types), each an ImmutableList swapped with ImmutableInterlocked.Update and returned as a snapshot; Seed<TState> resolving Slices.Single(s => s.StateType == typeof(TState)).Key (DUCKY350 on no match) and restoring with Origin.Hydration; GetSlice<TSlice>() returning the store-owned SliceStore; Settled(timeout) raising DuckyAssertionException; all IStore members forwarded, Slices included. DuckyAssertionException.
- **Acceptance tests:**
  - `TestStore_Seed_NoFailure`
  - `TestStore_FailureTypes_CollectsCustomFailures`
  - `TestStore_Settled_WaitsForEffects`
  - `Assertions_ThrowDuckyAssertionException`
  - `TestStore_Seed_ResolvesKeyByDeclaredStateType`
  - `TestStore_GetSlice_ReturnsStoreOwnedSliceStore`
- **Done:** Common DoD. Deletes the Ducky.Testing.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M9-04b TestStore disposal bound and concurrent reads (S)

- **Stage:** 9
- **Depends on:** M9-04
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Testing/TestStore/Dispose.cs, test/Ducky.Concurrency.Tests/Testing`
- **Invariants:** —
- **Scope:** Public WallClockTimeout (default 10 s, also the default of Settled). TestStore.DisposeAsync waits for the real store's DisposeAsync with WaitAsync(WallClockTimeout, TimeProvider.System) and throws DuckyAssertionException naming the bound, the likely causes and the last processed actions (the test-failure carve-out of INV-10, §8.1). Processed and Failures snapshots stay readable while a LongRunning effect appends from pool threads.
- **Acceptance tests:**
  - `TestStore_DisposeWithHungEffectRun_FailsWithinWallClockBound`
  - `TestStore_ProcessedReadWhileLongRunningEffectDispatches_NoException` (Ducky.Concurrency.Tests)
- **Done:** Common DoD.

#### M9-04c DUCKY021 TestStore member arm (S)

- **Stage:** 9
- **Depends on:** M9-04, M13-02
- **Unblocks:** M15-04
- **Area:** `src/Ducky.Generators/Dispatch/Collisions`
- **Invariants:** —
- **Scope:** DUCKY021 also checks the accessible members of TestStore (§24 step 9, P10), with verifier tests against the real Ducky.Testing assembly.
- **Acceptance tests:**
  - DUCKY021 TestStore-arm verifier tests (non-normative)
- **Done:** Common DoD.

#### M9-05 Ducky.Testing: SliceTest and EffectTest (M)

- **Stage:** 9
- **Depends on:** M9-04, M9-01
- **Unblocks:** M15-01
- **Area:** `src/Ducky.Testing/SliceTest+EffectTest`
- **Invariants:** —
- **Scope:** SliceTest.For<TSlice,TState>() Given/When/Then/ThenUnchanged: seeds through Restore, dispatches with Dispatch (reduced inline, §6.7) and throws DuckyAssertionException when an action is missing from Processed. EffectTest.Run (ThrowOnUnhandledAction = false before configure, AddEffect(instance) replacing a configure-registered type of the same effect, arrange after build) and a type-based EffectTest.Run<[DAM] TEffect, TAction>(action, configure, arrange, services), both constrained to Effect so EffectGroup is covered, returning EffectRun (Dispatched, Failures).
- **Acceptance tests:**
  - `SliceTest_ThenUnchanged_ReferenceEquals`
  - `EffectTest_CollectsDispatched`
  - `EffectTest_EffectReadingState_Works`
  - `EffectTest_UnhandledResultAction_NotAFailure`
  - `EffectTest_ConfigureAlsoRegistersEffectType_HandlerRunsOnce`
  - `EffectTest_EffectInjectingSliceStore_Works`
  - `SliceTest_ActionMissingFromProcessed_Throws`
- **Done:** Common DoD.

#### M9-06 AOT smoke: SliceStore and EntityState (S)

- **Stage:** 8
- **Depends on:** M9-01, M9-03, M3-05, M4-07
- **Unblocks:** M12-05
- **Area:** `test/Ducky.AotSmoke`
- **Invariants:** INV-24
- **Scope:** Extend the `AotSmoke` assertion (no new PASS names, P8) with SliceStore and EntityState, JSON round-trip included.
- **Acceptance tests:**
  - `AotSmoke` (now also covering SliceStore and EntityState)
- **Done:** Common DoD.

### M10 — Ducky.Draft

Draft runtime collections, the Draft generator with symbol-based mapping on the shared helpers, INV-26 properties, DUCKY101-109, the DUCKY003 Draft-receiver exemption, multi-assembly fixture, DUCKY901 and the Draft PackageSmoke consumer.

#### M10-01 Draft core types and ListDraft<T> (M)

- **Stage:** 17
- **Depends on:** M0-12
- **Unblocks:** M10-02, M10-04
- **Area:** `src/Ducky.Draft/Collections/ListDraft.cs`
- **Invariants:** INV-26
- **Scope:** DraftableAttribute, IRevocable, IDraft<T>; ListDraft<T> (lazy copy-on-write, remembers its own source, BuildList/BuildArray returning the source when clean and of that kind, default ImmutableArray reads as empty and is returned as-is, implicit conversions, equal set is not a change, explicit revoke). Ducky.Draft never references Ducky.
- **Acceptance tests:**
  - ListDraft_Clean_BuildReturnsSource (non-normative)
  - ListDraft_DefaultArray_ReadsEmptyReturnsDefault (non-normative)
  - ListDraft_Revoked_Throws (non-normative)
  - ListDraft_ModelBased (non-normative CsCheck)
- **Done:** Common DoD. Deletes the Ducky.Draft.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M10-02 Element drafts, dictionary and set drafts (M)

- **Stage:** 17
- **Depends on:** M10-01
- **Unblocks:** M10-05
- **Area:** `src/Ducky.Draft/Collections`
- **Invariants:** INV-26
- **Scope:** ListDraft<T,TDraft> (lazy element drafts, structural ops flush then clear the element cache), DictionaryDraft<K,V>, DictionaryDraft<K,V,TDraft>, SetDraft<T> (BuildHashSet/BuildSortedSet); revoking a collection revokes cached element drafts. A nullable-annotated element or value maps to the plain ListDraft<T>/DictionaryDraft<K,V>; ListDraft<T,TDraft> and DictionaryDraft<K,V,TDraft> never call createDraft on a null element (the indexer returns null and the element stays null at Build); flushed element drafts and replaced child drafts are revoked; collection drafts build through ToBuilder() to keep comparers.
- **Acceptance tests:**
  - ElementDraft_StructuralOpsFlushCache (non-normative runtime twin of Produce_ListStructuralOps_FlushElementDrafts)
  - DictionaryDraft_ModelBased (non-normative)
  - SetDraft_ModelBased (non-normative)
- **Done:** Common DoD.

#### M10-03 Draft generator test harness (S)

- **Stage:** 17
- **Depends on:** M0-12, M13-01
- **Unblocks:** M10-04
- **Area:** `test/Ducky.Draft.Generators.Tests/Infrastructure`
- **Invariants:** INV-25
- **Scope:** Draft generator test harness on the shared src/Shared.Generators helpers of M13-01 (P7): CSharpGeneratorDriver, Verify.SourceGenerators, the real Ducky.Draft reference, the zero-diagnostics helper for generated compilations (DocumentationMode.Diagnose, nullable enabled, warning level 9999), and the caching-test helper over tracking names.
- **Acceptance tests:**
  - Harness self-test: a planted warning in a generated compilation fails the zero-diagnostics helper (non-normative)
- **Done:** Common DoD; CoverageGate proves no over-broad link. Deletes the Ducky.Draft.Generators.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M10-04 Draft generator: pipeline, diagnostics and scalar members (M)

- **Stage:** 17
- **Depends on:** M10-01, M10-03
- **Unblocks:** M10-05
- **Area:** `src/Ducky.Draft.Generators/Pipeline`
- **Invariants:** INV-25, INV-26
- **Scope:** ForAttributeWithMetadataName("Ducky.Draft.DraftableAttribute"), symbol-free models, WithTrackingName per step. Emits Produce, CreateDraft and the nested Draft class (§13.3) for scalar and other members under the GEN-06 rules (// <auto-generated/>, #nullable enable, /// <summary> on public generated members, #pragma warning disable CS0612, CS0618 with its justification around user-type references, hint name {sanitized metadata name}.Draft.g.cs, oblivious reference types treated as annotated): private names __m_{Name}/__s_{Name} with verbatim member names and __x_original/__x_revoked/__x_Guard, explicit IDraft members, Build() omitted when the record has a Build member, revoke guard, get-only members kept; the draft mirrors instance properties with public, internal or protected internal accessibility (minimum of member and type accessibility; EqualityContract and synthesized members excluded; new over a hidden GetType/MemberwiseClone; error-obsolete members left out); implicit operator Draft?(R?). No-op decided at Build against the original. Diagnostics DUCKY101-104 and DUCKY108 with no output. AnalyzerReleases tracking.
- **Acceptance tests:**
  - `Produce_NoOpRecipe_ReturnsSameReference`
  - `Produce_RevertedScalar_ReturnsSameReference`
  - `Caching_{Step}_Cached` (Draft generator steps)
  - `Generated_CompilesClean_{Shape}` (scalar shapes, members named Build/IsDirty/Revoke/Revoked/O, TagsSet next to Tags, a case-only pair, accessibility rows, GetType, [Obsolete] rows, #nullable disable rows, hint-name rows)
  - `HintNames_UniqueAndValid` (Ducky.Draft.Generators.Tests)
  - `Descriptors_MatchCatalogue` (Ducky.Draft.Generators.Tests)
  - DUCKY101-104 and DUCKY108 verifier tests (non-normative)
- **Done:** Common DoD; Verify snapshots committed.

#### M10-05 Draft generator: collection, nested and nullable members (M)

- **Stage:** 17
- **Depends on:** M10-04, M10-02
- **Unblocks:** M10-06, M10-07
- **Area:** `src/Ducky.Draft.Generators/Mapping`
- **Invariants:** INV-25, INV-26
- **Scope:** Symbol-based mapping (OriginalDefinition vs GetTypeByMetadataName, FullyQualifiedFormat): draftable records, ImmutableList/Array, ImmutableDictionary, ImmutableHashSet/SortedSet, element drafts; tri-state members (untouched/drafted/assigned), nullable reads return null, per-kind no-op identity (ImmutableArray.Equals), new modifiers when a base declares Draft/CreateDraft, mutable collections plain get/set with DUCKY105. A same-compilation C maps to C.Draft iff it passes the generator's own shape predicate (one shared function, a boolean in the parent's equatable model); a cross-assembly C maps to C.Draft only when a nested public Draft implementing IDraft<C> with a public Draft(C) constructor exists, else plain get/set with DUCKY107; nullable-annotated elements are never element-drafted and oblivious elements keep element drafting.
- **Acceptance tests:**
  - `Produce_NoOp_ImmutableArrayMember_ReturnsSameReference`
  - `Produce_DefaultImmutableArrayMember_Works`
  - `Produce_NullableMemberNullOriginal_GetReturnsNull`
  - `Produce_ListStructuralOps_FlushElementDrafts`
  - `Generated_CompilesClean_{Shape}` (element kind x collection kind x nullability x derived)
  - `Produce_AssignRecordToDraftableMember_Builds`
  - DUCKY105 and DUCKY107 verifier tests (non-normative)
- **Done:** Common DoD.

#### M10-06 INV-26 properties (M)

- **Stage:** 17
- **Depends on:** M10-05
- **Unblocks:** M11-04
- **Area:** `test/Ducky.Draft.Tests/Properties`
- **Invariants:** INV-26
- **Scope:** CsCheck generators through Property.Check of random nested records and edit scripts (null assignment and null elements in nullable-element lists, collection replacement through implicit conversion, nested draft replacement, assigning a record to a draftable member, no-op ImmutableArray edits), checked with a structural comparer (element-wise, with comparer-identity checks), never record Equals; revocation of flushed element drafts and replaced child drafts.
- **Acceptance tests:**
  - `Produce_EqualsManualWithExpressions`
  - `Produce_UntouchedBranches_ReferenceEqual`
  - `Produce_BaseDeepEqualBeforeAndAfter`
  - `Produce_RevokedDraftAccess_Throws`
  - `Produce_AssignEqualScalar_IsNoOp`
  - `Produce_ElementDraftAfterStructuralOp_Throws`
  - `Produce_ReplacedChildDraft_Throws`
- **Done:** Common DoD.

#### M10-07 DUCKY106, Draft multi-assembly fixture, DUCKY901, Mutty caching port (M)

- **Stage:** 17
- **Depends on:** M10-05
- **Unblocks:** M10-07b, M15-04, M15-07, M16-02
- **Area:** `src/Ducky.Draft.Generators/Analyzers, test/Ducky.Draft.Generators.Tests/MultiAssembly, test/Ducky.PackageSmoke`
- **Invariants:** INV-25, INV-26
- **Scope:** DUCKY106 analyzer (draft escaping the recipe). Three-compilation fixture: Lib draftable record used as member and list element in App, App record deriving from a Lib draftable (new modifiers), nested Lib draft accessed after Produce throws. A cross-assembly variant with a hand-written Draft : IDraft<C> lacking the (C) constructor reports DUCKY107. PackageSmoke (§17.9, stage 17): a Ducky.Draft-only consumer (TreatWarningsAsErrors, nullable, GenerateDocumentationFile) with a nested draftable member, Produce and CreateDraft().Build(), and its variant with the committed fake Mutty nupkg raising DUCKY901. S-9 is re-measured now that the Draft projects hold their real logic. Port Mutty's caching suite.
- **Acceptance tests:**
  - `MultiAssembly_NoAmbiguityOrConflict` (Draft fixture)
  - DUCKY106 verifier tests (non-normative)
  - `PackageSmoke`: Mutty + Ducky.Draft consumer warns DUCKY901
  - `Caching_{Step}_Cached` (ported Mutty cases)
  - `PackageSmoke`: Ducky.Draft-only consumer builds from packages
  - S-9 re-measured at step 17: nightly-mutation and release-mutation timeout-minutes updated (docs/spec/spikes.md)
- **Done:** Common DoD.

#### M10-07b Draft analyzers: DUCKY109 and the DUCKY003 Draft-receiver exemption (S)

- **Stage:** 17
- **Depends on:** M10-07, M13-06
- **Unblocks:** M15-04, M15-07, M16-02
- **Area:** `src/Ducky.Draft.Generators/Analyzers/Discard, src/Ducky.Generators/Analyzers/Purity/DraftExemption`
- **Invariants:** INV-26
- **Scope:** DUCKY109 (Warning, Ducky.Draft.Generators): inside a Produce recipe, an expression statement that discards a same-type result of a plain get/set draft member's method (ImmutableQueue.Enqueue, ImmutableSortedDictionary.Add, EntityState.Upsert), with an assigned false-positive case. DUCKY003's exemption for receivers implementing Ducky.Draft.IDraft<T> or a Ducky.Draft collection draft (matched by metadata name) ships here, verified against the real Ducky.Draft assembly (§24 step 17, P10).
- **Acceptance tests:**
  - DUCKY109 verifier tests (non-normative)
  - DUCKY003 Draft-receiver exemption verifier tests against the real IDraft<T> (non-normative)
- **Done:** Common DoD; AnalyzerReleases.Unshipped and docs/diagnostics/DUCKY109.md added.

### M11 — Ducky.Reactive

R3 host, bridge and middleware; failures as system failure actions; resubscribe with backoff and its races; excluded from quiescence (INV-27); Reactive contract twins, generator arms and PackageSmoke consumer; AOT smoke for Draft and Reactive.

#### M11-01 Ducky.Reactive host, bridge and middleware (M)

- **Stage:** 16
- **Depends on:** M4-10, M2-08
- **Unblocks:** M11-01b, M11-02, M11-03, M11-05
- **Area:** `src/Ducky.Reactive`
- **Invariants:** INV-27
- **Scope:** ReactiveEffect (Handle(actions, state), RetryDelay), AddReactiveEffect<[DAM]T>() recording a store-scope factory, calling RequireResolvableConstructor for DUCKY309, Use<ReactiveMiddleware>() and AddEffect<ReactiveHost>(); ReactiveBridge registered Scoped (one per store) with Subject<object> and SynchronizedReactiveProperty<StateSnapshot> (values have a single writer, the drainer; subscription changes are synchronized); ReactiveMiddleware pushes from AfterReduce; ReactiveHost (Effect<StoreInitialized>, Merge) sets bridge.State.Value = ctx.State before subscribing any pipeline, emits StoreInitialized once after the first subscription, returns immediately and is not counted for quiescence; onNext => ctx.Dispatch; every subscription joins one CompositeDisposable disposed by the lifetime callback; OfActionType exact type. Internal SafeLogger for Ducky.Reactive.
- **Acceptance tests:**
  - `Reactive_DisposedWithStore_NoEmissionAfterDispose`
  - `Reactive_SyncSelfLoop_StoppedByDepthGuard`
  - `Reactive_DoesNotBlockWhenIdle`
  - `Reactive_Debounce_WithFakeTime`
  - `Reactive_ScopedDependencyInBrowser_Resolves`
  - `Reactive_StateObservable_FirstValueIsCurrentSnapshot`
- **Done:** Common DoD; R3 pinned in Directory.Packages.props and packed as R3 >= 1.3.x; EventIds 3000+. Deletes the Ducky.Reactive.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M11-01b Reactive twins of the core contract (S)

- **Stage:** 16
- **Depends on:** M11-01, M4-01b
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Reactive.Tests/Contract`
- **Invariants:** INV-03, INV-13, INV-31
- **Scope:** Ducky.Reactive twins of core rules: a throwing reducer on StoreInitialized still starts ReactiveHost and its pipelines subscribe; Ducky.Reactive's SafeLogger never lets a provider throw escape; a reactive effect with an unresolvable constructor dependency is reported by DUCKY309 with the other errors at first resolution.
- **Acceptance tests:**
  - `ReducerThrowsOnStoreInitialized_LoadEffectAndReactiveHostStillStart` (Ducky.Reactive.Tests)
  - `SafeLogger_ProviderThrows_NeverEscapes` (Ducky.Reactive.Tests)
  - `Build_ReactiveEffectCtorDependencyMissing_ReportedWithOtherErrors`
- **Done:** Common DoD.

#### M11-02 Reactive failures and resubscribe (M)

- **Stage:** 16
- **Depends on:** M11-01, M9-04
- **Unblocks:** M11-02b, M11-04
- **Area:** `src/Ducky.Reactive/Failures`
- **Invariants:** INV-12, INV-27
- **Scope:** Every reactive failure published by ReactiveMiddleware as DispatchSystem(EffectFailed(type, "reactive", ex), isFailure: true); onErrorResume continues; failure completion resubscribes after RetryDelay(attempt) through Task.Delay(delay, timeProvider, lifetimeToken) followed by lifetimeToken.ThrowIfCancellationRequested() into the one cancellation catch (null stops, Warning), attempt counter reset on emission. GroupByUntil recipe test.
- **Acceptance tests:**
  - `Reactive_FailureCompletion_ResubscribesWithBackoff`
  - `Reactive_OnErrorResume_ContinuesStream`
  - `Reactive_Failure_IsSystemFailureAction_StrictModeClean`
  - `Reactive_GroupByUntilRecipe_ReleasesGroups`
- **Done:** Common DoD.

#### M11-02b Reactive resubscribe races and replay (S)

- **Stage:** 16
- **Depends on:** M11-02
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Reactive/Resubscribe, test/Ducky.Concurrency.Tests/Reactive`
- **Invariants:** INV-27
- **Scope:** A resubscribe pending at dispose never resubscribes; failure resubscribes never replay StoreInitialized; resubscribes and timer-driven inner subscriptions racing a producer's drain see the current snapshot first and lose no subscriber (R3's Subject locks subscribe/dispose; a bridge-level lock only if the concurrency test shows otherwise). The deterministic twin drives FakeTimeProvider.Advance from a user AfterReduce placed before the reactive middleware.
- **Acceptance tests:**
  - `Reactive_ResubscribePendingAtDispose_NeverResubscribes`
  - `Reactive_OfActionTypeStoreInitialized_FiresOnceNotOnResubscribe`
  - `Reactive_ResubscribeConcurrentWithDrain_FirstValueCurrent_NoLostSubscriber` (Ducky.Concurrency.Tests)
  - `Reactive_ResubscribeConcurrentWithDrain_FirstValueCurrent_NoLostSubscriber_Deterministic`
- **Done:** Common DoD.

#### M11-03 Reactive vs prerender quiescence pin (S)

- **Stage:** 16
- **Depends on:** M11-01, M6-05
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Blazor.Tests/Prerender/Reactive`
- **Invariants:** INV-15, INV-27
- **Scope:** Pin that a reactive load in flight is not waited for by the seed; XML docs warning on Prerender<T>, ReactiveEffect and Settled.
- **Acceptance tests:**
  - `Prerender_ReactiveLoadInFlight_Documented`
- **Done:** Common DoD.

#### M11-04 AOT smoke: Draft and Reactive (S)

- **Stage:** 17
- **Depends on:** M10-06, M11-02, M3-05, M12-05
- **Unblocks:** M14-06, M16-02
- **Area:** `test/Ducky.AotSmoke`
- **Invariants:** INV-24
- **Scope:** Extend the `AotSmoke` assertion (no new PASS names, P8) with Produce over a generated draftable record and a reactive effect emission under NativeAOT.
- **Acceptance tests:**
  - `AotSmoke` (now also covering Draft and Reactive)
- **Done:** Common DoD; zero IL warnings from R3.

#### M11-05 Reactive generator arms and PackageSmoke consumer (S)

- **Stage:** 16
- **Depends on:** M11-01, M13-03, M13-07, M13-11
- **Unblocks:** M15-04, M16-02
- **Area:** `src/Ducky.Generators/Registration/Reactive, src/Ducky.Generators/Analyzers/Structural/OfActionType, test/Ducky.PackageSmoke`
- **Invariants:** INV-25
- **Scope:** The arms that target Ducky.Reactive ship with it (§24 step 16, P10): GEN-02 registers every ReactiveEffect (any public constructor, dependencies checked by DUCKY309) through a fully qualified AddReactiveEffect<T> call when that exact method symbol resolves, else DUCKY022; the OfActionType<T> arm of DUCKY005; generator tests reference the real Ducky.Reactive.dll. PackageSmoke gains its fifth consumer: it references only Ducky.Reactive, declares a ReactiveEffect and calls AddDuckyGenerated_*.
- **Acceptance tests:**
  - `Registration_RealReactive_EmitsAddReactiveEffect_NoDucky022`
  - `Generated_CompilesClean_{Shape}` (reactive effect rows, including one whose only constructor injects HttpClient)
  - DUCKY005 OfActionType-arm verifier tests (non-normative)
  - `PackageSmoke`: Reactive-only consumer compiles the generated AddReactiveEffect<T> call
- **Done:** Common DoD.

### M12 — Server persistence

PersistStorage.Server over IDistributedCache, scopes, retries and the phase-5a flush, the auth-driven scope switch with epochs, init-phase deadlines, reconnect absorption and attempt supersession (INV-17), static SSR pins, pause seeds across scopes; AOT smoke for server persistence and DevTools payload.

#### M12-01 IDistributedCache provider and PersistStorage.Server (M)

- **Stage:** 13
- **Depends on:** M6-13, M6-11
- **Unblocks:** M12-02, M12-03, M12-06
- **Area:** `src/Ducky.Blazor/Persistence/Providers/DistributedCache`
- **Invariants:** INV-17, INV-31
- **Scope:** Internal DistributedCacheProvider (key {prefix}:{scope}:{sliceKey}, UTF-8 envelope, MaxAge => AbsoluteExpirationRelativeToNow else ServerEntryOptions), DuckyScopes.NameIdentifier; Server keys use Scope ?? NameIdentifier, browser keys are scoped only when Scope is set explicitly; a null scope means no I/O (Info once). In the browser Server storage is inert (Info 2020, seed-only): inert slices count as not persisted (Status stays Hydrated), never count as scoped keys and never need an AuthenticationStateProvider. NonInteractive rule (read during init, write-through); debounce 0. DUCKY314/315 (skipped when IsBrowser) and DUCKY317 as AddValidation rules; Ducky.Blazor's EveryErrorCode_FollowsMessageTemplate now covers DUCKY310-317 and 352. Every NonInteractive Server-storage test runs against MemoryDistributedCache and a Task.Yield() wrapper.
- **Acceptance tests:**
  - `DistributedCache_TwoScopes_NeverSeeEachOther`
  - `DistributedCache_NullScope_NoIo`
  - `DistributedCache_AnonymousUser_NoIo`
  - `DistributedCache_DefaultPrefixWithServerStorage_IsConfigurationError`
  - `Browser_ServerStorageWithoutCacheOrAuth_NoConfigurationError`
  - `Server_ServerStorageWithoutCache_Ducky314`
  - `Precedence_ServerStorage_IndependentOfRegistrationOrder`
  - `Hydration_BrowserOnlyInertServerSlices_StatusHydrated`
  - `Scope_DefaultNeverAppliesToBrowserKeys`
  - `EveryErrorCode_FollowsMessageTemplate` (Ducky.Blazor.Tests: DUCKY310-317, 352)
- **Done:** Common DoD; tests use the real MemoryDistributedCache.

#### M12-02 Server flush, retries, late-flush loss and last-writer-wins pins (M)

- **Stage:** 13
- **Depends on:** M12-01
- **Unblocks:** M12-05
- **Area:** `test/Ducky.Blazor.Tests/DistributedCache/Flush, src/Ducky.Blazor/Persistence/Providers/DistributedCache/Flush, build/Build.CI.cs`
- **Invariants:** INV-16, INV-17, INV-29
- **Scope:** Server storage in phase 5a (no JS): provider failures retried with the 1 s to 30 s backoff while the store is alive (PersistenceFailed once per streak); the flush cancels pending backoff and makes one immediate attempt per dirty key. When the drain overruns DisposeTimeout, DisposeAsync completes at the bound and the chained flush catches ObjectDisposedException and provider exceptions per key and counts them ducky.persistence.lost. Documented last-writer-wins and prerender-flush ceilings. S-9 is re-measured at step 13.
- **Acceptance tests:**
  - `DistributedCache_DisposeFlushesPending`
  - `Dispose_ChangeInsideDebounce_FlushedImmediately_ServerStorage`
  - `DistributedCache_TwoCircuitsSameUser_LastWriterWins`
  - `DistributedCache_PrerenderFlushAfterCircuitWrite_Documented`
  - `DistributedCache_TransientCacheFailure_WriteRetriedUntilSuccess`
  - `Dispose_KeyInRetryBackoff_FlushWritesImmediately_ServerStorage`
  - `Dispose_DrainOverrunsTimeout_ServerScope_DisposeAsyncCompletesAtBound_LateFlushCountsLost`
  - S-9 re-measured at step 13: nightly-mutation and release-mutation timeout-minutes updated (docs/spec/spikes.md)
- **Done:** Common DoD.

#### M12-03 Scope switch core (M)

- **Stage:** 13
- **Depends on:** M12-01, M6-10b, M6-12
- **Unblocks:** M7-02b, M12-03b, M12-03c, M12-04
- **Area:** `src/Ducky.Blazor/Persistence/ScopeSwitch`
- **Invariants:** INV-14, INV-16, INV-17
- **Scope:** Subscribe to AuthenticationStateChanged before the epoch-0 resolution (server: synchronous prefix; browser: inside the hand-off callback, the provider resolved with GetService), unsubscribe in dispose phase 5a. Handler, synchronously under _issue: epoch = ++_scopeVersion, a new current attempt, the reset's PreviousState (the newest old-epoch snapshot, taken on the drainer through AfterReduce) queued to each scoped writer's final-snapshot queue and written under the old key before the loop reads Store.State, one Hydration restore of scoped initial states plus Hydrating{epoch}; the epoch records its key set. Then resolve the scope and record it for the epoch if still current; switch reads run as commands in each key's writer loop, after its final snapshot and any in-flight write; a null scope drops only the scoped keys (HydrationCompleted(false) at once when nothing else is carried). The handler never throws.
- **Acceptance tests:**
  - `ScopeSwitch_TwoOverlappingSwitches_DistinctEpochs_LatestWins`
  - `ScopeSwitch_ToAnonymous_StatusReturnsToHydrated_UnscopedWritesResume`
  - `DistributedCache_UserChangesInsideCircuit_ActionDuringSwitch_NeverWrittenUnderOtherScope`
  - `DistributedCache_UserChangeWithConcurrentLocalChange_NeverWritesOldStateUnderNewKey`
  - `Write_UnscopedChangeDuringScopeSwitch_IsWrittenAfterTerminal`
  - `Wasm_UserChange_ScopedBrowserKeysMoveToNewScope` (bUnit; its E2E twin is in M14-05)
  - `ScopeSwitch_ChangeCommittedJustBeforeSwitch_WrittenUnderOldKey`
  - `ScopeSwitch_SameScopeReasserted_FinalWriteNotRolledBack`
- **Done:** Common DoD.

#### M12-03b Scope switch during init and initial hydration (M)

- **Stage:** 13
- **Depends on:** M12-03, M6-08
- **Unblocks:** M12-04, M12-07
- **Area:** `src/Ducky.Blazor/Persistence/ScopeSwitch/Init`
- **Invariants:** INV-14, INV-17
- **Scope:** A switch's key set is its scoped keys plus the not-yet-restored unscoped keys of the attempt it supersedes (transitively); the writer's hydration skip and the reads use it, and a null-scope switch still reads carried unscoped keys through their writer loops. _initPhase, read and cleared only under _issue: set in step 3, cleared with the terminal of the attempt current during init or by the init-token callback (clearing disposes the init-phase timer); the init-phase HydrationTimeout deadline and the init-token callback act on whichever attempt is current and are no-ops once it is cleared; a switch arms its own timer iff _initPhase is clear, so a switch during init never outlives InitTimeout.
- **Acceptance tests:**
  - `ScopeSwitch_AuthChangeDuringInitialScopeResolution_NeverWritesUnderOldScope`
  - `ScopeSwitch_DuringInit_TerminalPrecedesStoreInitialized_WithinOneHydrationTimeout`
  - `ScopeSwitch_DuringInitialHydration_UnscopedKeysStillRestored`
  - `ScopeSwitch_ToAnonymousDuringInitialHydration_UnscopedKeysStillRestored`
  - `ScopeSwitch_JustAfterInitialTerminal_OwnTimerArmed_NotFailedByInitDeadline`
  - `ScopeSwitch_BetweenInitialTerminalAndInitReturn_HungRead_FailsAtOwnTimeout`
- **Done:** Common DoD.

#### M12-03c Scope switch: write retries, reconnect absorption, inert Server keys (M)

- **Stage:** 13
- **Depends on:** M12-03, M6-11, M6-12
- **Unblocks:** M14-02
- **Area:** `src/Ducky.Blazor/Persistence/ScopeSwitch/Reassert`
- **Invariants:** INV-16, INV-17
- **Scope:** A failed queued old-scope snapshot stays at the head of its queue and is retried with the same backoff, re-serialized against its own epoch's key and baseline; posting a switch read or enqueuing a final snapshot ends a backoff sleep. Re-assertion check before a switch: outside _issue the scope delegate is called once; if its ValueTask completed successfully and, under _issue, the current epoch's recorded scope is known, ordinally equal and its attempt has issued its terminal, the event is absorbed (no epoch bump, reset or reads; Debug log); every other case runs the switch. In the browser inert Server keys are never read and never join a switch, but the reset restores them; without an AuthenticationStateProvider there is no subscription.
- **Acceptance tests:**
  - `ScopeSwitch_FinalOldScopeWriteFailsTransiently_RetriedUnderOldKey`
  - `ScopeSwitch_WriterInRetryBackoff_SwitchReadCompletesWithinHydrationTimeout`
  - `ScopeSwitch_CircuitReconnectSameUser_NoResetNoReRead`
  - `Wasm_ServerStorageOnly_NoAuthProvider_NoSubscriptionNoThrow`
  - `Wasm_UserChange_InertServerSlicesResetNotRead`
- **Done:** Common DoD.

#### M12-04 Scope switch races (S)

- **Stage:** 13
- **Depends on:** M12-03, M12-03b
- **Unblocks:** M14-02
- **Area:** `test/Ducky.Concurrency.Tests/DistributedCache, test/Ducky.Blazor.Tests/DistributedCache/Races`
- **Invariants:** INV-17
- **Scope:** CsCheck SampleParallel in Ducky.Concurrency.Tests (§17.3 row 6) with a TCS-gated cache, and their …_Deterministic twins in Ducky.Blazor.Tests covering every branch.
- **Acceptance tests:**
  - `DistributedCache_StaleAttemptAfterSecondSwitch_NeverRestoredOrWrittenUnderNewScope` (Ducky.Concurrency.Tests)
  - `DistributedCache_StaleAttemptAfterSecondSwitch_NeverRestoredOrWrittenUnderNewScope_Deterministic`
  - `DistributedCache_UserChangeDuringInitialHydration_OldScopeValuesNeverRestored` (Ducky.Concurrency.Tests)
  - `DistributedCache_UserChangeDuringInitialHydration_OldScopeValuesNeverRestored_Deterministic`
- **Done:** Common DoD.

#### M12-05 AOT smoke: server persistence and DevTools payload (S)

- **Stage:** 15
- **Depends on:** M12-02, M8-02, M3-05, M9-06
- **Unblocks:** M11-04, M16-02
- **Area:** `test/Ducky.AotSmoke`
- **Invariants:** INV-23, INV-24
- **Scope:** Through public API only: PersistStorage.Server round-trip over MemoryDistributedCache (KeyPrefix smoke, fixed scope) and UseDevTools with a fake IJSRuntime (Trace = true) producing a non-empty payload. The round-trip extends the `AotSmoke` assertion (no new PASS name, P8); DevToolsPayload_NonEmpty is its own manifest name.
- **Acceptance tests:**
  - `DevToolsPayload_NonEmpty`
  - `AotSmoke` (now also covering the Server-storage round-trip)
- **Done:** Common DoD.

#### M12-06 NonInteractive Server storage and static SSR pins (S)

- **Stage:** 13
- **Depends on:** M12-01, M5-06
- **Unblocks:** M14-02
- **Area:** `test/Ducky.Blazor.Tests/DistributedCache/StaticSsr`
- **Invariants:** INV-17
- **Scope:** Server-storage values reach NonInteractive HTML only when a lifecycle method awaits InitializeAsync (DuckyInitializer does, in OnInitializedAsync); the seed carries them in any case (OnPersisting waits for idle). Pin the asynchronous IDistributedCache cases and the Post/Redirect/Get stale read (the request-scope flush runs after the redirect response); no public flush API in 2.0, a ponytail note names the 2.1 FlushPersistenceAsync upgrade path.
- **Acceptance tests:**
  - `StaticSsr_AsyncDistributedCache_DuckyInitializer_HtmlShowsStoredValue`
  - `StaticSsr_AsyncDistributedCache_WithoutInitializer_HtmlShowsInitialState_Documented`
  - `DistributedCache_StaticSsrPostRedirect_StaleReadDocumented`
- **Done:** Common DoD.

#### M12-07 Pause seeds across scopes (S)

- **Stage:** 13
- **Depends on:** M6-13b, M12-03b
- **Unblocks:** M16-04
- **Area:** `src/Ducky.Blazor/Prerender/PauseSeed/Scope`
- **Invariants:** INV-15, INV-17
- **Scope:** A pause records the SHA-256 of the captured scope (never the raw id), carries scoped keys only for a known non-null scope of snap.ScopeEpoch and leaves out every key of an attempt still Hydrating (read under _issue). Scoped keys of a pause seed are not restored in the synchronous prefix: they are handed through SeedSettled and applied in step 6 only when the epoch-0 scope matches, otherwise storage or InitialState is restored. The pause override applies only to browser-storage keys; Server storage wins. Internal IVT hook BlazorOptions.AfterSeedIdleHook.
- **Acceptance tests:**
  - `Circuit_ResumeUnderOtherUser_PauseSeedScopedSlicesNotRestoredOrWritten`
  - `Circuit_PauseDuringScopeSwitch_StorageNotOverwrittenWithInitialState`
- **Done:** Common DoD.

### M13 — Generators, analyzers and tombstones

Shared generator helpers, GEN-01 dispatch helpers, GEN-02 core registration (step 7) and its Blazor arms (Prerender<T> at stage 11, PersistAttributeDefaults<T> and Ducky.Blazor.Generated.Tests at stage 12), multi-assembly fixture (INV-25), the stage-7 analyzers (DUCKY001-025 arms whose APIs exist by step 6), DUCKYM001-M008 tombstones, stage-7 PackageSmoke assertions.

#### M13-01 Shared generator helpers, Ducky.Generators harness and AssemblyNameSanitizer (S)

- **Stage:** 7
- **Depends on:** M4-10
- **Unblocks:** M10-03, M13-02, M13-03, M13-06, M13-07, M13-09
- **Area:** `src/Shared.Generators, test/Ducky.Generators.Tests/Infrastructure`
- **Invariants:** INV-25
- **Scope:** src/Shared.Generators (P7): EquatableArray.cs, CodeWriter.cs (~40 lines, raw string literals), IdentifierSanitizer.cs and AssemblyNameSanitizer.cs (invalid chars => _, leading digit or keyword => _ prefix, null => Anonymous), one helper per file, linked per use. Ducky.Generators test infrastructure: CSharpGeneratorDriver, Verify.SourceGenerators, the real Ducky.dll (never stubs for gated symbols, GEN-06), a generated-compilation helper with DocumentationMode.Diagnose, nullable enabled and warning level 9999, and the caching-test helper over tracking names.
- **Acceptance tests:**
  - `AssemblyNameSanitizer_Table`
  - IdentifierSanitizer_Table (non-normative)
  - EquatableArray_ValueEquality (non-normative)
- **Done:** Common DoD. Deletes the Ducky.Generators.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M13-02 GEN-01 DispatchExtensionsGenerator (M)

- **Stage:** 7
- **Depends on:** M13-01
- **Unblocks:** M9-04c, M13-05, M13-11
- **Area:** `src/Ducky.Generators/Dispatch`
- **Invariants:** INV-25
- **Scope:** [DuckyAction] => {Record}DispatchExtensions (nested: A_B_RecordDispatchExtensions) under the GEN-06 rules (prologue, #nullable enable, summaries, the justified CS0612/CS0618 pragma, hint name {sanitized metadata name}.DispatchExtensions.g.cs, oblivious types treated as annotated): one overload pair per explicitly declared accessible constructor (the implicit parameterless one only when none is declared, never the record copy constructor; error-obsolete constructors skipped), defaults kept, parameter names verbatim with keywords @-escaped, receiver __dispatcher, minimum effective accessibility, MethodName. DUCKY020 (generic, nested in generic, inaccessible or file-local, error-obsolete, no usable constructor, generated name already declared), DUCKY021 (collisions with IDispatcher/IStore/EffectContext/object members => skipped; the TestStore arm ships in M9-04c), DUCKY024 (Info: two same-named, same-signature helpers in one compilation).
- **Acceptance tests:**
  - `Generated_CompilesClean_{Shape}` (positional/non-positional, defaults, record structs, nested at several depths, file-local, [Obsolete] and [Obsolete(error: true)] rows, global namespace, keyword and case-only names, #nullable disable rows)
  - `Helper_RecordStructWithDefaults_DispatchesDefaultValues`
  - `HintNames_UniqueAndValid` (Ducky.Generators.Tests)
  - `Descriptors_MatchCatalogue` (Ducky.Generators.Tests)
  - `Caching_{Step}_Cached` (dispatch steps)
  - DUCKY020, DUCKY021 and DUCKY024 verifier tests (non-normative)
- **Done:** Common DoD.

#### M13-03 GEN-02 RegistrationGenerator core (slices, effects) (M)

- **Stage:** 7
- **Depends on:** M13-01
- **Unblocks:** M11-05, M13-04, M13-05, M13-08, M13-11
- **Area:** `src/Ducky.Generators/Registration`
- **Invariants:** INV-25
- **Scope:** Ducky.Generated.{Asm}_DuckyRegistrations.AddDuckyGenerated_{Asm} (fixed hint name DuckyRegistrations.g.cs): every non-abstract, non-generic, accessible Slice<> with a public parameterless constructor (SliceStores included; DUCKY023 otherwise), and every Effect<>/EffectGroup with any public constructor (dependencies checked by DUCKY309); file-local slices and effects are DUCKY023. The DUCKY022 'type or method doesn't resolve' branch, tested with compilations that omit the Ducky.Blazor and Ducky.Reactive references. The Blazor and Reactive emission arms ship with their APIs (M13-04, M13-04b, M11-05; P10). Middleware is never registered.
- **Acceptance tests:**
  - `Generated_CompilesClean_{Shape}` (registration shapes, including an effect whose only constructor injects HttpClient)
  - `Caching_{Step}_Cached` (registration steps)
  - DUCKY022 (not-resolved branch) and DUCKY023 verifier tests (non-normative)
- **Done:** Common DoD.

#### M13-04 GEN-02 Prerender<T> emission (S)

- **Stage:** 11
- **Depends on:** M13-03, M6-04
- **Unblocks:** M13-04b
- **Area:** `src/Ducky.Generators/Registration/Blazor/Prerender`
- **Invariants:** INV-25
- **Scope:** When the exact Ducky.Blazor Prerender<T> method symbol resolves, emit a fully qualified Prerender<T>(b) call per [Prerender] slice, else DUCKY022 for it (§24 step 11, P10); generator tests reference the real Ducky.Blazor.dll, and the skew branch uses an older-shape fixture.
- **Acceptance tests:**
  - `Generated_CompilesClean_{Shape}` ([Prerender] rows against the real Ducky.Blazor)
  - DUCKY022 skew case for Prerender<T> through an older-shape fixture (non-normative)
- **Done:** Common DoD.

#### M13-04b GEN-02 PersistAttributeDefaults<T> emission and Ducky.Blazor.Generated.Tests (M)

- **Stage:** 12
- **Depends on:** M13-04, M6-03, M13-07, M13-11
- **Unblocks:** M14-01, M14-02, M15-04, M16-02
- **Area:** `src/Ducky.Generators/Registration/Blazor/Persist, src/Ducky.Generators/Analyzers/Structural/Persist, test/Ducky.Blazor.Generated.Tests, test/Ducky.PackageSmoke`
- **Invariants:** INV-25
- **Scope:** GEN-02 always emits PersistAttributeDefaults<T> for [Persist] (only explicitly supplied named arguments; an empty delegate for a bare [Persist]), gated on the exact method symbol (DUCKY022 on a mismatch); the Persist<T>() arm of DUCKY012 (§24 step 12, P10). New test/Ducky.Blazor.Generated.Tests, the one Blazor test project with DuckyUseGenerators (joins Ducky.slnx, Ducky.Tests.slnf, the coverage run and the computed Stryker test projects; IVT from Ducky.Blazor with the key), holding the three generator-emitted attribute tests and their shared, composable [Persist]/[Prerender] fixtures. PackageSmoke's Server consumer declares [Persist(Version = 2, SyncAcrossTabs = true)] and [Prerender] slices under TreatWarningsAsErrors.
- **Acceptance tests:**
  - `Registration_RealBlazor_EmitsPersistAndPrerender_NoDucky022`
  - `PersistAttribute_NoArguments_SlicePersisted`
  - `PersistAttributePlusBuilderMigrate_Composes`
  - `AddBlazor_AfterGeneratedPersist_ConfigureApplied`
  - DUCKY012 Persist<T>()-arm verifier tests (non-normative)
  - `PackageSmoke`: Server consumer with [Persist]/[Prerender] compiles from packages (no DUCKY022)
- **Done:** Common DoD.

#### M13-05 Multi-assembly fixture (Ducky generators) (S)

- **Stage:** 7
- **Depends on:** M13-02, M13-03
- **Unblocks:** M16-04
- **Area:** `test/Ducky.Generators.Tests/MultiAssembly`
- **Invariants:** INV-25
- **Scope:** §16.3: Lib (MyCompany.Todos) and App both run the generator and declare Increment in different namespaces; Tests references both without the generator and calls both registrations (extension and explicit) and both Increment helpers; TreatWarningsAsErrors. App also declares a second Increment in another App namespace, and the fixture asserts DUCKY024 on it.
- **Acceptance tests:**
  - `MultiAssembly_NoAmbiguityOrConflict`
- **Done:** Common DoD.

#### M13-06 Purity analyzers DUCKY001-003 (M)

- **Stage:** 7
- **Depends on:** M13-01, M3-03
- **Unblocks:** M9-01c, M10-07b, M15-04
- **Area:** `src/Ducky.Generators/Analyzers/Purity`
- **Invariants:** —
- **Scope:** Analyse On<T> reducer lambdas and Selector.Define projectors (the reducer and projector arms, §24 step 7): non-determinism (001), I/O or blocking (002), mutation (003; nested recipe parameters are not state). The SliceStore mutator arms ship in M9-01c and the Draft-receiver exemption of DUCKY003 in M10-07b (P10). Generated code not analysed. False-positive corpus over the existing snippets.
- **Acceptance tests:**
  - DUCKY001 verifier tests, reducer and projector arms (non-normative)
  - DUCKY002 verifier tests, reducer and projector arms (non-normative)
  - DUCKY003 verifier tests, reducer and projector arms (non-normative)
  - Purity_FalsePositiveCorpus_ZeroHits (non-normative)
- **Done:** Common DoD; AnalyzerReleases.Unshipped updated; docs/diagnostics pages.

#### M13-07 Structural analyzers DUCKY005, 011, 012, 013, 014, 015 (M)

- **Stage:** 7
- **Depends on:** M13-01
- **Unblocks:** M11-05, M13-04b, M15-04
- **Area:** `src/Ducky.Generators/Analyzers/Structural`
- **Invariants:** —
- **Scope:** 005 non-concrete T in On<T>/EffectGroup.On<T>/Effect<T> (the OfActionType<T> arm ships in M11-05); 011 `s with { }` in On<T>; 012 slice with [Persist] relying on the derived Key (the Persist<T>() arm ships in M13-04b); 013 ConcurrencyKey without value equality; 014 duplicate On<T>; 015 two slices share a TState.
- **Acceptance tests:**
  - DUCKY005 verifier tests (non-normative)
  - DUCKY011 verifier tests (non-normative)
  - DUCKY012 verifier tests (non-normative)
  - DUCKY013 verifier tests (non-normative)
  - DUCKY014 verifier tests (non-normative)
  - DUCKY015 verifier tests (non-normative)
- **Done:** Common DoD.

#### M13-08 Usage analyzers DUCKY004, 006, 008, 010, 016, 025 (M)

- **Stage:** 7
- **Depends on:** M13-03
- **Unblocks:** M9-01c, M13-11, M15-04
- **Area:** `src/Ducky.Generators/Analyzers/Usage`
- **Invariants:** —
- **Scope:** 004 Singleton in a non-WASM web project (build_property options from Ducky.targets); 006 [DuckyAction]/state types outside the set of T for which the UseJson context type declares a JsonTypeInfo<T> property (union over Combine; a DiagnosticAnalyzer, since it must see STJ generator output); 008 IStore/IDispatcher injected into effect, effect-group or middleware constructors; 010 blocking on DispatchAsync/WhenIdleAsync/InitializeAsync (the SetAsync arm ships in M9-01c); 016 AddDucky without the assembly's own AddDuckyGenerated_{Asm}; 025 UseJson(JsonTypeInfoResolver.Combine(...)) where a combined context's [JsonSourceGenerationOptions] sets a non-default option (a named argument or JsonSerializerDefaults other than General), with the fix in the message.
- **Acceptance tests:**
  - DUCKY004 verifier tests (non-normative)
  - DUCKY006 verifier tests (non-normative)
  - DUCKY008 verifier tests (non-normative)
  - DUCKY010 verifier tests (non-normative)
  - DUCKY016 verifier tests (non-normative)
  - DUCKY025 verifier tests (non-normative)
- **Done:** Common DoD.

#### M13-09 DUCKY009 Select in markup/lifecycle (S)

- **Stage:** 10
- **Depends on:** M13-01, M5-05
- **Unblocks:** M15-04
- **Area:** `src/Ducky.Generators/Analyzers/Razor`
- **Invariants:** INV-20
- **Scope:** ConfigureGeneratedCodeAnalysis(Analyze | ReportDiagnostics); flag Select in BuildRenderTree, OnParametersSet(Async), OnAfterRender(Async) and event handlers of DuckyComponent/DuckyLayout subclasses at the #line-mapped .razor location; tested with the real Razor source generator in the driver.
- **Acceptance tests:**
  - DUCKY009 verifier tests with the Razor generator (non-normative)
- **Done:** Common DoD.

#### M13-10 1.x tombstones DUCKYM001-M008 (S)

- **Stage:** 7
- **Depends on:** M4-10, M0-02
- **Unblocks:** M13-11, M15-04, M15-05
- **Area:** `src/Ducky/Migration, src/Ducky.Blazor/Migration`
- **Invariants:** —
- **Scope:** Member-less interfaces with error-level [Obsolete(DiagnosticId, UrlFormat)] and [EditorBrowsable(Never)] in their 1.x namespaces, rebuilt from the 1.0.292 public API (§22.1): M001 IState/IKeyedAction, M002 SliceReducers<TState>/ISlice<TState>, M003 AsyncEffect<TAction>/IAsyncEffect, M004 IReactiveEffect, M005 MemoizedSelector, M006 the Fsa records, M007 NormalizedState<TKey,TEntity,TState>, M008 DuckyLayout<TState>; dev-only types get none; no IMiddleware tombstone. PublicAPI entries; docs/diagnostics/DUCKYM00n.md; coverage reports nothing uncovered (S-2 shape).
- **Acceptance tests:**
  - Tombstones_DeriveFromEach_ReportsDuckyM00n (non-normative, compilation test)
  - `PackageSmoke` Server consumer implementing Microsoft.AspNetCore.Http.IMiddleware stays unambiguous
- **Done:** Common DoD.

#### M13-11 PackageSmoke generator assertions (S)

- **Stage:** 7
- **Depends on:** M13-02, M13-03, M13-08, M13-10
- **Unblocks:** M11-05, M13-04b, M16-02
- **Area:** `test/Ducky.PackageSmoke`
- **Invariants:** —
- **Scope:** The stage-7 PackageSmoke assertions (§17.9 is staged): the WASM and Server consumers call AddDuckyGenerated_PackageSmoke() and a generated dispatch helper; the Ducky.Blazor-only consumer compiles a generated helper and AddDuckyGenerated_* (generator and buildTransitive targets flow, R-PKG-5); its Singleton variant and the Server Singleton variant raise DUCKY004 under -warnaserror; adding Ducky.Generator raises DUCKY900.
- **Acceptance tests:**
  - `PackageSmoke`: Blazor-only consumer compiles generated helpers
  - `PackageSmoke`: Server Singleton variant fails with DUCKY004
  - `PackageSmoke`: Blazor-only consumer + Ducky.Generator fails with DUCKY900
- **Done:** Common DoD.

### M14 — Samples, E2E and AOT

Samples.Wasm and Samples.Server (Interactive Auto), every Playwright test of §17.6 at stage 18, and the trimmed AOT-WASM smoke that can't skip in the aot job.

#### M14-01 Samples.Wasm (M)

- **Stage:** 18
- **Depends on:** M8-03, M7-02, M9-01, M13-04b
- **Unblocks:** M14-03, M14-04, M14-06
- **Area:** `samples/Ducky.Samples.Wasm`
- **Invariants:** —
- **Scope:** Workload-free WASM standalone sample: todos with slices, effects, a SliceStore, generated registration, JsonSerializerContext, Persist Local + SyncAcrossTabs, Prerender-free, UseDevTools guarded by IsDevelopment; pages used by E2E and the trimmed smoke.
- **Acceptance tests:**
  - `Sample` builds in Ducky.slnx with no workload
  - Purity_FalsePositiveCorpus_ZeroHits still green over the sample
- **Done:** Common DoD (samples are not coverage-gated; analyzers and warnings still apply).

#### M14-02 Samples.Server (+ .Client, Interactive Auto) (M)

- **Stage:** 18
- **Depends on:** M12-04, M8-03, M13-04b, M12-06, M12-03c
- **Unblocks:** M14-03, M14-04, M14-05, M15-03
- **Area:** `samples/Ducky.Samples.Server`
- **Invariants:** —
- **Scope:** Interactive Auto web app with shared .Client registration: Prerender slices loaded by effects, Local/Session/Server persistence (MemoryDistributedCache, KeyPrefix set, auth with a test login), cross-tab, DevTools, a DelegatingHandler page, large-slice and CJK pages for E2E. Includes a NameIdentifier-scoped Server-storage slice and a page that drops and restores the circuit connection.
- **Acceptance tests:**
  - `Sample` builds and starts with the E2E host fixture (health endpoint)
- **Done:** Common DoD.

#### M14-03 E2E: persistence, prerender and circuit resume (M)

- **Stage:** 18
- **Depends on:** M14-01, M14-02
- **Unblocks:** M16-02
- **Area:** `test/Ducky.E2E/Persistence`
- **Invariants:** INV-14, INV-15, INV-17, INV-23
- **Scope:** Playwright specs against both samples (stage 18), including the S-5 and S-7 regression twins.
- **Acceptance tests:**
  - `Persist_ReloadRestores_WasmAndServer`
  - `Persist_LargeSlice_OnServer_RoundTrips`
  - `Prerender_NoDoubleLoad_NetworkCalledOnce`
  - `Prerender_LargeSlice_InteractiveServer_CircuitStarts`
  - `Prerender_SeedJustUnderBudget_QuoteDenseAndCjk_InteractiveServer_CircuitStarts`
  - `Circuit_ResumeAfterDisconnect_PauseSeedWinsOverStaleLocalStorage`
  - `Circuit_Resume_SeedRestoresPrerenderSlices`
  - `Gate_Unknown_ProbeDetectsPrerender`
  - `Prerender_ServerAndWasmModesConfigured_SeedPersistedAndApplied`
  - `Circuit_ReconnectWithScopedSlices_NoResetFlicker`
- **Done:** Common DoD.

#### M14-04 E2E: cross-tab (M)

- **Stage:** 18
- **Depends on:** M14-01, M14-02
- **Unblocks:** M16-02
- **Area:** `test/Ducky.E2E/CrossTab`
- **Invariants:** INV-18, INV-23
- **Scope:** Two-page and disconnect/resume scenarios.
- **Acceptance tests:**
  - `CrossTab_TwoPages_ChangeInAAppearsInB`
  - `CrossTab_SubscriberThatRedispatches_ConvergesWithinNWrites`
  - `CrossTab_ListenerRegisteredOnceAcrossReinit`
  - `CrossTab_ClearFromOtherScript_ResetsSyncedSlices`
  - `CrossTab_ClearPersistedStateInOtherTab_ResetsSlice`
  - `CrossTab_LargeValue_ReadViaStream`
  - `CrossTab_NonAsciiValueUnderCharLimitOverByteLimit_UsesStream`
  - `CrossTab_EventDuringCircuitDisconnect_ConvergesAfterReconnect`
  - `CrossTab_CircuitResumed_OldListenerRemoved`
  - `CrossTab_TooLargeKeyRemovedBeforePull_RealRuntime_ResetsSlice`
- **Done:** Common DoD.

#### M14-05 E2E: Interactive Auto, store identity and DevTools (M)

- **Stage:** 18
- **Depends on:** M14-02
- **Unblocks:** M16-02
- **Area:** `test/Ducky.E2E/Auto`
- **Invariants:** INV-17, INV-18, INV-22, INV-28
- **Scope:** Circuit and WASM stores in one document, shared registration, handler-scope dispatch, user switch, fake extension.
- **Acceptance tests:**
  - `Auto_ServerAndWasmStoresInOneDocument_Converge`
  - `Auto_SessionAndUnsyncedLocalSlices_ConvergeInOneDocument`
  - `Auto_ClearInCircuitStore_ResetsWasmStore`
  - `Auto_SharedServerPersistRegistration_WasmStartsAndSkipsServerSlices`
  - `CrossTab_CircuitAndWasmStoresInSamePage_IndependentListeners`
  - `Wasm_DelegatingHandlerDispatch_ReachesUiStore`
  - `Wasm_UserChange_ScopedBrowserKeysMoveToNewScope`
  - `DevTools_FakeExtension_ReceivesSanitizedPayloads`
- **Done:** Common DoD.

#### M14-06 Trimmed AOT-WASM smoke (S)

- **Stage:** 18
- **Depends on:** M14-01, M0-08, M11-04
- **Unblocks:** M16-04
- **Area:** `build/Build.Aot.cs, test/Ducky.E2E/Aot`
- **Invariants:** INV-24
- **Scope:** AotSmoke target from stage 18: publish Samples.Wasm with -p:RunAOTCompilation=true -p:TrimMode=full -p:RestoreLockedMode=false -o artifacts/wasm-trimmed, install Playwright Chromium, run Ducky.E2E with --filter-method "*.WasmTrimmedSmoke", DUCKY_REQUIRE_TRIMMED_PUBLISH=1 and DUCKY_TRIMMED_PUBLISH_DIR; the fixture skips only when DUCKY_REQUIRE_TRIMMED_PUBLISH is unset; the target parses the TRX and fails unless exactly one test ran and passed with zero skipped. The test injects a fake __REDUX_DEVTOOLS_EXTENSION__: a non-empty todos/added payload, a persisted slice that survives a reload, a Trace stack (possibly <unknown> frames).
- **Acceptance tests:**
  - `WasmTrimmedSmoke`
  - Planted check: a publish written to another directory fails WasmTrimmedSmoke in the aot job instead of skipping it
- **Done:** Common DoD; aot job green.

### M15 — Docs and migration guides

docfx with compiled-snippet regions, user guides, threading.md, a complete diagnostics catalogue checked by Docs, and the seven migration guides with compiled before/after samples and the 1.x API list check.

#### M15-01 Docs infrastructure (M)

- **Stage:** 18
- **Depends on:** M9-05, M0-11
- **Unblocks:** M15-02, M15-03, M15-04, M15-05, M15-06, M15-07
- **Area:** `docs/docfx.json, test/Ducky.Docs.Tests, build/Build.Gates.cs#Docs, .github/workflows/docs.yml`
- **Invariants:** —
- **Scope:** docfx.json; Ducky.Docs.Tests replaces its skeleton region test (every snippet is a #region of a compiled, tested file); Ducky.Migration.slnx scaffold with per-guide Before/After projects whose docs/migration/samples/Directory.Build.props imports the root props and sets ManagePackageVersionsCentrally=false, RestoreLockedMode=false and NuGetAudit=false; full Docs target (builds Ducky.Migration.slnx without a lock file, fence check, docfx to artifacts/docs); hand-written docs.yml (on release: Docs + Pages with the WASM sample).
- **Acceptance tests:**
  - `Docs` target builds docfx with a region include from Ducky.Docs.Tests
  - Planted check: a fenced csharp block fails Docs
- **Done:** Common DoD. Deletes the Ducky.Docs.Tests Skeleton_{Project}_Smoke test and its manifest entry (§24 step 1).

#### M15-02 Core guides and threading.md (M)

- **Stage:** 18
- **Depends on:** M15-01
- **Unblocks:** M16-05
- **Area:** `docs/guides/core, docs/threading.md, test/Ducky.Docs.Tests/Core`
- **Invariants:** —
- **Scope:** Getting started (direct Ducky PackageReference rule, R-PKG-5), slices and keys, effects and policies, selectors, SliceStore, EntityState, testing with Ducky.Testing, threading.md with the §7 text verbatim and the liveness ceilings.
- **Acceptance tests:**
  - Every guide snippet is a compiled, tested region (Docs target green)
  - ThreadingDoc_MatchesIDispatcherXmlDocs (non-normative)
- **Done:** Common DoD.

#### M15-03 Blazor, Reactive and Draft guides (M)

- **Stage:** 18
- **Depends on:** M15-01, M14-02
- **Unblocks:** M16-05
- **Area:** `docs/guides/blazor, test/Ducky.Docs.Tests/Blazor`
- **Invariants:** —
- **Scope:** Render-mode matrix and the one load-effect rule, persistence (browser and server, scopes, ceilings), prerender (seed budget, Effect<T> rule), cross-tab, DevTools (IsDevelopment guard), Reactive recipes (debounced search, polling, GroupByUntil), Draft usage.
- **Acceptance tests:**
  - Every guide snippet is a compiled, tested region (Docs target green)
- **Done:** Common DoD.

#### M15-04 Diagnostics catalogue completeness (S)

- **Stage:** 18
- **Depends on:** M15-01, M13-06, M13-07, M13-08, M13-09, M13-10, M10-07, M13-04b, M11-05, M10-07b, M9-01c, M9-04c
- **Unblocks:** M16-04
- **Area:** `docs/diagnostics`
- **Invariants:** —
- **Scope:** Every DUCKY/DUCKYM ID has a page whose bad-example snippets are regions over the analyzer verifier tests' raw strings; HelpLinkUri of every descriptor points to its page; rerun the DUCKY001-003 false-positive corpus over all docs snippets. Adds the Docs check that fails when an ID of §8.2 or §16.2 (DUCKY001-025, DUCKY101-109, DUCKY300-353, DUCKY900-902, DUCKYM001-M008) has no docs/diagnostics/{ID}.md.
- **Acceptance tests:**
  - EveryDiagnostic_HasDocsPageAndHelpLink (non-normative)
  - Purity_FalsePositiveCorpus_ZeroHits (non-normative)
  - Planted check: deleting docs/diagnostics/DUCKY025.md fails Docs
- **Done:** Common DoD.

#### M15-05 Migration guide: Ducky 1.x (M)

- **Stage:** 18
- **Depends on:** M15-01, M13-10
- **Unblocks:** M16-05
- **Area:** `docs/migration/ducky-1x.md, docs/migration/samples/ducky-1x`
- **Invariants:** —
- **Scope:** Mapping table, behaviour changes each with a test, checklist (§22.1; step 3 adds a possibly empty context passed with UseJson(AppJson.Default)), compiled Before (Ducky 1.x from nuget.org) and After projects. Rows for DuckyComponent.Dispatcher.X(), GetSliceState<T>/GetSlice<T>/GetSliceByKey/TryGetSlice/GetSliceKeys, OnAfterSubscribed(), StateChanged, AddDucky(IConfiguration, …)/AddDuckyCore/DuckyOptions, ActionStream/LastAction and Ducky.Blazor.Router (no equivalent). Commits docs/migration/ducky-1x-api.txt (the 1.0.292 public types) and adds the Docs check that fails on a listed type with neither a tombstone nor a mapping row.
- **Acceptance tests:**
  - `Ducky`.Migration.slnx builds the ducky-1x Before and After samples
  - `After` sample tests pass
  - Planted check: removing the mapping row of a listed 1.x type fails Docs
- **Done:** Common DoD.

#### M15-06 Migration guides: Bustand, TheBlazorState, BlazorMVU (M)

- **Stage:** 18
- **Depends on:** M15-01
- **Unblocks:** M16-04
- **Area:** `docs/migration/{bustand,theblazorstate,blazormvu}`
- **Invariants:** —
- **Scope:** §22.2 as a table (SetAsync(Func) => SetAsync(Func), SetAsync(TState) => SetAsync(_ => value), an I/O-awaiting store method => an effect; rows for Subscribe/Subscribe<TSlice>/StateChanged, IsInitialized, OnStateChanged, IMiddleware/IAsyncMiddleware, ZustandComponentScoped/ZustandScope and IStore<TState>); the UserStore sample declares SetUserAsync wrapping SetAsync, loaded by an Effect<StoreInitialized>. §22.3-22.4 with compiled before/after samples.
- **Acceptance tests:**
  - `Ducky`.Migration.slnx builds the three Before/After pairs
- **Done:** Common DoD.

#### M15-07 Migration guides: Mutty, DuckyNext, blazor-state (S)

- **Stage:** 18
- **Depends on:** M15-01, M10-07, M10-07b
- **Unblocks:** M16-04
- **Area:** `docs/migration/{mutty,duckynext,blazor-state}`
- **Invariants:** —
- **Scope:** §13.4 and §22.5-22.6; the Mutty guide states the net10.0 requirement and the unmaintained last Mutty. The Produce row reads 'unchanged for scalar and nested-record edits; collection edits per the rows below', with rows for List<T>/List<MutableT> members, IReadOnlyList<T>/IReadOnlyCollection<T>, queue/stack/sorted-dictionary members (DUCKY109) and record struct (DUCKY101); the sample includes a Find-based recipe and a queue member.
- **Acceptance tests:**
  - `Ducky`.Migration.slnx builds the Mutty Before/After pair
- **Done:** Common DoD.

### M16 — Release candidate

Nightly audit/benchmarks, mutation budget (S-9) and score >= 90, the three-job release pipeline (trusted publishing), spec closure (activeStage 19, SpecTraceGate --strict), v2.0.0-rc.1 with the two consumer migrations, and the GA runbook.

#### M16-01 Nightly audit and benchmarks (M)

- **Stage:** 19
- **Depends on:** M4-10, M0-11
- **Unblocks:** —
- **Area:** `benchmarks/, build/Build.Audit.cs, .github/workflows/nightly-audit.yml`
- **Invariants:** —
- **Scope:** Audit target (dotnet restore Ducky.slnx -p:NuGetAudit=true -p:NuGetAuditMode=all with NU1901-NU1904 as errors, <NuGetAuditSuppress> items only, never over Ducky.Migration.slnx), benchmarks/Ducky.Benchmarks with committed baseline and Benchmarks target (report only), nightly-audit workflow via Build.CI.cs.
- **Acceptance tests:**
  - `VerifyWorkflows` green with nightly-audit
  - `Audit` fails on a planted vulnerable package
- **Done:** Common DoD.

#### M16-02 Mutation budget (S-9) and score to >= 90 (M)

- **Stage:** 19
- **Depends on:** M13-11, M12-05, M11-04, M10-07, M14-03, M14-04, M14-05, M11-05, M13-04b, M10-07b
- **Unblocks:** M16-04
- **Area:** `src/*/stryker-config.json, build/Build.CI.cs`
- **Invariants:** —
- **Scope:** Final S-9 measurement of the full run on ubuntu-latest; set nightly-mutation/release-mutation timeout-minutes with 30% headroom under 360 or split the nightly per project (Mutation --project); kill surviving mutants with tests, not disables, until every project is >= 90; systematic survivors per §17.8 (the S-6 ConfigureAwait answer, Descriptors_MatchCatalogue for descriptor strings).
- **Acceptance tests:**
  - Nightly Mutation: every project >= 90
  - S-9 recorded in docs/spec/spikes.md
- **Done:** Common DoD.

#### M16-03 Release pipeline (M)

- **Stage:** 19
- **Depends on:** M0-11
- **Unblocks:** M16-05
- **Area:** `build/Build.Release.cs, .github/workflows/release.yml, cliff.toml`
- **Invariants:** —
- **Scope:** Targets Changelog (git-cliff, --release-version via the ReleaseVersion parameter), Publish (no DependsOn, ordered after the gates; Pack's exact-set check on the downloaded packages; v* tag; git merge-base --is-ancestor against origin/$ReleaseBranch; dotnet nuget push artifacts/packages/*.nupkg and .snupkg with --source https://api.nuget.org/v3/index.json --api-key $NUGET_API_KEY --skip-duplicate; the tombstone push only for the exact v2.0.0 tag), GitHubRelease (gh with GH_TOKEN, prerelease flag), ReleaseGates (Ci, E2E, AotSmoke; SpecTraceGate --strict for -rc.N and GA tags; uploads artifacts/packages and artifacts/tombstone), MutationForSha (passes on a successful nightly-mutation run for the SHA, else runs Mutation, --strict at rc/GA; fails, never falls back, when the query errors), Release (local). Hand-written release.yml with three jobs: release-gates, release-mutation (contents: read, actions: read, issues: write) and publish (environment nuget with approval, NuGet/login trusted publishing, pinned git-cliff, `./build.sh Publish GitHubRelease` without --skip); fetch-depth: 0 on every job. Can be scheduled early to publish milestone alphas of the active stage.
- **Acceptance tests:**
  - Tag v2.0.0-alpha.1 publishes prerelease packages after approval, without the tombstone
  - `Publish` refuses a tag not reachable from origin/$ReleaseBranch
  - Planted check: a failing gh query makes MutationForSha fail instead of starting a full run
- **Done:** Common DoD.

#### M16-04 Spec closure (S)

- **Stage:** 19
- **Depends on:** M16-02, M15-04, M1-14, M2-07, M2-09, M3-02, M3-04, M4-04, M6-09, M7-02b, M7-03, M8-03b, M9-01b, M9-02, M9-04b, M11-01b, M11-02b, M11-03, M12-07, M13-05, M14-06, M15-06, M15-07
- **Unblocks:** M16-05
- **Area:** `docs/spec/tests.yaml, build/Build.SpecTrace.cs`
- **Invariants:** —
- **Scope:** activeStage reaches 19 (P1): every manifest entry active, SpecTraceGate --strict green over all 32 invariants, Mutation --strict green on all seven projects; public API diff reviewed against §5, §11.1, §12-15.
- **Acceptance tests:**
  - `SpecTraceGate` --strict green with activeStage 19
  - Planted check: an entry with a stage above activeStage fails --strict
  - `Ci` green with every manifest test active
- **Done:** Common DoD.

#### M16-05 v2.0.0-rc.1 and consumer migrations (M)

- **Stage:** 19
- **Depends on:** M16-03, M16-04, M15-05, M15-02, M15-03
- **Unblocks:** M16-06
- **Area:** `release (no repo code)`
- **Invariants:** —
- **Scope:** Tag v2.0.0-rc.1; migrate NeoSocial (Ducky 1.0.291 / Ducky.Blazor 1.0.292) and BlazorProtectedStorage (1.0.224) with the guide against the rc packages; record time in the guide; file issues for gaps.
- **Acceptance tests:**
  - NeoSocial tests pass on rc packages
  - BlazorProtectedStorage tests pass on rc packages
- **Done:** RC accepted; follow-up issues filed and triaged.

#### M16-06 GA runbook (S)

- **Stage:** 19
- **Depends on:** M16-05
- **Unblocks:** —
- **Area:** `docs/release.md`
- **Invariants:** —
- **Scope:** Release PR (ShipPublicApi, Changelog, no baseline), tag v2.0.0, SetBaseline PR after the push, rename main to release/1.x and v2 to main, protect-branch.sh for main, nuget.org deprecations (Ducky 1.x, Ducky.Generator, Mutty) and repo retirements (§21).
- **Acceptance tests:**
  - Runbook dry-run checklist reviewed by the owner
- **Done:** Owner executes at GA.

## 7. Invariant → story index

| Invariant | Stories |
|---|---|
| INV-01 | M1-05, M1-13, M1-14 |
| INV-02 | M1-05, M1-13, M1-14, M3-01b, M4-09 |
| INV-03 | M1-05, M1-05b, M1-13, M1-14, M4-09, M5-02, M6-11, M11-01b |
| INV-04 | M1-10, M1-13, M1-14, M4-04 |
| INV-05 | M1-13, M3-04, M4-03, M4-03b, M4-04, M4-05b |
| INV-06 | M1-06, M1-07, M2-09, M4-02, M9-01b |
| INV-07 | M1-04, M1-05, M1-13, M1-14 |
| INV-08 | M1-05, M4-01b |
| INV-09 | M3-01, M3-01b, M3-04 |
| INV-10 | M1-05, M1-11, M1-14, M2-03, M4-10, M9-01b |
| INV-11 | M2-01, M2-02, M2-03, M2-03b, M2-04, M2-05, M2-06, M2-07, M9-01, M9-01b |
| INV-12 | M1-07, M2-02, M4-01, M4-02, M11-02 |
| INV-13 | M1-10, M1-11, M1-12, M2-03, M3-01, M4-01b, M4-03, M4-03b, M4-03c, M4-04, M11-01b |
| INV-14 | M6-02, M6-03, M6-07, M6-08, M12-03, M12-03b, M14-03 |
| INV-15 | M5-04, M6-04, M6-05, M6-06, M6-13, M6-13b, M9-02, M11-03, M12-07, M14-03 |
| INV-16 | M6-10, M6-10b, M6-11, M6-12, M7-02b, M7-03, M12-02, M12-03, M12-03c |
| INV-17 | M7-02b, M12-01, M12-02, M12-03, M12-03b, M12-03c, M12-04, M12-06, M12-07, M14-03, M14-05 |
| INV-18 | M7-01, M7-02, M7-02b, M7-03, M14-04, M14-05 |
| INV-19 | M5-03, M5-05, M5-06, M5-04 |
| INV-20 | M5-05, M13-09 |
| INV-21 | M3-01, M3-02, M3-03 |
| INV-22 | M0-02, M1-09, M4-09, M5-02, M14-05 |
| INV-23 | M4-06, M4-07, M5-02, M6-01, M6-09, M7-02, M8-01, M8-02, M8-03b, M12-05, M14-03, M14-04 |
| INV-24 | M0-08, M3-05, M4-07, M9-06, M11-04, M12-05, M14-06 |
| INV-25 | M10-03, M10-04, M10-05, M10-07, M11-05, M13-01, M13-02, M13-03, M13-04, M13-04b, M13-05 |
| INV-26 | M10-01, M10-02, M10-04, M10-05, M10-06, M10-07, M10-07b |
| INV-27 | M11-01, M11-02, M11-02b, M11-03 |
| INV-28 | M8-01, M8-02, M8-03, M8-03b, M14-05 |
| INV-29 | M1-11, M2-03b, M4-01, M4-03c, M4-04, M4-05b, M6-11, M12-02 |
| INV-30 | M9-01, M9-01b |
| INV-31 | M1-03, M1-08, M2-01, M2-07, M2-08, M4-05, M4-05b, M6-03, M11-01b, M12-01 |
| INV-32 | M9-03 |

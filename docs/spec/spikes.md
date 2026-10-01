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

## S-3: the AOT-clean prerender seed path (M0-13)

**Question.** Does a trimmed AOT WebAssembly app persist and take a `byte[]` seed with zero IL warnings, through a
`JsonTypeInfo`-based `PersistentComponentState` API if .NET 10 has one, or through the audited `byte[]` suppression?

**Setup.** `spikes/blazor` (one Blazor Web App, `SPIKE_MODES=wasm`), run 2026-10-01 on macOS arm64 with SDK 10.0.401,
ASP.NET Core 10.0.12 and Chromium (Playwright 1.63). The prerender pass registers `RegisterOnPersisting(…,
RenderMode.InteractiveAuto)` and writes `PersistAsJson<byte[]>("ducky:seed", utf8)`; the WASM store (a singleton with
its own store scope) takes it with `TryTakeFromJson<byte[]>`. The client is published with `-p:RunAOTCompilation=true`
and `TrimMode=full` (42 assemblies AOT-compiled), once with the suppression and once without (`-p:SeedPath=Json`).
The system SDKs had no usable wasm-tools (10.0.302's workload set is missing manifests, 10.0.401 has none), so the AOT
publish used a private 10.0.401 with wasm-tools in a scratch directory; nothing was installed system-wide.

**Answer: yes, through the audited suppression.** .NET 10.0.12 has no `JsonTypeInfo` overload: the public surface of
`PersistentComponentState` is `RegisterOnPersisting` (with and without a render mode), `RegisterOnRestoring`,
`PersistAsJson<T>` and `TryTakeFromJson<T>`, both `[RequiresUnreferencedCode]` and neither `[RequiresDynamicCode]`.
`PersistAsBytes`/`TryTakeBytes` exist but are internal, and `PersistentComponentStateSerializer<T>` only serves
`[PersistentState]` properties. With one `[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "byte[] is
intrinsic to STJ")]` on each of the two seed calls, the AOT publish reports no IL warning from the app's code, and the
AOT app takes the prerender seed (`taken through the store scope: {"src":"prerender",…}`). Without it, the two call
sites report IL2026 twice each (the trim analyzer at build, ILLink at publish). No IL3050 suppression is needed.

**Consequences.**

- §10's audited suppression stands, as one IL2026 justification carried by exactly two attributes, because
  `UnconditionalSuppressMessage` applies per member: one on the member making the persist call, one on the member making
  the take call, and no IL3050. §10's bullet now says so. The seed stays `PersistAsJson<byte[]>` (base64 inside the
  state dictionary), so §11.4's wire estimate is unchanged.
- **The Blazor WebAssembly SDK hides trim warnings by default**: it sets `SuppressTrimAnalysisWarnings=true` unless
  `TrimmerDefaultAction` is `link`, so a trimmed publish reports no IL warning at all. `WasmTrimmedSmoke`'s publish in
  the `AotSmoke` target (M14-06) must pass `-p:SuppressTrimAnalysisWarnings=false`. This and the next three bullets
  amend the publish arguments of §10's `Samples.Wasm` smoke bullet and of §19's `AotSmoke` row, and M14-06.
- Every trimmed Blazor WASM publish on 10.0.12 then reports IL2104 for `Microsoft.AspNetCore.Components` and
  `Microsoft.JSInterop`, the framework's own warnings (with `TrimmerSingleWarn=false`: IL2065, IL2072 and IL2111 inside
  `DotNetDispatcher`, `ComponentFactory`, `ComponentProperties` and `CascadingParameterState`). They fail the publish
  under warnings as errors, so the target passes `-p:ILLinkTreatWarningsAsErrors=false`, fails unless the output reports
  IL2104 for both assemblies (the positive control: with the warnings hidden the output is empty and a scan for unwanted
  warnings alone would pass), and fails on any other IL warning.
- Any `Routes`/`App` that renders `<Router>` trips the trim analysis through the generated `OpenComponent<Router>`
  (`Router.NotFoundPage` is a DAM-annotated property): IL2111 from the build-time analyzer, IL2110 from ILLink once
  the component is kept. Ducky.Blazor renders no Router. The sample needs a type-level suppression on that component,
  justified as the framework's.
- `TrimMode=full` trims away whatever the trimmer cannot see being used: in a Web App client the server-referenced root
  component went ("Root component type 'Client.Routes' could not be found"), and routed pages are found only by
  reflection. The prototype makes its client assembly a `TrimmerRootAssembly`, which keeps it analysed. The trimmed
  sample of M14-06 does the same, or `WasmTrimmedSmoke` fails with a blank page.

Not measured here: Linux and Windows (the `aot` job publishes on Linux; M14-06 checks the IL2104 allowlist and the AOT
publish there again), and Firefox and WebKit (Chromium only).

## S-4: CsCheck `SampleParallel` within the CI budget (M0-14)

**Question.** Does a CsCheck `SampleParallel` linearizability property at 50 repetitions (`DUCKY_REPEAT`) stay under
3 minutes, and with which operation counts?

**Setup.** `spikes/S-4-dispatcher`, run 2026-10-01 on macOS arm64 (Apple M1 Max, 10 logical CPUs) with SDK 10.0.401,
CsCheck 4.9.1 and xunit.v3.mtp-v2 4.0.1 (the pins of `Directory.Packages.props`). The stub is a lock-protected queue
with one drainer: the dispatching thread that finds no drainer drains outside the lock until the queue is empty, and
`DispatchAsync` completes after its action is reduced and committed (`RunContinuationsAsynchronously`). The property
has the §17.3 row 13 shape: `[Theory]` over `DUCKY_REPEAT` (default 50), a 10 s `WaitAsync` per repetition, two
operations (awaited `DispatchAsync` of a random int, `ReadState`) against a sequential reducer model (append to a list).
The final state and every read must match one linearization: each `ReadState` carries a fresh key, so the stub's read
is compared with the model's read of the same operation. Times are the wall time of `dotnet test --no-build` (about
1 s of it is host start), with `CsCheck_Threads` set to 10 (the CsCheck default here) and to 4 (an `ubuntu-latest`
runner's vCPUs).

**Answer: yes, with large headroom. Tuned counts: `maxSequentialOperations: 10` (the CsCheck default),
`maxParallelOperations: 6`, `iter` left to CsCheck (100, or `CsCheck_Iter`).** 50 repetitions take 7.7 s at 10 threads
and 5.6 s at 4 (the bold row); one repetition on one thread (the Stryker setting, §17.8) takes 1.1 s. A naive stub
(each caller reduces in place, no lock, no drainer) fails all 50 repetitions, each with a shrunk two-dispatch
counterexample. Both buggy stubs stay in the spike behind `S4_STUB=stranded|naive` (README), so M1-14 can re-measure
the detection column.

| Counts (seq / par / iter) | 50 reps, 10 threads | 50 reps, 4 threads | Stranded-action stub: failing reps of 50 (10 / 4 threads) |
|---|---|---|---|
| 10 / 5 / 100 | 4.6 s | 4.1 s | 7 / 2 |
| 10 / **6** / 100 | 7.7 s | 5.6 s | 10 / 8 |
| 10 / 7 / 100 | 24.6 s | 7.8 s | 21 / 7 |
| 10 / 8 / 100 | 81.2 s | 13.8 s | 13 / 5 |
| 20 / 5 / 100 | 4.5 s | 4.2 s | – |
| 10 / 5 / 1000 | 34.7 s | 30.1 s | – |
| 10 / 6 / 1000 | 66.0 s | 39.6 s | – |

- **Detection.** The stranded-action stub has the INV-03 bug: the drainer sees the queue empty under the lock but clears
  its flag after releasing it, so a dispatch enqueued in between returns, finds a drainer, and is never reduced (its
  `DispatchAsync` hangs and the 10 s bound fails the repetition; the times of those runs are not budget data). Every
  count catches it in some repetitions of a 50-repetition run; at 6 parallel operations 8 to 10 of 50 failed at either
  thread count in the table's runs and 6 / 5 (10 / 4 threads) in a re-run through `S4_STUB=stranded`. A per-repetition
  detection of 10 to 20% puts a 50-repetition miss between 0.9^50 (about 0.5%) and 0.8^50. That is a point estimate for
  this stub's race window (a few instructions between the unlock and the flag write), not a guarantee for the real
  dispatcher: detection depends on the window's width (a `SpinWait` before the flag write failed 20 of 20 in review).
  5 parallel operations caught it in only 2 of 50 at 4 threads.
- **Cost.** The linearization check tries every order of the parallel operations, so time grows factorially with
  `maxParallelOperations` (6! = 720, 8! = 40 320), and from 6 operations on more CsCheck threads are slower: each
  thread runs its parallel operations on threads of its own, oversubscribing the cores (81 s at 10 threads against 14 s at 4 for 8
  operations). 7 doubles detection at 10 threads but not at 4, for three times the cost. `iter` scales linearly
  (×10 iterations is about ×7 time) and the sequential prefix costs nothing measurable.
- **Headroom for the real dispatcher.** 7.7 s is about 1/23 of the 3-minute budget and 0.15 s of each repetition's 10 s
  bound. The real store commits snapshots, runs middleware hooks and slices per action, so an operation will cost more
  than the stub's; M1-14 re-runs these counts on `Linearizability_DispatchVsModel` and lowers `maxParallelOperations`
  to 5 only if 50 repetitions on `ubuntu-latest` exceed 3 minutes.
- **`iter` stays CsCheck's.** The `PropertyLong` nightly lengthens runs through `CsCheck_Iter` (§19), the value CsCheck
  uses for an `iter` the call leaves unset (`CsCheck_Iter=1000` took 5 repetitions from 1.6 s to 7.9 s); a tuned
  `iter` argument would pin the count instead.

Not measured here: Linux and Windows, and the real dispatcher (M1-14, stage 2).

### S-4 re-run against the real dispatcher (M1-14, stage 2)

**Setup.** `Linearizability_DispatchVsModel` in `test/Ducky.Concurrency.Tests/Linearizability`, run 2026-10-01 on the
same machine, SDK and pins, Release, against `DuckyStore` with one `LogSlice` (the real `Dispatcher`: lock-protected
queue, single drainer, snapshot commit per action). Same two operations and model as the spike (awaited `DispatchAsync`,
`ReadState` of the log), plus the sync-`Dispatch` check of §17.3 row 13 in the same repetition: CsCheck `Sample`
(`threads: 1`, `iter` CsCheck's) over 2 to 6 producers firing 1 to 50 `Dispatch` calls each on dedicated threads, then
exactly-once and per-producer order once the last drain exits. Times are the test-run `duration` of the whole
`LinearizabilityTests` class at 50 repetitions; sibling worktrees were building on the machine, so they are noisy (the
par-6, 4-thread row measured 8.4 s and 16.7 s in two runs).

**Answer: the spike's counts hold. `maxSequentialOperations: 10`, `maxParallelOperations: 6`, `iter` left to CsCheck.**

| Counts (seq / par) | 50 reps, 10 threads | 50 reps, 4 threads | Slowest repetition (10 / 4 threads) |
|---|---|---|---|
| 10 / 5 | 7.2 s | 7.1 s | – |
| 10 / **6** | 14.3 s, 16.2 s | 8.4 s, 16.7 s | 0.63 s / 0.82 s |
| 10 / 7 | 54.3 s, one repetition over the 10 s bound | 12.0 s | – |

- **Budget.** The whole `Ducky.Concurrency.Tests` suite (550 cases, every row-1 to row-13 test at 50 repetitions)
  takes 14.6 s at 10 threads and 13.2 s at 4, about 1/12 of the 3-minute budget. One repetition on one thread (the
  Stryker setting) takes 0.9 s. 7 parallel operations are ruled out: at 10 threads one repetition of a correct
  dispatcher outran the 10 s bound (7! orders per check, oversubscribed cores).
- **Detection on mutants of the real code** (each applied to `src/` and reverted): a naive dispatcher (every caller
  processes in place, no lock, no drainer) fails 10 of 10 repetitions with shrunk counterexamples; a LIFO queue fails
  5 of 5 (through the sync-`Dispatch` order check; LIFO between concurrent awaited dispatches is still linearizable); a
  sync `Dispatch` that enqueues some actions twice fails 3 of 3 (exactly-once check). The stranded-action mutant
  (`_draining = false` moved out of the empty-check lock, the stub's INV-03 bug) fails only 1 of 50 at 4 threads and 1
  of 50 and 0 of 50 at 10, against the stub's 6 to 10: the real drain's release window is as narrow, and an awaited
  `DispatchAsync` is a slower producer than the stub's. INV-03's release path stays with
  `Dispatch_AfterDrainRelease_NoStrandedAction` and a future deterministic seam inside `Drain`; this property is the
  INV-01/02/04/07 check. Completing a `DispatchAsync` before its reduce fails 2 of 10 here and 20 of 20 in
  `DispatchAsync_QueuedBehindOtherThreadsDrain_CompletesAfterReduce`. Completing it after the reduce but before the
  commit (INV-07) only shows when another operation starts in that gap, so the model comparison alone missed it (0 of
  50): each awaited dispatch also reads its own write back, which fails that mutant 5 of 100 (the correct dispatcher
  passes 150 of 150); `Snapshot_AfterConcurrentReduces_IsNeverStale` stays its main detector. Seqs are drawn from the
  whole `int` range, so no two operations of one iteration are equal records.
- **The bound under `PropertyLong`.** `CsCheck_Iter=100000` (§19) multiplies a repetition's work by 1 000, so a flat
  10 s bound over the test would fail every nightly run (`CsCheck_Iter=10000` timed out at 10 s). The §17.3 bound
  therefore sits on each blocking step instead: every awaited `DispatchAsync`, the producers and the drain exit of the
  sync check wait through `Interleaving.Wait` (10 s, thread dump on expiry), so a deadlock fails within 10 s under any
  `CsCheck_Iter`. `Interleaving.WithinProperty` bounds both CsCheck runs by progress, not by their length: it fails
  once 10 s pass with no step entering or leaving `Interleaving.Wait`, so a hang inside a synchronous store call fails
  within 10 s too, while a live run may take as long as its CPU budget needs (a first repeat on a cold 2-vCPU runner
  took 10-15 s and failed the old 10 s whole-run bound).
- **Still to measure.** Linux and Windows (`ubuntu-latest`, 4 vCPUs): lower `maxParallelOperations` to 5 only if 50
  repetitions exceed 3 minutes there. `Selection.Value` joins the operations at stage 5.

## S-5: the `IJSStreamReference` read path on Server (M0-13)

**Question.** Does a Server app round-trip about 2 MB from `localStorage` through `IJSStreamReference` without closing
the circuit, and does a pull export returning an empty `Uint8Array` yield a zero-length reference (§11.9)?

**Setup.** `spikes/blazor`, `/s5`, same run as S-3, `SPIKE_MODES=server` (and again on the WASM side, with
`SPIKE_MODES=wasm`, Debug and AOT). .NET writes an envelope-shaped ASCII value of 100 KiB and 2 MiB with
`storageSet(area, key, value)` (one string argument, .NET to JS), `storageGetStream` returns
`new TextEncoder().encode(localStorage.getItem(key) ?? "")`, and .NET reads it with `OpenReadStreamAsync(3 MiB)` under
`await using`. Then the removed key, a one-byte array, `null`, and a 2 MiB value opened with `maxAllowedSize` 1 MiB.

**Answer: the round trip yes; the empty array no.**

| Case | Interactive Server | WebAssembly |
|---|---|---|
| 100 KiB and 2 MiB | `Length` = UTF-8 size, bytes equal, 17 ms and 47 ms | the same, 20 ms and 30 ms (AOT) |
| empty `Uint8Array` | `JSException`: "Length must be a positive value. (Parameter 'totalLength')" | the same `JSException` |
| `new Uint8Array(1)` | `Length` 1, 1 byte read | the same |
| `null` | `JSException`: "Cannot read properties of null (reading 'buffer')" | the same |
| 2 MiB, `maxAllowedSize` 1 MiB | the reference is created (`Length` 2097152); `OpenReadStreamAsync` throws `ArgumentOutOfRangeException` | the same |

The circuit stayed open throughout (no `closed` in the circuit log, a later click re-rendered, no reconnect UI). In an
earlier run the empty-array `JSException` escaped the click handler and the circuit died (`CircuitHost` event 111): an
unhandled pull failure is fatal to the circuit, so every pull call site catches.

**Fallback (triggered): the pull exports never return an empty array.** `storageGetStream` (M6-01, read by M6-09 and
M7-02b) and `devtoolsTakeMessage` (M8-01, read by M8-03b) return `new Uint8Array(1)` (one NUL byte) when nothing is
there, and .NET treats a reference with `Length <= 1` as not found: an envelope and a DevTools message are JSON objects,
never shorter than 2 bytes. That replaces "an empty `Uint8Array`" and "`Length == 0`" in INV-23 (§3), §11.5 step 4,
§11.7, §11.8, §11.9, ADR-0030 and the stories M6-01, M6-09, M7-02b, M8-01 and M8-03b, all amended to match. The harness
spec is renamed `DuckyJs_PullExports_ReturnOneByteWhenGone` (SPEC, `tests.yaml`, plan) and asserts the one-byte return;
`CrossTab_TooLargeKeyRemovedBeforePull_RealRuntime_ResetsSlice` asserts the one-byte reference; the fake references of
the bUnit `CrossTab_TooLargeKeyRemovedBeforePull_ResetsSlice` and of `DevTools_LargeImport_ReadViaStream` (its
taken-message case) have `Length` 1. The `null` rule of §11.9 is confirmed.

**The over-`MaxPayloadBytes` path is decided from `Length`.** The reference arrives with its size (2097152 above), so
§11.5 and §11.8 treat `reference.Length > MaxPayloadBytes` as not found with Warning 2023 before calling
`OpenReadStreamAsync`, inside the `await using` of the reference. `maxAllowedSize` stays as a backstop; its
`ArgumentOutOfRangeException`, if caught at all, is caught around the `OpenReadStreamAsync` call only, so an unrelated
one from the read or the envelope parsing is never turned into a silent "not found".

Not measured here: Linux and Windows, Firefox and WebKit (Chromium only), and values near the 5 MiB `localStorage`
quota.

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

## S-7: .NET 10 render-mode and service questions (M0-13)

**Question.** The six questions of §23 S-7, before M5-04 designs the `InteractivityGate`.

**Setup.** `spikes/blazor`, same run as S-3, the one Web App in its three configurations: `server` (Interactive Server
only), `wasm` (Interactive WebAssembly only) and `auto` (both render modes, `InteractiveAuto` on `Routes`, as the Auto
template). Development environment, so the server's scope validation is on; the AOT publish of S-3 repeated the
`wasm` answers in Production. Cookie sign-in with a `NameIdentifier` claim; the store subscribes to
`AuthenticationStateChanged` on its store-scope provider and calls a delegate shaped like `DuckyScopes.NameIdentifier`
(`(await provider.GetAuthenticationStateAsync()).User.FindFirst(ClaimTypes.NameIdentifier)?.Value`), recording whether
its task has completed before any await.

**Answers.**

1. **`PersistentComponentState` and `IJSRuntime` in WASM: yes, identical.** Both are singletons
   (`PersistentComponentState` from a factory, `IJSRuntime` the `DefaultWebAssemblyJSRuntime` instance), and the
   renderer scope, the store scope and the root return the same instances, in the `wasm` app and on the WebAssembly side
   of `auto`. The root, measured in a second run of `wasm` and `auto` (same day and versions), is the provider the
   singleton store is constructed from; resolving the scoped `AuthenticationStateProvider` from it throws under the
   Development scope validation, as a root should. On the server both are scoped and the DI scope is the store scope
   (renderer and store resolve the same instance; the root throws under scope validation). **`WebAssemblyHost.Services`,
   what Program.cs reaches as `host.Services`, is not the root:** it is the renderer's scope itself (the same provider
   object, so it resolves the scoped `AuthenticationStateProvider` to the renderer's instance). §6.10's "from the root
   (for example a Program.cs preload through `host.Services`)" was inaccurate and now names `host.Services` as the
   renderer scope; a singleton store is the same instance either way.
2. **`AuthenticationStateProvider` is scoped everywhere measured:** `DeserializedAuthenticationStateProvider`
   (`AddAuthenticationStateDeserialization`, the Web App template's WASM side), `RemoteAuthenticationService<…>`
   (`AddOidcAuthentication` and `AddMsalAuthentication`) and `ServerAuthenticationStateProvider`. In the browser the
   store scope gets a different instance from the renderer's, as §6.10 and §11.6 assume; the hand-off stays mandatory.
3. **The synchronous `InvalidOperationException` from the prerender import: only without server interactivity.** In
   the `wasm` app the prerender `IJSRuntime` is `UnsupportedJavaScriptRuntime` and the import throws synchronously
   ("…cannot be issued during server-side static rendering…"). In the `server` and `auto` apps it is `RemoteJSRuntime`,
   and the call returns a task that is **already faulted** with `InvalidOperationException` ("…the component is being
   statically rendered…"); nothing is thrown. Interactive runtimes (circuit and WASM) return a pending task that
   completes. So this answer is **no** for every app that configures Interactive Server, the Auto template included.
4. **A paused and resumed circuit delivers `ducky:seed` to a scoped service: yes.** After three increments,
   `Blazor.pauseCircuit()` ran the store's `InteractiveAuto` callback (`src: "pause"`), the circuit closed, and after
   `Blazor.resumeCircuit()` the new circuit's new store took `{"src":"pause","counter":3}` in its first component's
   `OnInitialized`. One client-side caveat for the E2E twins: after a reconnect forced with
   `Blazor._internal.forceCloseConnection()`, `pauseCircuit()` returns `true` without sending `PauseCircuit`, and
   `resumeCircuit()` is then refused ("The circuit host '…' has already been initialized"), in every run. Pause before
   forcing a reconnect, or on a fresh page.
5. **`RegisterOnPersisting(…, RenderMode.InteractiveAuto)` persists the seed in single-mode and both-modes apps: yes.**
   The seed went into the server state of the `server` app, the WebAssembly state of the `wasm` app, and both of them in
   the `auto` app, and was taken on the Server side (first visit) and on the WebAssembly side (once its resources were
   cached). A null-mode registration from the same non-component service is accepted in both single-mode apps (both keys
   persisted), but in the `auto` app persisting throws `InvalidOperationException` ("The registered callback
   PersistNullMode must be associated with a component or define an explicit render mode type during registration",
   from `ComponentStatePersistenceManager.InferRenderModes`) and the whole response is a 500: §11.4's explicit mode is
   required, and the failure is worse than a lost seed.
6. **Reconnect raises `AuthenticationStateChanged` on the live store: yes, and the scope completes synchronously.** After
   `forceCloseConnection()` the circuit reconnected (`connection down`, `connection up`, same circuit, same store and
   provider instance), the event fired once with its task completed, and the `NameIdentifier` delegate had completed
   with the same user (`alice`) before any await, which is the case §11.6's re-assertion check absorbs. **Resume raises
   nothing on a live store:** a resumed circuit is a new DI scope, so its store is new and is created after the user is
   set; it saw no event, and its epoch-0 resolution completed synchronously with the user.

**Fallbacks triggered.**

- **The probe (answer 3).** M5-04's gate probe treats both shapes as `NonInteractive`: a synchronous
  `InvalidOperationException` from the import, or a returned task that is already faulted with an
  `InvalidOperationException` (checked without awaiting). Any other outcome is `Interactive`, and only a task that is
  not faulted is kept as the module handle. That amends §11.4's probe bullet, ADR-0029, M5-04 and
  `Gate_Unknown_ProbeDetectsPrerender`, whose fake runtime gets both shapes in bUnit and whose E2E twin runs against the
  Server and Auto samples.
- **The `DuckyComponent` hand-off for the other services (§23 rule): enabled.** Answer 3 is a no, and §23 says any no
  enables the fallback of §6.10. M5-04's `InteractivityGate` hand-off therefore carries the renderer-scope
  `PersistentComponentState` and `IJSRuntime` along with the renderer `IServiceProvider` (from which the
  `AuthenticationStateProvider` is taken, §11.6), and `PrerenderHandoff` and `JsBridge` use the handed-over instances
  once a component has registered; before that (a Program.cs preload) they use the store scope's. §6.10 and M5-04 now
  say so. Answer 1 measured the same instances in every scope on 10.0.12, so on that version the fallback changes no
  behaviour and no real-runtime test can tell; `Gate_Wasm_HandsOverPersistentStateAndJsRuntime` (bUnit, a fake renderer
  scope whose instances differ from the store scope's) checks the routing, so it holds when an upgrade makes either
  service scoped in WASM. Dropping the fallback on the strength of answer 1 would amend the rule of §23 and §6.10: an
  owner decision, which this record does not make.
- Answers 2, 4 and 5 confirm §6.10 and §11.4 as written. Answer 6 confirms §11.6 for reconnect (`ConnectCircuit`) only:
  §11.6's "spike S-7 confirms it for .NET 10 reconnect and resume" was wrong for `ResumeCircuit`, which creates a new
  store and raises no event on a live one. That is a wording correction with no design change, made in §11.6.
  `Circuit_ReconnectWithScopedSlices_NoResetFlicker` relies on answer 6; on resume no re-assertion is needed.

Not measured here: Linux and Windows, Firefox and WebKit (Chromium only), and running OIDC or MSAL apps: answer 2's
`AddOidcAuthentication` and `AddMsalAuthentication` lifetimes come from their service registrations, not from apps that
sign in through them.

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

## S-9: Stryker run time per mutated project (M0-14)

**Question.** How long does a full Stryker run take per mutated project, which `timeout-minutes` does that give
`nightly-mutation` and `release-mutation`, and how does the nightly split per project if one job no longer fits?

**Setup.** Run 2026-10-01 on macOS arm64 (Apple M1 Max, 10 logical CPUs) with SDK 10.0.401 and `dotnet-stryker` 4.16.0,
in the environment the targets give Stryker (`DUCKY_REPEAT=1`, `DUCKY_PROPERTY_SEEDS=1`, `CsCheck_Threads=1`). The
three S-6 prototypes (`spikes/S-6-mutation`, commands in its README, test projects built first), at Stryker's default
concurrency (half the logical CPUs, 5 here) and at `--concurrency` 1 and 2 (an `ubuntu-latest` runner has 4 vCPUs, so Stryker's default there
is 2); then the seven stage-1 projects through `./build.sh Mutation --strict-stages`, and one project alone through
`--project`. The S-6 prototypes needed one fix to build again: since M0-07 the root pins
`Microsoft.Extensions.Logging.Abstractions`, so the spike's own `PackageVersion` was a duplicate (NU1506); their lock
files gain the root's `MinVer` reference.

**Answer.** A per-project full run is 7 to 23 seconds, nearly all of it fixed cost; the first `timeout-minutes` stays
**330 for `nightly-mutation` and for `release-mutation`** (§20.1), and the split is per-project
`Mutation --project <name>` jobs inside the same workflow.

| Project (prototype) | Mutants created / tested | Full run, default concurrency | `--concurrency 1` | `--concurrency 2` |
|---|---|---|---|---|
| `Plain` (plain SDK) | 20 / 13 | 7.3 s | 7.1 s | 7.3 s |
| `Gen` (netstandard2.0 generator) | 51 / 35 | 7.6 s | 7.6 s | 7.8 s |
| `Web` (Razor SDK, two test projects) | 18 / 11 | 10.9 s | 12.2 s | 10.6 s |

| Stage-1 project (`Mutation --strict-stages`) | Stryker time | Mutants |
|---|---|---|
| `Ducky` (first: builds six test projects) | 22.5 s | 0 |
| `Ducky.Generators` | 6.4 s | 0 |
| `Ducky.Testing` | 7.2 s | 0 |
| `Ducky.Blazor` | 11.1 s | 0 |
| `Ducky.Reactive` | 8.1 s | 1 |
| `Ducky.Draft` | 7.7 s | 0 |
| `Ducky.Draft.Generators` | 6.9 s | 0 |

- **Fixed cost dominates.** Per project, analysis takes 2 to 3 s, building the test projects 1 to 2 s each, and the
  initial test run 2 s; testing the mutants took about 2 s whatever the concurrency (35 mutants of `Gen` included). The
  whole seven-project run took 89 s with `Restore` and `Compile` (the six zero-mutant projects fail the staged check, as
  S-6 recorded); `Mutation --strict-stages --project Ducky.Reactive` alone took 21 s (Restore 1 s, Compile 8 s,
  Mutation 8 s).
- **What grows.** With `coverage-analysis: "off"` (S-6) every tested mutant runs every test of its project's
  `test-projects` list in reused test hosts, so a project's time is about its fixed cost plus
  tested mutants × the list's test time ÷ concurrency. The prototypes' tests take milliseconds; the real lists (`Ducky`'s
  has six test projects, `Ducky.Concurrency.Tests` among them, §17.8) will take seconds, which is
  the term the stage-13 and stage-17 measurements must capture.
- **The timeout.** The prototype puts a full nightly at 89 s here, 2 minutes with 30% headroom: it fits 330 minutes
  by two orders of magnitude even on a much slower runner, so it gives no reason to change §20.1's 330. A timeout
  sized to the prototype would cancel the nightly as soon as stage 2 gives `Ducky` real logic, long before the
  stage-13 re-measurement. 330 keeps 30% headroom (§23) while a measured full run stays at or below 254 minutes
  (330 / 1.3); stages 13 and 17 re-measure on `ubuntu-latest` and record the new value, or the split, here.
  `nightly-mutation.yml` already has 330 (`build/Build.CI.cs`); `release.yml` (M16-03) writes 330 for
  `release-mutation`.
- **The split, designed now.** `Mutation --project <name>` (repeatable, M0-05, §17.8) runs and checks only the named
  projects, with every other rule unchanged. If a measured run passes 254 minutes, `nightly-mutation.yml` gets seven
  per-project jobs (§17.8), each running `Mutation --project <name>` under its own `timeout-minutes`, emitted as seven
  named jobs or as one matrix job by the `GetJobs` override of `build/DuckyGitHubActionsAttribute.cs`, whichever
  Fallout's `GitHubActionsJob` supports (unchecked here; a check for the stage-13/17 story). No leg may cancel another
  (`strategy.fail-fast: false` on a matrix), so every project still opens its own below-90 issue that night, and the
  `Report: nightly failure` step (`build/Build.CI.cs`) moves out of the legs into one final job that `needs:` them all,
  with `if: failure() || cancelled()`: per leg, concurrent search-then-create on one title would race into duplicate
  issues. It stays one workflow, so `MutationForSha`'s `gh run list --workflow nightly-mutation.yml --commit` still
  finds one run for the SHA, successful only when every leg passed. `release-mutation` can split the same way (same
  `fail-fast` and report rules) only once `MutationForSha` takes what §19 gives `Mutation` alone: the shared Fallout
  `Project` parameter (`build/Build.Mutation.cs`) passed through to its fallback `Mutation`, and its nightly-run query
  made per leg (the nightly job of its own project); that §19 change belongs to the split's story.
  A single project above 254 minutes cannot be split this way; its `test-projects` list (the per-mutant cost) is then
  the lever.

Not measured here: `ubuntu-latest` (no CI run was made for this story; the stage-13 and stage-17 measurements are
made there), and projects with real logic.

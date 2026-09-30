# ADR-0024: DispatchResult and a never-faulting async API

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.2, §6.4, §7, §8; D3, D8; INV-02, INV-10, INV-11; review rounds 4, 5, 6 and 7

## Context

Returning a plain `Task` hides veto, drop, dispose and failure; dx-first returned a cancelled task after dispose, which throws when awaited.

## Decision

`DispatchAsync` and `SetAsync` return `Task<DispatchResult>` (`Reduced`, `Vetoed`, `Dropped`, `Failed`, `Disposed`) and never fault or cancel. At run time no member throws for a reducer, middleware or effect failure. The only exceptions are programmer errors, thrown synchronously before any task exists (review rounds 3 and 4): `ArgumentNullException`, the cached `DuckyConfigurationException` (DUCKY353), `ArgumentOutOfRangeException` from `Restore` with a non-restore origin, `InvalidOperationException` from re-entrant materialization, DUCKY351 (`DuckyConfigurationException`) / DUCKY352 (`InvalidOperationException`) misuse, and `InvalidOperationException` from `WhenIdleAsync` (or `TestStore.Settled`) called inside a non-`LongRunning` effect run, which counts itself as running and would wait for itself forever, holding idle off for the rest of the store's life (review round 6). The never-fault guarantee covers an explicit set of async members (INV-10), with three named carve-outs: caller-token cancellation of `InitializeAsync`/`WhenIdleAsync`, `EffectContextExtensions.Run` inside an effect run, and the test-failure reporting of `TestStore.Settled`/`TestStore.DisposeAsync`/`EffectTest.Run` (review round 5 moved `TestStore.DisposeAsync` here from the never-fault set: under `TestStore`'s `FakeTimeProvider` the store's own dispose bounds never elapse, so it needs a wall-clock bound and must be able to fail). A superseded effect run can't dispatch (`Dropped`): `EffectContext` checks its run token at call time and the dispatcher re-checks it at process time; every such drop, at call time or at process time, is logged and counted (`ducky.dispatch.dropped` with a `ducky.drop.reason` tag, review round 6). Any dispatch after disposal began, from an effect run included, completes `Disposed`, never `Dropped`: `EffectContext` and `SliceStore.Set`/`SetAsync` read the store's disposal state before the run token, so the result doesn't depend on whether dispose step 2 has cancelled the token yet, and shutdown traffic of Merge, Exhaust and Queue runs never counts as dropped runs (review round 7).

## Consequences

Every path is assertable, so mutants on those paths die; stale Switch results are provably dropped.

## Alternatives considered

Exceptions for veto/drop (rejected); plain `Task` (rejected: unobservable).

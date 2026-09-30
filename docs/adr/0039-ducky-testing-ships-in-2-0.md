# ADR-0039: Ducky.Testing ships in 2.0

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §15, §6.7; TST-01; review rounds 5 and 7

## Context

Minimal-core deferred testing helpers; the library's own tests and docs need a TestStore with `Settled()` and strict unhandled actions.

## Decision

Ship Ducky.Testing (TestStore, SliceTest, EffectTest, `DuckyAssertionException`) on Ducky's public API only, with no test-framework dependency. Review round 5: `TestStore.DisposeAsync` is bounded by a wall-clock `WallClockTimeout` (as `Settled` is) and reports a hung disposal as `DuckyAssertionException`, because the store's own dispose bounds run on the `FakeTimeProvider`; the recorded lists (`Processed`, `Failures`, `EffectRun.*`) are immutable snapshots swapped atomically, so tests may read them while the drainer appends. Review round 7: `TestStore.Seed<TState>` resolves the slice key through the public `IStore.Slices` by declared state type (DUCKY350 when none matches), and the synchronous `SliceTest.Then` relies on the normative inline start of §6.7 (every init already complete ⇒ the first `Dispatch` is reduced before it returns).

## Consequences

Users and the library share one harness; Microsoft.Reactive.Testing leaves the runtime graph.

## Alternatives considered

Defer to 2.1 (rejected: users need it at GA).

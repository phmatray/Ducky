# ADR-0032: Built-ins on public Middleware, no StoreExtension

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5, §11, §14, §15; D19; R-PKG-2; review rounds 1, 5, 6 and 7

## Context

Correctness-first used a public `StoreExtension` with internal members so only Ducky packages could plug into fixed stages; that needs a privileged seam across packages.

## Decision

Prerender handoff, persistence (which hosts cross-tab sync as an internal component, because cross-tab restores must be issued under persistence's private `_issue` lock, review round 6), DevTools and the reactive bridge are internal sealed `Middleware` subclasses registered with `Use<T>()`. The reactive host is an `Effect<StoreInitialized>`. `Middleware.DispatchSystem` is the one privileged dispatch (`Origin.System`, bypasses the init buffer and every veto; `isFailure` marks failure actions). What the built-ins need from the core is public, read-only surface rather than a privileged seam: `IStore.Json` (`DuckyJson`, the store's resolver), `IStore.InitialState`, `IStore.Slices` (the store-owned slices, each with its public `Key` and declared `StateType`: DevTools serializes, sends and imports each slice through its declared type, and `TestStore.Seed<TState>` resolves the key by declared state type, review round 7), `Selection<T>.Create`, and `DuckyBuilder.AddValidation`. There is no configure-time error API (review round 5 removed `AddError`): options compose until `configure` returns, so Ducky.Blazor's configuration checks are `AddValidation` rules evaluated on the final composed options at first resolution.

## Consequences

No `InternalsVisibleTo` between shipped packages; the core is exercised through its public API. Relative order of AfterReduce observers doesn't matter; effects always start after every AfterReduce.

## Alternatives considered

`StoreExtension` stage slots (rejected: privileged seam); ordering validator (MW-03, rejected).

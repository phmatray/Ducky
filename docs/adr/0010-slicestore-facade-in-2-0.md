# ADR-0010: SliceStore facade in 2.0

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §12; API-01; matrix conflict 7; open question 11

## Context

A Zustand-style method store is the Bustand differentiator, but two ways to change state double the documented surface, and closures can't be replayed in DevTools.

## Decision

Ship `SliceStore<TState> : Slice<TState>` in 2.0. `Set` dispatches an internal `SetState<TState>` through the full pipeline; the mutator runs at reduce time on the drainer. `AddSlice<T>()` makes it injectable (no `AddSliceStore`). DevTools labels `{key}/{name}`; replay is unsupported, jump works.

## Consequences

Bustand's lost updates are fixed (INV-30). It is still a slice, so it can react to actions with `On<T>`.

## Alternatives considered

Defer to 2.1 (rejected: owner promoted it); a separate state container (rejected: two pipelines).

# ADR-0027: Quiescent prerender seed

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §11.4; SSR-01; D7; INV-15

## Context

A seed written while a load effect is still running would mark the slice `WasRestored` with 'loading' state, and the interactive side would never load.

## Decision

`OnPersisting` awaits `WhenIdleAsync` bounded by `PrerenderIdleTimeout`. `WhenIdleAsync` ignores effects marked `LongRunning` and all Ducky.Reactive work (reactive pipelines are never counted as running). This is normative: a `Prerender<T>` slice must be loaded by an `Effect<T>` (review round 3). On timeout, no seed is written and a Warning is logged once per store. Quiescence doesn't mean the load succeeded: a slice registered with `Prerender<TSlice, TState>(include)` is seeded only while `include(state)` is true, and slices whose load can fail during prerender use it to keep error and incomplete states out of the seed, so the interactive side retries (recovered review round 2).

## Consequences

Degraded (a second load) but never wrong; pollers don't trap the seed.

## Alternatives considered

Seed in `OnPersisting` without waiting (rejected: stuck UI); count only runs started by the prerender's actions (rejected: harder to explain than `LongRunning`); track in-flight reactive work through a public `IStore` hook (rejected in round 3: new public surface for a case an `Effect<T>` already covers); skip a slice automatically when an `EffectFailed` was reduced during prerender (rejected in round 2: an effect failure can't be attributed to a slice).

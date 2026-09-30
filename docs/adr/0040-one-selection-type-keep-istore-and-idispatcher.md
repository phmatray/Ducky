# ADR-0040: One Selection type; keep IStore and IDispatcher

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.2, §11.1; D21; CMP-05; review rounds 4 and 7

## Context

1.x consumers inject `IStore`/`IDispatcher`; proposals had `SelectResult<T>`, `ISelection<T>` and `Selection<T>` for one concept, and a heavy `IStore` surface.

## Decision

Keep `IStore : IDispatcher` (same names as 1.x) with a trimmed surface: `State`, `InitialState`, `Slices`, `Json`, `Select`, `Restore`, `InitializeAsync`, `WhenIdleAsync`. `Slices` (read-only, registration order) was added in review round 7, so DevTools and `TestStore.Seed` get each slice's declared `StateType` through public API. `InitialState` and `Json` were added in review round 1 so Ducky.Blazor can work without `InternalsVisibleTo` (ADR-0032). `EffectContext` implements `IDispatcher` so generated helpers work in effects. One `Selection<T>` (evaluate-on-read, implicit conversion, `ToString`) everywhere.

## Consequences

Lower migration cost; one concept per type.

## Alternatives considered

Concrete `DuckyStore` only (rejected: migration cost); separate component/non-component selection types (rejected).

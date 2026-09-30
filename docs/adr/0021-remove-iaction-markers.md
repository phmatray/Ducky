# ADR-0021: Remove IAction markers

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.2, §22.1; CORE-05; open question 4; review round 5

## Context

1.x required `IAction` while its own library actions did not implement it (hidden behind a global CS0618 suppression).

## Decision

Any non-null object can be dispatched; records are recommended. `IAction`, `IState`, `IKeyedAction` are removed; the published `IState` and `IKeyedAction` are tombstoned as DUCKYM001, while `IAction` existed only on the unreleased `dev` branch and gets no tombstone (review round 5: tombstones follow the 1.0.292 public API). The generator targets `[DuckyAction]`.

## Consequences

Less ceremony; library and user actions follow one rule.

## Alternatives considered

Keep an optional marker (rejected: no benefit with exact-type matching).

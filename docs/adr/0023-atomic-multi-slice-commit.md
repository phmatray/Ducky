# ADR-0023: Atomic multi-slice commit

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.4 step 6; CORE-04; INV-08

## Context

The matrix said 'previous state is kept' when a reducer throws, which with several slices can leave an action partly applied.

## Decision

Reducers write into a scratch array; if any throws, nothing commits for that action, which completes `Failed` with one `ReducerFailed`.

## Consequences

No torn state; one render per action.

## Alternatives considered

Per-slice commit (rejected: torn state).

# ADR-0004: Synchronous middleware hooks, not an onion

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §5.6; MW-01; matrix conflict 1; open question 5

## Context

Ducky 1.x, Bustand and phased DuckyNext use before/after/veto hooks; NotRedux, BlazorMVU and DuckyNext-Poc use an onion `next()`. An onion makes the pipeline async around the reducer, which breaks the inline single-drainer.

## Decision

Middleware has `InitializeAsync`, `MayDispatch` (the only veto, consulted only for `Local` and `Effect` origins so library traffic can never be vetoed), `BeforeReduce`, `AfterReduce`, `DisposeAsync`, and receives an immutable `ActionContext`. No `Abort()`, no `Metadata`. Retry belongs inside effects (Microsoft.Extensions.Resilience); error capture goes through failure actions.

## Consequences

Reducers stay synchronous and inline. One veto path means one test matrix. Transform/retry middleware is not expressible, by design.

## Alternatives considered

Onion `next()` (rejected: async reduce, re-entrancy); dx-first's `Abort()` + `MayDispatch` (rejected: two veto paths).

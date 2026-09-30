# ADR-0026: Causal depth measures synchronous causality

- Status: Accepted
- Date: 2026-09-30
- Related: SPEC §6.4, §6.5; CORE-02; D12; INV-06, INV-12; review rounds 1 and 7

## Context

All three proposals let the depth `AsyncLocal` flow across awaits, so a legitimate polling effect (Tick → delay → Tick) dies after 64 ticks, while dx-first never restored the scope.

## Decision

A dispatch is a child (depth + 1) only while its parent is being processed on the same flow (scope sequence equals the drainer's current sequence). After a real asynchronous yield it starts a new chain at depth 0 and keeps the correlation id. The scope is always restored in `finally`, and the drainer's current sequence is reset to 0 there, so it is never stale between actions or while idle (sequences start at 1). The rule applies to every origin (`DispatchSystem` and `Restore` included); only the core's own failure actions are enqueued at depth 0. Review round 7: the causal scope and the effect-run scope are `AsyncLocal` **instance fields of each store**, never statics, so a scope never crosses stores: code running on store A's flow that dispatches into store B (the circuit-accessor pattern) is an unrelated producer for B, at depth 0 on a new chain, and never carries A's run token, idle accounting or sequence into B (`TwoStores_EffectRunOfA_DoesNotScopeDispatchSetOrWhenIdleOfB`); the `NoStaticMutableFields` allow-list stays exact. The loop-guard `ReducerFailed` is enqueued at depth 0, and a depth drop under a failure action is logged only, so a reactor that re-dispatches on the failure can't restart the loop forever.

## Consequences

Synchronous loops stop at 64; pollers run forever (`PollingEffect_RunsBeyondMaxDepthTicks`); traces keep their correlation. Asynchronous recurrence loops are not depth-guarded (documented).

## Alternatives considered

Depth across awaits (rejected: kills pollers); no depth guard (rejected: hung drainer).

# ADR-0052: A union effect shares one concurrency slot

- Status: Proposed
- Date: 2026-11-18
- Related: ADR-0008, ADR-0051; #345

## Context

A concurrency slot lives in one `EffectRunner`, and one runner served one exact action type (ADR-0008).
`EffectGroup<TAction>` registers one `On<Case>` per case, so each case is its own effect with its own slots (ADR-0051).
Two triggers of the same work could therefore never supersede each other. The only way to get one `Switch` slot was a
forwarder: a group whose handlers dispatch one internal action, and one effect on that action (#345). Nothing checks that
a forwarder lists every trigger, each load appears twice in the action log, and cancelling needs a flag of its own.
`Effect<TUnion>` compiled but never ran: dispatch unwraps a union to its case, and the effect index held only the union.

## Decision

`Effect<TAction>` accepts a union. It gains `protected virtual void RegisterCases()`, called once by its constructor, and
`protected void OnCase<TCase>(Func<TCase, TAction> wrap)`, which validates a case as `EffectGroup.On<T>` does (DUCKY319,
DUCKY307, DUCKY308). The Ducky generator overrides the hook in the effect's `partial` declaration with one
`OnCase<Case>(a => new TAction(a))` per case, the cases read as ADR-0050 says.

`CreateRunners` still returns one `EffectRunner`. With registered cases, that runner is indexed under every case and wraps
each action in the union before it calls `ConcurrencyKey` and `Handle`. One runner is one slot table, so `Policy` and
`ConcurrencyKey` apply across the cases and the slot code does not change. Matching stays by exact type (ADR-0008): the
effect index only learns that a runner can serve several exact types. With no registered case, the runner is built from
`typeof(TAction)` as before.

Only a class deriving directly from `Effect<TUnion>`, with `TUnion` closed, is generated. DUCKY027, DUCKY029, DUCKY030,
DUCKY031 and DUCKY032 guard its shape as they do for `EffectGroup<TAction>`. A non-union `Effect<TAction>` is an ordinary
effect that the union rules do not apply to.

`EffectGroup<TAction>` keeps one slot per case. Its behaviour does not change.

## Consequences

The forwarder, its internal action and its `Abandon` flag go away: a case that starts nothing, under `Switch`, cancels the
run in flight. A case missing from the `switch` in `Handle` is CS8509. A case that belongs to two trigger unions starts both
effects, each in its own slot table. An `Effect<TUnion>` that is not `partial` is now an error; that shape never ran, and
2.0 is pre-release. Without the generator (`DuckyUseGenerators` off), the author overrides `RegisterCases` by hand. One
effect has one policy and one key function: a policy per case is not available inside a union effect.

## Alternatives considered

`EffectGroup<TAction>` with a shared-slot switch: rejected, the group would have two behaviours behind one flag, and it
contradicts ADR-0051's "each case stays its own effect". One runner per case around a shared slot table: rejected, it keeps
the runner's action type singular but adds a shared mutable object for no behaviour that one runner lacks.

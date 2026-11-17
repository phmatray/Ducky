# ADR-0051: Union-first slices reduce through generated case registration

- Status: Proposed
- Date: 2026-11-17
- Related: ADR-0008, ADR-0050; #311, #313, #314; #304 (union dispatch)

## Context

A slice whose actions form a C# 15 union registers one `On<T>` per case today, and nothing checks that every case has a
handler (#311). The union already names its cases, and a `switch` over it is checked for exhaustiveness by the compiler.
The runtime matches handlers by exact type (ADR-0008), and Dispatch unwraps a union to its case (#304), so whatever the
shape, a handler has to be registered per case, never per union (`On<TUnion>()` is DUCKY319).

## Decision

`Slice<TState, TAction> : Slice<TState>` declares `protected abstract TState Reduce(TState state, TAction action)` and a
`protected virtual void RegisterCases()` hook that its constructor calls. The Ducky generator emits, into the slice's
`partial` declaration, an override of the hook with one `On<Case>((s, a) => Reduce(s, new TAction(a)))` per case, the
cases read as ADR-0050 says (a case that is itself a union is unwrapped to its own cases, the way Dispatch unwraps). So a
case dispatched alone or wrapped in the union reaches the same `Reduce` arm, and the runtime is unchanged.

Only a slice deriving directly from `Slice<TState, TAction>`, with `TAction` closed, is generated (an open one, a reusable
generic base, is skipped). Two errors guard the shape: DUCKY026 when
`TAction` is not a union, DUCKY027 when the slice or a containing type is not `partial`.

Effects follow the same shape (#314): `EffectGroup<TAction> : EffectGroup` declares
`public abstract Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken)` and the same
`RegisterCases` hook, which the generator overrides with one `On<Case>((a, c, t) => Handle(new TAction(a), c, t))` per
case, through the same case reader and the same two errors. Each case stays its own effect (`Concurrency.Merge`, one
global slot): a policy or key per case is `EffectGroup.On<T>`'s job.

## Consequences

Adding a case to the union adds its registration and leaves `Reduce`'s `switch` non-exhaustive (CS8509), which fails a
build with warnings as errors. Without the generator (`DuckyUseGenerators` off), the hook stays empty and the slice
handles nothing unless the author overrides `RegisterCases` by hand. A union with a user-declared single-parameter
constructor would register that parameter type as a case (ADR-0050's unmeasured shape).

## Alternatives considered

Reflection over the union at slice construction: rejected, it is not trimming-safe (INV-24) and the generator already
sees the cases. A single `On<TUnion>` handler: rejected, dispatch never delivers the union itself (#304).

# Unions in Ducky

Ducky 2.x runs on C# 15, and a `union` of record cases is the natural shape for a feature's actions. Four pieces work together, and each builds on the one before:

| You want | Use | Details |
|---|---|---|
| to dispatch a union | `store.Dispatch(new MyAction(new Case(...)))`, or just the case | the store unwraps a union to its case ([DUCKY319](../../diagnostics/DUCKY319.md)); see [Slices and keys](slices-and-keys.md#actions-and-state-as-c-15-unions) |
| one reducer for the whole union | `Slice<TState, TAction>` | below; [DUCKY026](../../diagnostics/DUCKY026.md), [DUCKY027](../../diagnostics/DUCKY027.md) |
| one effect handler and one helper per case | `EffectGroup<TAction>` and `[DuckyAction]` on the union | [Union effects and dispatch helpers](union-effects.md); [DUCKY028](../../diagnostics/DUCKY028.md) |
| one concurrency slot shared by every case | `Effect<TAction>` with the union as `TAction` | [One slot for every case](union-effects.md#one-slot-for-every-case); [DUCKY027](../../diagnostics/DUCKY027.md) |

## Dispatching a union

Dispatch unwraps a union to its case, so reducers, effects, middleware and `ActionContext` all see the case (`Deposit`), never the union (`AccountAction`). A case dispatched alone and the same case inside the union reach the same handler.

## One exhaustive reducer

Derive from `Slice<TState, TAction>` with the union as `TAction`, declare the slice `partial`, and write one `Reduce` with a `switch` over the union:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionSliceGuideTests.cs#union-slice)]

The generator overrides `RegisterCases` in the other half of the slice, with one `On<Case>` per case, each calling `Reduce` with the case wrapped in the union. A case missing from the `switch` is a CS8509 warning, an error with warnings as errors, so adding a case to the union points at every reducer that must handle it.

- The slice, and every type it is nested in, must be `partial` ([DUCKY027](../../diagnostics/DUCKY027.md)), and `TAction` must be a union ([DUCKY026](../../diagnostics/DUCKY026.md)).
- Without the generator (`DuckyUseGenerators` off), override `RegisterCases` and call `On<Case>((s, a) => Reduce(s, new TAction(a)))` for each case.

Test it on a `TestStore`: dispatch a case, then the union value, and read the state.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/UnionSliceGuideTests.cs#union-slice-test)]

## On&lt;T&gt; or a union slice?

- Pick **`Slice<TState>` with `On<T>`** when the slice reacts to a few actions of different shapes, or to actions that other features declare. Each reducer is small and stands alone.
- Pick **`Slice<TState, TAction>`** when the slice owns a closed set of actions and you want the compiler to prove every one is handled.

The two compose: a store can mix both kinds of slice, and an action reaches every slice that handles it.

## Effects and helpers

An `EffectGroup<TAction>` handles every case of the union in one `switch`, and `[DuckyAction]` on the union generates a dispatch helper per case. An `Effect<TAction>` over the union also shares one concurrency slot across the cases. [Union effects and dispatch helpers](union-effects.md) covers all three, with the same tested examples.

# Testing with Ducky.Testing

`Ducky.Testing` runs your slices and effects on a real store, with a fake clock, and works with any test framework: a failed check throws `DuckyAssertionException`.

```shell
dotnet add package Ducky.Testing
```

## Slices

`SliceTest` seeds a state, dispatches actions and checks the result:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-slice)]

`Then(expected)` compares with `Equals`, `Then(state => ...)` runs your own assertions, and `ThenUnchanged()` checks that the reducers returned the same reference.

## Effects

`EffectTest.Run` builds a store with the effect, dispatches one action, waits until the store is idle and returns what the effect dispatched. Given these actions and this effect:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-effect-actions)]

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-effect-under-test)]

the test registers a fake for its dependency (this sample nests the effect in a class named `App`):

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-effect)]

The actions an effect dispatches usually have no reducer in the test; they are results, not failures. Add the slices the effect reads with `configure` (`d => d.AddSlice<TodosSlice>()`) and seed them with `arrange` (`store => store.Seed(...)`). The store builds the effect, so an effect that injects a SliceStore gets the store-owned instance.

## The whole store

`TestStore` is an `IStore` for tests. A test of one slice needs no lambda:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-store-single)]

Several slices, seeding and settling go through `Create(configure)`:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/TestingGuideTests.cs#testing-store)]

- `Create` registers a `FakeTimeProvider` (`store.Time`) and sets `ThrowOnUnhandledAction`, so an action that no slice or effect handles is a failure. Set it back to `false` in `configure` when that is expected.
- `Seed` restores a state as hydration does: `State.WasRestored<TState>()` becomes true.
- `Settled()` waits until the queue is empty and no effect runs, with a real-time bound (`WallClockTimeout`, 10 seconds), and throws `DuckyAssertionException` when it expires. Like `WhenIdleAsync`, it doesn't wait for `LongRunning` effects.
- `Processed` lists every action the store reduced (or whose reducer failed), with its origin, depth and correlation; `Failures` lists `ReducerFailed` and `EffectFailed`.
- `GetSlice<CartStore>()` returns the store-owned SliceStore, to call its methods.
- `DisposeAsync` has the same real-time bound, so a test whose effect ignores cancellation fails instead of hanging.

Advance the fake clock with `store.Time.Advance(...)` to run timers and delays that effects create with `context.Time`.

## Components

A component that selects from the store (`DuckyComponent`, `DuckySelect`) re-renders after the store commits: the store drains on its own thread, then posts a change check to the renderer. An assertion made right after a dispatch or an interaction can therefore run before the markup changes. `WhenIdleAsync` and `Settled()` only wait for the store.

After a dispatch or interaction, settle before asserting on markup. `WhenRenderedAsync` (Ducky.Blazor) waits until the store is idle and then until the render it requested has run:

[!code-csharp[](../../../test/Ducky.Blazor.Tests/Components/StoreRenderingTests.cs#when-rendered)]

To test a union slice or a union effect group on a `TestStore`, see [Unions in Ducky](unions.md#one-exhaustive-reducer).

It is async, so there is nothing to block on: [DUCKY010](../../diagnostics/DUCKY010.md) reports `WhenRenderedAsync(...).GetAwaiter().GetResult()` like the other Ducky tasks. Output that depends on the culture follows the render's execution context, which is empty (see #296).

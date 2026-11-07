# Reactive effects

`Ducky.Reactive` writes effects as [R3](https://github.com/Cysharp/R3) pipelines: a `ReactiveEffect` receives the stream of actions and the stream of states, and returns a stream of actions to dispatch. Use it where time and streams are the point (debouncing, polling, per-key switching); an `Effect<T>` is simpler for everything else.

```shell
dotnet add package Ducky.Reactive
```

## Debounced search

The state and actions of a search box:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/RecipesGuideTests.cs#reactive-search-state)]

The effect waits for a pause in typing, then searches; a newer term cancels the request in flight:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/RecipesGuideTests.cs#reactive-search)]

Register it with `AddReactiveEffect<T>()`. The generated `AddDuckyGenerated_{Assembly}()` registers every accessible reactive effect too (this sample nests its effects in a class named `App`):

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/RecipesGuideTests.cs#reactive-register)]

- The store creates the effect from its own scope, with constructor dependencies from DI, and disposes it with the store. Inject `TimeProvider` for every time operator, so tests can drive the clock.
- `OfActionType<T>()` matches the exact type. The `state` stream starts with the current state; `StoreInitialized` reaches a pipeline once, when it starts.
- An emission dispatched synchronously in reaction to an action is part of its chain, so an action that re-emits itself stops at the depth guard (64).

## Polling while a flag is set

`state` drives the timer: turning `AutoRefresh` on starts it, turning it off disposes it.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/RecipesGuideTests.cs#reactive-polling)]

## A switch per key

To cancel a load in flight only when the *same* item is requested again, group by key and switch inside each group. Use `GroupByUntil`, never a plain `GroupBy`: a group that never ends keeps one open subject per key the pipeline ever saw. R3 has neither operator, so here is one to copy:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/GroupByUntilOperator.cs#reactive-group-by-until)]

[!code-csharp[](../../../test/Ducky.Docs.Tests/Reactive/RecipesGuideTests.cs#reactive-keyed-switch)]

## Failures

- An exception in a pipeline dispatches `EffectFailed` with `ActionType` `"reactive"`, as a system action: strict mode and the unhandled-action check ignore it. A non-terminal error (`OnErrorResume`) keeps the stream going.
- A pipeline that ends with a failure is subscribed again after `RetryDelay(attempt)`: 1, 2, 4, 8, 16, then 30 seconds, and no more after the eighth attempt. Override `RetryDelay` to change it (`null` stops). A successful emission resets the count.
- A pipeline that reacts to `EffectFailed` must leave reactive failures out (`.Where(static failed => failed.ActionType != "reactive")`), or its own failure during an outage would feed it forever.

## Idle, prerender and tests

Reactive pipelines are never "running" for the store: `WhenIdleAsync`, `TestStore.Settled()` and the prerender seed don't wait for them, even with a request in flight.

- Load a `Prerender<T>` slice with an `Effect<T>`, never a reactive effect ([Prerender](../blazor/prerender.md)).
- In tests, advance the fake clock (`store.Time.Advance(...)`), then check the outcome; `Settled()` alone doesn't wait for a pipeline.

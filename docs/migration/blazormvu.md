# Migrating from BlazorMVU

BlazorMVU keeps a model inside each component and updates it with one `Update(msg, model)` function. In Ducky 2.0 the model is a slice state in the app store, each message an action, and commands and subscriptions are effects.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/blazormvu`. `Before` builds against BlazorMVU: BlazorMVU was never published to nuget.org, so the sample uses a verbatim copy of its source in `docs/migration/samples/legacy/BlazorMVU.Core`. `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests.

## Checklist

1. Replace BlazorMVU with `Ducky` and `Ducky.Blazor`, and add `Ducky.Testing` to test projects.
2. Turn the model into a slice state and each message into a plain action record (see [Model and messages](#model-and-messages)).
3. Turn `Init` into `Initial` and each case of `Update` into one `On<Msg>`.
4. Move each `Cmd` into an effect, and each `Sub` into a long-running effect.
5. Derive the component from `DuckyComponent<TState>`, register with `AddDucky` (see [Components and registration](#components-and-registration)), then run the app's tests.

## Before and after

### Model and messages

[!code-csharp[](samples/blazormvu/Before/Counter.cs#before-model)]

[!code-csharp[](samples/blazormvu/After/Counter.cs#after-model)]

### Init and Update

[!code-csharp[](samples/blazormvu/Before/Counter.cs#before-update)]

[!code-csharp[](samples/blazormvu/After/Counter.cs#after-update)]

### Commands

[!code-csharp[](samples/blazormvu/Before/Counter.cs#before-cmd)]

[!code-csharp[](samples/blazormvu/After/Counter.cs#after-cmd)]

`ctx.Run` covers the started/succeeded/failed triad: any exception other than the run's own cancellation dispatches `failed`. The reducer no longer starts the work; the effect reacts to `LoadCount` after it was reduced, and `Policy` decides what a second `LoadCount` does while one runs (see [Effects](../guides/core/effects.md)).

### Subscriptions

[!code-csharp[](samples/blazormvu/Before/Counter.cs#before-sub)]

[!code-csharp[](samples/blazormvu/After/Counter.cs#after-sub)]

The effect waits on `context.Time`, so a test drives it with `TestStore.Time` (a `FakeTimeProvider`). With Ducky.Reactive installed, a `ReactiveEffect` over `Observable.Interval(period, timeProvider)` does the same.

### Components and registration

[!code-csharp[](samples/blazormvu/Before/Counter.cs#before-component)]

[!code-csharp[](samples/blazormvu/After/CounterView.cs#after-component)]

[!code-csharp[](samples/blazormvu/After/Registration.cs#after-registration)]

## Mapping table

| BlazorMVU | Ducky 2.0 |
|---|---|
| `MvuComponent<TModel, TMsg>` | a `Slice<TState>` for the model, and a `DuckyComponent<TState>` that renders it |
| `Init()`, `InitWithCmd()` | `Initial`; the initial command becomes an `Effect<StoreInitialized>` |
| an `Update(msg, model)` switch, `UpdateWithCmd` | one `On<Msg>` per case; the command part becomes an effect |
| `Dispatch(msg)`, `DispatchAsync(msg)` | `Dispatch(action)`, `DispatchAsync(action)` (on the component, the store or an effect's context) |
| `Cmd.OfTask`, `Cmd.OfTaskUnit`, the `Cmd<TMsg>.OfTaskOption` case | an `Effect<Msg>`, with `ctx.Run` for the started/succeeded/failed triad |
| `Cmd.OfMsg`, `Cmd.OfFunc`, `Cmd.Batch` | a synchronous `ctx.Dispatch` in an effect, once per message |
| `Cmd.OfEffect` | an effect that runs the side effect and dispatches nothing |
| `Cmd.Delay`, `Cmd.DelayMsg` | an effect that awaits `Task.Delay(delay, ctx.Time, cancellationToken)`, then dispatches |
| `Sub.Timer`, `Sub.Every` | a `ReactiveEffect` with `Observable.Interval(period, timeProvider)`, or a `LongRunning` polling effect |
| `SimpleMvuComponent<TModel, TMsg>` local state | a slice (global), or plain component fields where it is truly local |
| `Middleware.Debounce`, `Middleware.Throttle` | an `Effect` with `Switch` + `ctx.Time`, or an Rx `Debounce` |
| `Middleware.Logger`, `Middleware.Timing`, `Middleware.ErrorHandler` | `ILogger`, `ActivitySource` and `Meter`; an effect on `ReducerFailed` or `EffectFailed` |
| `TimeTravelDebugger`, `EnableTimeTravel` | Redux DevTools (`UseDevTools()`) |

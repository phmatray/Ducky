# Migrating from TimeWarp.State

This guide takes a Blazor app from TimeWarp.State 11.0.3 (formerly Blazor-State), its latest stable release as of 2026-11-08, to Ducky 2.0. TimeWarp.State routes each action through MediatR to one async handler that mutates a mutable state. Ducky splits that handler in two: a pure reducer that returns the next immutable state, and an effect for the part that awaits.

The 12.0 line of TimeWarp.State is a pre-release: 12.0.0-beta.17 on nuget.org as of 2026-11-08. Its betas depend on TimeWarp.Mediator instead of MediatR (from [12.0.0-beta.1](https://www.nuget.org/packages/TimeWarp.State/12.0.0-beta.1)); from [12.0.0-beta.9](https://github.com/TimeWarpEngineering/timewarp-state/blob/v12.0.0-beta.9/documentation/release-notes/release12.0.0-beta.9.md) they target `net11.0` only, and [12.0.0-beta.11](https://github.com/TimeWarpEngineering/timewarp-state/blob/v12.0.0-beta.11/documentation/release-notes/release12.0.0-beta.11.md) generates the state clones and removes the Redux DevTools integration. This guide maps the stable 11.0.3 line, which targets `net8.0`.

Ducky 2.0 targets `net10.0` only, and it is a pre-release. [How Ducky compares](../compare.md) sets the two side by side.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/timewarp-state`. `Before` is user code against `TimeWarp.State` 11.0.3 from nuget.org, written from TimeWarp.State's own [sample](https://github.com/TimeWarpEngineering/timewarp-state/tree/11.0.3/Samples/00-StateActionHandler). `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests and one test per behaviour change below.

## Checklist

1. Target `net10.0`. Replace `TimeWarp.State` (and `TimeWarp.State.Plus`) with `Ducky` and `Ducky.Blazor`, and add `Ducky.Testing` to test projects. Remove MediatR, AnyClone and Blazored storage if nothing else uses them.
2. Turn each `State<T>` into an immutable record and move `Initialize()` into a slice's `Initial` (see [State](#state)).
3. Lift each nested `*ActionSet.Action` to a top-level record. Mark it `[DuckyAction]` to keep a generated dispatch method (see [Actions and handlers](#actions-and-handlers)).
4. Rewrite each handler's mutations as `On<TAction>` reducers returning `state with { ... }`. Split an async handler into a reducer for what it set before the `await` and an `Effect<TAction>` that dispatches what it learned (see [The async handler becomes an effect](#the-async-handler-becomes-an-effect)).
5. Replace `TimeWarpStateComponent` and `GetState<T>()` with `DuckyComponent<TState>` or `Select` (see [Components](#components)).
6. In `_Imports.razor` and the `.razor` files, replace `@using TimeWarp.State` with `@using Ducky` and `@using Ducky.Blazor`, `@inherits TimeWarpStateComponent` with `@inherits DuckyComponent<TState>` (or `DuckyComponent` and a `Select` in `OnInitialized`), and calls of the generated `CounterState.X(...)` methods with `Store.X(...)` or `await Store.XAsync(...)`.
7. Replace `AddTimeWarpState` with `AddDucky` and the generated `AddDuckyGenerated_{Assembly}()` (see [Registration](#registration)).
8. Add a `JsonSerializerContext` listing the state and action types and pass it with `UseJson`: DevTools payloads, persisted slices and prerender seeds are serialized through it, and [DUCKY006](../diagnostics/DUCKY006.md) lists the types it lacks.
9. Run the app's tests, then read [Behaviour changes](#behaviour-changes).

## Before and after

### State

[!code-csharp[](samples/timewarp-state/Before/CounterState.cs#before-state)]

[!code-csharp[](samples/timewarp-state/After/CounterState.cs#after-state)]

### Actions and handlers

[!code-csharp[](samples/timewarp-state/Before/CounterState.cs#before-increment)]

The action leaves its state class, and the handler's mutation becomes a reducer in the slice:

[!code-csharp[](samples/timewarp-state/After/CounterState.cs#after-actions)]

[!code-csharp[](samples/timewarp-state/After/CounterState.cs#after-slice)]

TimeWarp.State's generator turns each `*ActionSet` into a method on the state, `await CounterState.IncrementCount(amount: 1)`, which [sends the action through MediatR](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State.SourceGenerator/ActionSetMethodGenerator.cs#L84-L93). `[DuckyAction]` gives the same shape on anything that dispatches: `Store.IncrementCount(1)` and `await Store.IncrementCountAsync(1)` (see [Getting started](../guides/core/getting-started.md#declare-state-actions-and-a-slice)).

### The async handler becomes an effect

[!code-csharp[](samples/timewarp-state/Before/CounterState.cs#before-load)]

[!code-csharp[](samples/timewarp-state/After/CounterState.cs#after-effect)]

What the handler set before its `await` is now `LoadCount`'s reducer, above; what it set after is `CountLoaded` or `CountLoadFailed`, dispatched by the effect. The effect takes its services through its constructor, as the handler did, and its token is cancelled when the run is superseded or the store is disposed, which replaces `State<T>.CancelOperations()`. `Policy` picks `Merge` (the default), `Switch`, `Exhaust` or `Queue` (see [Effects and concurrency policies](../guides/core/effects.md)).

### Components

[!code-csharp[](samples/timewarp-state/Before/CounterView.cs#before-component)]

[!code-csharp[](samples/timewarp-state/After/CounterView.cs#after-component)]

`GetState<T>()` [subscribes the component](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Components/TimeWarpStateComponent.cs#L76-L80) to every change of that state. `DuckyComponent<TState>` does the same through its `State` property; a `DuckyComponent` that calls `Select((CounterState s) => s.Count)` in `OnInitialized` re-renders only when that value changed, which covers what `RegisterRenderTrigger` was for (see [Selectors](../guides/core/selectors.md)).

The component still loads in `OnInitializedAsync`, as before. On a prerendered page that runs twice; the [load rule](../guides/blazor/render-modes.md#one-load-rule-in-every-mode) moves the first load into an effect on `StoreInitialized` and, with `Prerender<T>`, runs it once per page.

### Registration

[!code-csharp[](samples/timewarp-state/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/timewarp-state/After/Registration.cs#after-registration)]

`AddDuckyGenerated_{Assembly}()` is generated at build time for each assembly that declares slices or effects (see [Getting started](../guides/core/getting-started.md#register)). The `AppJson` passed to `UseJson` is a `JsonSerializerContext` listing the state and action types: Ducky serializes DevTools payloads, persisted slices and prerender seeds through it, never by reflection.

The tests use `Ducky.Testing` instead of a host and `ISender`:

[!code-csharp[](samples/timewarp-state/After.Tests/CounterTests.cs#after-test)]

## Mapping table

TimeWarp.State column: TimeWarp.State 11.0.3, as of 2026-11-08.

| TimeWarp.State 11.0.3 | Ducky 2.0 |
|---|---|
| `TimeWarp.State` (`net8.0`; depends on MediatR, version 12.4.1 or later, and AnyClone) | `Ducky`, `Ducky.Blazor` (`net10.0`; no MediatR) |
| `services.AddTimeWarpState(o => o.Assemblies = [...])` ([assembly scan and MediatR registration](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Extensions/ServiceCollectionExtensions.AddTimeWarpState.cs#L105-L119)) | `services.AddDucky(d => d.AddDuckyGenerated_{Assembly}())`, generated at build time |
| `sealed partial class XState : State<XState>` with `private set` properties | a `record` state and a `Slice<TState>` |
| `Initialize()` | `protected override TState Initial` |
| nested `XActionSet.Action : IAction` (a MediatR `IRequest`) | a top-level `record`, no interface |
| generated `await XState.DoThing(args)` | `[DuckyAction]`: `Store.DoThing(args)` or `await Store.DoThingAsync(args)` |
| `Mediator.Send(new XState.XActionSet.Action(...))` | `Store.Dispatch(action)` or `await Store.DispatchAsync(action)` |
| `ActionHandler<TAction>` mutating `Store.GetState<XState>()` | `On<TAction>((state, action) => state with { ... })` in the slice |
| an async `Handle` that awaits a service | a reducer for the start, plus an `Effect<TAction>` that dispatches the result |
| the `CancellationToken` of `Handle`, `State<T>.CancelOperations()` | the effect's run token; `Policy => Concurrency.Switch` cancels the previous run |
| `StateTransactionBehavior` (on by default): clone before, restore and publish `ExceptionNotification` on a throw | nothing to clone: a reducer that throws commits nothing and dispatches `ReducerFailed`; an effect that throws dispatches `EffectFailed` |
| `TimeWarpStateComponent`, `GetState<T>()` | `DuckyComponent<TState>` (`State`), or `DuckyComponent` + `Select` in `OnInitialized` |
| `RegisterRenderTrigger<TState>(...)` | `Select(selector)`: the component re-renders when the selected value changes |
| `GetPreviousState<T>()` | `ActionContext.PreviousState` in a `Middleware` |
| MediatR `IPipelineBehavior<,>`, `IRequestPreProcessor<>`, `IRequestPostProcessor<,>` | `Middleware` (`MayDispatch`, `BeforeReduce`, `AfterReduce`) for the synchronous part, an effect for the asynchronous part |
| `o.UseReduxDevTools()` + `<TimeWarpJavaScriptInterop />` + `<ReduxDevTools />` | `d.UseDevTools(...)` guarded by `IsDevelopment()`, no component ([Redux DevTools](../guides/blazor/devtools.md)) |
| TimeWarp.State.Plus `[PersistentState(PersistentStateMethod.LocalStorage / SessionStorage)]` + manual post-processor registration | `d.Persist<TSlice>(...)` or `[Persist(Storage = PersistStorage.Session)]` ([Persistence](../guides/blazor/persistence.md)) |
| `PersistentStateMethod.PreRender`, `.Server` ([not implemented](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State.Plus/Features/Persistentence/Services/PersistenceService.cs#L35-L36) in 11.0.3) | `d.Prerender<TSlice>()` ([Prerender](../guides/blazor/prerender.md)); `PersistStorage.Server` keeps a slice in `IDistributedCache` |
| no multi-tab sync | `Persist<TSlice>(o => o.SyncAcrossTabs = true)` ([Cross-tab sync](../guides/blazor/cross-tab.md)) |
| `RouteState` (`UseRouting`) | no router state: use `NavigationManager` |
| integration tests that build a host and `Send` through `ISender` | `SliceTest`, `EffectTest`, `TestStore` in `Ducky.Testing` ([Testing](../guides/core/testing.md)) |

## Behaviour changes

Each comes with the test that pins it.

### State is immutable

A TimeWarp.State handler mutates the state in place, so `StateTransactionBehavior`, on by default ([`UseStateTransactionBehavior`](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Extensions/TimeWarpStateOptions.cs#L20)), [clones the state](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Features/CloneState/Pipeline/StateTransactionBehavior.cs#L58-L60) before every action to be able to roll it back. A Ducky reducer returns a new record and the store swaps the reference: a snapshot you hold never changes, and there is no clone to make or to get wrong.

[!code-csharp[](samples/timewarp-state/After.Tests/BehaviourChangeTests.cs#immutable-state)]

### What an async handler set before its await is rendered at once

TimeWarp.State re-renders a state's subscribers in a MediatR post-processor, [after the handler returns](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Features/RenderSubscriptions/RenderSubscriptionsPostProcessor.cs#L22-L31) (Blazor may re-render the calling component at the handler's first `await`), and the generated method completes after the whole handler. In Ducky, `LoadCount` is reduced and its subscribers notified before the effect's API call answers, and `await Store.LoadCountAsync()` completes then, not when the count arrives. To wait for the effect too, use `WhenIdleAsync()` (or `TestStore.Settled()` in a test).

[!code-csharp[](samples/timewarp-state/After.Tests/BehaviourChangeTests.cs#intermediate-state)]

### Effects have a concurrency policy

TimeWarp.State applies no concurrency policy: each `Send` runs its handler, even while an earlier one for the same action is still running, and [`CancelOperations()`](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/State/State.cs#L69-L75) cancels the token that every operation of the state shares. A Ducky effect picks a policy per effect: with `Switch`, a newer `LoadCount` cancels only the running load, and the late result of the superseded run is dropped.

[!code-csharp[](samples/timewarp-state/After.Tests/BehaviourChangeTests.cs#effect-switch)]

### Failures are actions, not swallowed exceptions

When a TimeWarp.State handler throws, `StateTransactionBehavior` [restores the state, publishes an `ExceptionNotification` and returns](https://github.com/TimeWarpEngineering/timewarp-state/blob/11.0.3/Source/TimeWarp.State/Features/CloneState/Pipeline/StateTransactionBehavior.cs#L110-L120): by default the caller sees no exception. In Ducky, a reducer that throws commits nothing in any slice, the dispatch ends `Failed`, and the store dispatches `ReducerFailed`; an effect that throws dispatches `EffectFailed`. Handle either with an effect on that failure action, and see them in `TestStore.Failures`.

[!code-csharp[](samples/timewarp-state/After.Tests/BehaviourChangeTests.cs#failures)]

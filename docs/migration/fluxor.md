# Migrating from Fluxor

This guide takes a Blazor app from Fluxor 6.11.0 (`Fluxor` and `Fluxor.Blazor.Web`, the latest release as of 2026-11-08) to Ducky 2.0. The two libraries share the Redux model: a feature state becomes a slice, a reducer method becomes an `On<TAction>` in that slice, an effect method becomes an `Effect<TAction>`. What changes is mostly around the edges: how the store is registered and started, what an effect can do about concurrency, and what a dispatch tells you.

Ducky 2.0 targets `net10.0` only, and it is a pre-release. Fluxor 6.11.0 targets `net8.0`, `net9.0` and `net10.0` and has years of production use. If your app can't move to .NET 10 yet, stay on Fluxor. [How Ducky compares](../compare.md) sets the two side by side.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/fluxor`. `Before` is user code against `Fluxor.Blazor.Web` 6.11.0 from nuget.org, written from Fluxor's own [tutorials](https://github.com/mrpmorris/Fluxor/tree/6.11.0/Source/Tutorials/02-Blazor). `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests and one test per behaviour change below.

## Checklist

1. Target `net10.0`. Replace `Fluxor` and `Fluxor.Blazor.Web` with `Ducky` and `Ducky.Blazor`, and add `Ducky.Testing` to test projects. Remove `Fluxor.Blazor.Web.ReduxDevTools` (DevTools ship in `Ducky.Blazor`) and the community `Fluxor.Persist` (persistence ships in `Ducky.Blazor`).
2. Make each `[FeatureState]` a plain record, and move its `[ReducerMethod]`s into the constructor of a `Slice<TState>`, one `On<TAction>` each (see [State, actions and reducers](#state-actions-and-reducers)).
3. Turn each `[EffectMethod]` into an `Effect<TAction>` that dispatches through its `context`, passes its `cancellationToken` on, and picks a `Policy` (see [Effects](#effects)).
4. Replace `FluxorComponent` with `DuckyComponent`, and each injected `IState<T>` or `IStateSelection<TState, TValue>` with a `Select` in `OnInitialized` (see [Components](#components)).
5. In `_Imports.razor` and the `.razor` files, replace `@using Fluxor` and `@using Fluxor.Blazor.Web.Components` with `@using Ducky` and `@using Ducky.Blazor`, `@inherits FluxorComponent` with `@inherits DuckyComponent`, and each `@inject IState<T>` or `@inject IStateSelection<TState, TValue>` with a `Select` in the `@code` block's `OnInitialized`; drop `@inject IDispatcher`, since the component's own `Dispatch` and `DispatchAsync` replace it.
6. Replace `AddFluxor` with `AddDucky` and the generated `AddDuckyGenerated_{Assembly}()`, register middleware with `Use<T>()`, and pass a `JsonSerializerContext` to `UseJson` (see [Registration](#registration)).
7. Delete `<StoreInitializer />`. Move the first load from `OnInitialized` to an effect on `StoreInitialized`, and register a prerendered slice with `Prerender<T>` (see [The store initializer and the first load](#the-store-initializer-and-the-first-load)).
8. Run the app's tests, then read [Behaviour changes](#behaviour-changes).

## Before and after

### State, actions and reducers

[!code-csharp[](samples/fluxor/Before/Counter.cs#before-state)]

[!code-csharp[](samples/fluxor/After/Counter.cs#after-state)]

Actions don't change: any record is an action in both libraries. The reducers move from static `[ReducerMethod]`s, which Fluxor's scan finds anywhere in the assembly, into the slice that owns the state:

[!code-csharp[](samples/fluxor/Before/Counter.cs#before-reducers)]

[!code-csharp[](samples/fluxor/After/Counter.cs#after-reducers)]

Both libraries treat a reducer that returns the same reference as no change: Fluxor's `Feature<TState>` compares with [`ReferenceEquals`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/Feature.cs#L95), and so does Ducky (see [Slices and keys](../guides/core/slices-and-keys.md#reducers)). Fluxor's feature name becomes the slice key, derived from the class name unless you override `Key`; pin it once the slice is persisted or prerendered, as the sample does.

### Effects

[!code-csharp[](samples/fluxor/Before/Counter.cs#before-effect)]

[!code-csharp[](samples/fluxor/After/Counter.cs#after-effect)]

The effect takes its services through its constructor, as Fluxor's instance effects do, and both libraries create one effect instance per store. What is new is the `CancellationToken`, cancelled when the run is superseded or the store is disposed, and the `Policy`: `Merge` (the default, what Fluxor always does), `Switch`, `Exhaust` or `Queue` (see [Effects and concurrency policies](../guides/core/effects.md)). Dispatch through the `context`, never through an injected `IStore`: the context knows which run dispatches, so a superseded run's late results are dropped (DUCKY008 warns on the injection).

### Middleware

[!code-csharp[](samples/fluxor/Before/Counter.cs#before-middleware)]

[!code-csharp[](samples/fluxor/After/Counter.cs#after-middleware)]

Fluxor's four main [`Middleware`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/Middleware.cs#L21-L33) hooks map across: `MayDispatchAction` becomes `MayDispatch`, `BeforeDispatch` becomes `BeforeReduce`, `AfterDispatch` becomes `AfterReduce`, and `InitializeAsync(IDispatcher, IStore)` becomes `InitializeAsync(CancellationToken)` with the store in the `Store` property. Ducky's dispatch hooks receive an `ActionContext` (the action, its type name, origin, depth and the states before and after) and are synchronous: asynchronous work belongs in an effect. Fluxor's `AfterInitializeAllMiddlewares` and the client scripts of an `IWebMiddleware` have no counterpart.

### Components

[!code-csharp[](samples/fluxor/Before/CounterView.cs#before-component)]

[!code-csharp[](samples/fluxor/After/CounterView.cs#after-component)]

`FluxorComponent` finds its `IState<T>` and `IStateSelection` members [by reflection](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/StateSubscriber.cs#L59) when its `OnInitialized` runs, which is why an override must call `base.OnInitialized()`: Fluxor's analyzer ([FLXW01](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Docs/releases.md)) reports a missing call, and [disposal throws](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor.Blazor.Web/Components/FluxorComponent.cs#L82) otherwise. A `DuckyComponent` subscribes through the `Select` calls you make in `OnInitialized`, with no base call needed. `Select` after the first render throws (DUCKY352), and DUCKY009 catches a `Select` in markup.

`IStateSelection` maps to the same `Select`, narrowed to one value:

[!code-csharp[](samples/fluxor/Before/CounterView.cs#before-selection)]

[!code-csharp[](samples/fluxor/After/CounterView.cs#after-selection)]

For a selector shared by several components, or one combining slices, use `Selector.Define` (see [Selectors](../guides/core/selectors.md)); Fluxor has no memoized selector type.

### The store initializer and the first load

[!code-csharp[](samples/fluxor/Before/CounterView.cs#before-initializer)]

Fluxor's `StoreInitializer` starts the store in [`OnAfterRenderAsync`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor.Blazor.Web/StoreInitializer.cs#L78-L88), and until then dispatched actions [wait in a queue](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/Store.cs#L163-L164). Ducky needs no initializer: the store initializes on its first use (a dispatch, a `Select`, a `State` read). Delete the component. `<DuckyInitializer>` exists for the cases its guides name: a static SSR page that must show Server-storage values ([Static SSR](../guides/blazor/static-ssr.md)), or a WebAssembly app with a persistence `Scope` and no Ducky component ([Persistence](../guides/blazor/persistence.md#scopes)).

The Fluxor component dispatched the first `LoadCounterAction` from `OnInitialized`. On a prerendered page that runs twice, once in the prerender pass and once interactively. In Ducky the first load is an effect on `StoreInitialized` that returns early when the slice was restored, the [load rule](../guides/blazor/render-modes.md#one-load-rule-in-every-mode):

[!code-csharp[](samples/fluxor/After/Counter.cs#after-load-rule)]

### Registration

[!code-csharp[](samples/fluxor/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/fluxor/After/Registration.cs#after-registration)]

`AddDuckyGenerated_{Assembly}()` is generated at build time for each assembly that declares slices or effects (see [Getting started](../guides/core/getting-started.md#register)); a class library that declares slices has its own. In a Blazor Web App, call `AddDucky` in both `Program.cs` files, as Fluxor's tutorial asks for `AddFluxor`. Ducky serializes through the `JsonSerializerContext` you pass to `UseJson`, never by reflection: the prerender seed, persistence and DevTools need it, and DUCKY006 lists the types it lacks:

[!code-csharp[](samples/fluxor/After/AppJson.cs#after-json)]

## Mapping table

Fluxor column: Fluxor 6.11.0, as of 2026-11-08.

| Fluxor 6.11 | Ducky 2.0 |
|---|---|
| `Fluxor`, `Fluxor.Blazor.Web` (`net8.0`, `net9.0`, `net10.0`) | `Ducky`, `Ducky.Blazor` (`net10.0`) |
| `services.AddFluxor(o => o.ScanAssemblies(asm))` | `services.AddDucky(d => d.AddDuckyGenerated_{Assembly}())`, generated at build time |
| `o.ScanTypes(...)` | `d.AddSlice<TSlice>()`, `d.AddEffect<TEffect>()` |
| `o.AddMiddleware<T>()` | `d.Use<T>()` |
| `o.WithLifetime(StoreLifetime.Scoped / Singleton)` | `d.Lifetime`; by default a singleton in the browser and scoped on the server |
| `<Fluxor.Blazor.Web.StoreInitializer />` | nothing: the store initializes on first use; `<DuckyInitializer>` is optional |
| `StoreInitializer.UnhandledException` | an effect on `EffectFailed` or `ReducerFailed` |
| `[FeatureState]` record with a parameterless constructor, or `Feature<TState>` with `GetInitialState()` | a plain record and a `Slice<TState>` with `protected override TState Initial` |
| `[FeatureState(Name = "...")]`, `Feature.GetName()` | the slice `Key`, derived from the class name or overridden |
| `[ReducerMethod] static TState M(TState, TAction)` | `On<TAction>((state, action) => ...)` in the slice constructor |
| `[ReducerMethod(typeof(TAction))] static TState M(TState)` | `On<TAction>(state => ...)` |
| `Reducer<TState, TAction>` | `On<TAction>` in the slice |
| `[EffectMethod] Task M(TAction, IDispatcher)`, `[EffectMethod(typeof(TAction))]` | `Effect<TAction>.Handle(action, EffectContext, CancellationToken)`; several actions in one class: `EffectGroup` |
| `Effect<TTriggerAction>.HandleAsync(action, dispatcher)` | `Effect<TAction>.Handle(action, context, cancellationToken)` |
| a `CancellationToken` shared by hand between effect methods | `Policy => Concurrency.Switch` (or `Exhaust`, `Queue`) and the run's token |
| `IDispatcher.Dispatch(action)` (returns `void`) | `Dispatch(action)`, or `await DispatchAsync(action)` for a `DispatchResult` once reduced |
| `IState<TState>`, `@inject IState<T>` + `FluxorComponent` | `DuckyComponent` + `Select(...)` in `OnInitialized`, or `DuckyComponent<TState>` |
| `IStateSelection<TState, TValue>.Select(selector, valueEquals, selectedValueChanged)` | `Select(selector, comparer)` on a component; `store.Select(selector, onChange, comparer)` elsewhere |
| `FluxorLayout` | `DuckyLayout` |
| `MaximumStateChangedNotificationsPerSecond` | no equivalent: a component re-renders only when a value it selected changed |
| `SubscribeToAction<TAction>(callback)`, `IActionSubscriber` | no component-level action subscription: put the reaction in an `Effect<TAction>`, or select the state the action changes |
| `Middleware`: `InitializeAsync`, `MayDispatchAction`, `BeforeDispatch`, `AfterDispatch` | `Middleware`: `InitializeAsync`, `MayDispatch`, `BeforeReduce`, `AfterReduce` |
| `o.UseRouting()`, `GoAction` | no router state: use `NavigationManager` |
| `Fluxor.Blazor.Web.ReduxDevTools`, `o.UseReduxDevTools(...)` | `d.UseDevTools(...)` in `Ducky.Blazor`, guarded by `IsDevelopment()` ([Redux DevTools](../guides/blazor/devtools.md)) |
| community [`Fluxor.Persist`](https://github.com/Tailslide/fluxor-persist) (`net8.0`, `net9.0`): `UsePersist`, `IStringStateStorage` | `d.Persist<TSlice>(...)` or `[Persist]`: `localStorage`, `sessionStorage` or `IDistributedCache` ([Persistence](../guides/blazor/persistence.md)) |
| no multi-tab sync | `Persist<TSlice>(o => o.SyncAcrossTabs = true)` ([Cross-tab sync](../guides/blazor/cross-tab.md)) |
| no prerender state transfer in a release ([#547](https://github.com/mrpmorris/Fluxor/issues/547)) | `d.Prerender<TSlice>()` ([Prerender](../guides/blazor/prerender.md)) |
| static reducers called directly; `Effect<T>.HandleAsync` with a mocked `IDispatcher` | `SliceTest`, `EffectTest`, `TestStore` in `Ducky.Testing` ([Testing](../guides/core/testing.md)) |

## Behaviour changes

Each comes with the test that pins it.

### The store works during prerender and static SSR

Fluxor's `StoreInitializer` initializes the store in `OnAfterRenderAsync`, which a prerender pass or a static SSR page never reaches, so with it no released version reduces actions or runs effects there; the workaround reported in [#547](https://github.com/mrpmorris/Fluxor/issues/547), calling [`IStore.InitializeAsync()`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/IStore.cs#L78) from a component, breaks JavaScript middleware such as Redux DevTools. #547 was closed as completed on 2026-09-03, but as of 2026-11-08 no release contains the change: 6.11.0, of 2026-08-03, is the latest, and the work sits on the unreleased branch `547/PeteM-SSR`. A Ducky store initializes on its first use, rendered or not:

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#no-initializer)]

With `Prerender<T>`, the prerender pass waits until its store is idle, writes the slice into the page's persisted component state, and the interactive store starts from it. The slice reads as restored, so the load rule doesn't load it a second time ([Prerender](../guides/blazor/prerender.md)):

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#load-rule)]

The full prerender-to-interactive handoff is pinned in Ducky's own suite (`Prerender_RestoredSlice_LoadEffectRunsOnceAcrossHandoff` in `test/Ducky.Blazor.Tests/Prerender/PrerenderHandoffTests.cs`).

### Effects have a concurrency policy and a cancellation token

Fluxor starts every matching effect for every action and [awaits them together in the background](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/Store.cs#L188-L236), with no cancellation token: two quick loads both run to the end, and the one that answers last wins, whatever the order they were dispatched in. A Ducky effect with `Switch` cancels the running load and drops its late result:

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#effect-switch)]

Keep the default `Merge` where every run must complete, as in Fluxor.

### Dispatch returns a DispatchResult

Fluxor's [`IDispatcher.Dispatch`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/IDispatcher.cs#L16) returns `void`, and no release has a `DispatchAsync` (the work on [#322](https://github.com/mrpmorris/Fluxor/issues/322) is unreleased). Ducky's `Dispatch` also returns `void`; `await DispatchAsync(...)` completes once the action is reduced, before its effects finish, with `Reduced`, `Vetoed`, `Dropped`, `Failed` or `Disposed`. The task never faults: a reducer failure is the `Failed` result and a `ReducerFailed` action (see the [threading contract](../threading.md)). To also wait for effects, use `WhenIdleAsync()`.

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#dispatch-result)]

### Handlers match the exact action type

Fluxor matches a reducer or an effect with [`action is TAction`](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/DependencyInjection/Wrappers/ReducerWrapper.cs#L12), so a handler of a base action also runs for every derived action. Ducky matches the exact type only ([ADR-0008](../adr/0008-exact-type-handler-matching-everywhere.md)): give each action its own `On<T>`, and [DUCKY005](../diagnostics/DUCKY005.md) rejects a handler for an abstract type, an interface or `object`, which could never match.

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#exact-type)]

### Registration is generated, not scanned

Fluxor finds features, reducers and effects with a [reflection scan](https://github.com/mrpmorris/Fluxor/blob/6.11.0/Source/Lib/Fluxor/DependencyInjection/ServiceCollectionExtensions.cs#L40) at startup; its maintainer notes that this scan is not compatible with Native AOT, while registering features, reducers and effects by hand is ([#507](https://github.com/mrpmorris/Fluxor/issues/507)). Ducky's generator emits the registration at build time, and its libraries are built with `IsAotCompatible` and checked by a Native AOT smoke test and a trimmed WebAssembly test ([SPEC §10](../spec/SPEC.md#10-serialization-and-aot-core-07-eng-08-inv-23-inv-24)).

[!code-csharp[](samples/fluxor/After.Tests/BehaviourChangeTests.cs#generated-registration)]

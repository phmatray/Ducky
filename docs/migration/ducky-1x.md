# Migrating from Ducky 1.x

This guide takes an app from Ducky 1.0.292, the last published 1.x (`Ducky`, `Ducky.Blazor` and `Ducky.Generator` on nuget.org), to Ducky 2.0. Ducky 2.0 is a rewrite: the concepts carry over (slices, actions, effects, selectors, `DuckyComponent<TState>`), most type names do not.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/ducky-1x`: `Before` builds against the 1.0.292 packages, `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests and one test per behaviour change below.

## Checklist

1. Update the packages: `Ducky` and `Ducky.Blazor` to 2.0, add `Ducky.Testing` to test projects, and remove `Ducky.Generator`. Its generators now ship inside `Ducky`; its final 2.0.0 package only raises DUCKY900 to say so.
2. Fix each DUCKYM error (see [Tombstones](#tombstones-duckym001-to-duckym008)). Each one names the 2.0 replacement and links its page.
3. Add a `JsonSerializerContext` and pass it with `UseJson(AppJson.Default)`. It may start empty: [DUCKY006](../diagnostics/DUCKY006.md) then lists every `[DuckyAction]` and slice state type missing from it, and [DUCKY306](../diagnostics/DUCKY306.md) at startup names any persisted or prerendered state type still missing.

   [!code-csharp[](samples/ducky-1x/After/AppJson.cs#after-json)]

4. Replace the registration chain (see [Registration](#registration)).
5. Run the app's tests, then read [Behaviour changes](#behaviour-changes): they compile unchanged and behave differently.

## Before and after

### State, actions and reducers

[!code-csharp[](samples/ducky-1x/Before/Counter.cs#before-counter)]

[!code-csharp[](samples/ducky-1x/After/Counter.cs#after-counter)]

`[DuckyAction]` is now `Ducky.DuckyActionAttribute` (`using Ducky;` replaces `global using Ducky.Generators;`), and its dispatch helpers are `{Record}DispatchExtensions` in the record's namespace, on anything that dispatches: `IStore`, an effect's `EffectContext`, a `TestStore`.

### Effects

[!code-csharp[](samples/ducky-1x/Before/Counter.cs#before-effect)]

[!code-csharp[](samples/ducky-1x/After/Counter.cs#after-effect)]

An effect takes its services through its constructor and gets a `CancellationToken`; `ctx.Run` covers the started/succeeded/failed triad, and `Policy` picks Merge, Switch, Exhaust or Queue (see [Effects](../guides/core/effects.md)).

### Selectors

[!code-csharp[](samples/ducky-1x/Before/Counter.cs#before-selector)]

[!code-csharp[](samples/ducky-1x/After/Counter.cs#after-selector)]

### Components

[!code-csharp[](samples/ducky-1x/Before/CounterView.cs#before-component)]

[!code-csharp[](samples/ducky-1x/After/CounterView.cs#after-component)]

### Registration

[!code-csharp[](samples/ducky-1x/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/ducky-1x/After/Registration.cs#after-registration)]

### Reading state

[!code-csharp[](samples/ducky-1x/Before/Registration.cs#before-read)]

[!code-csharp[](samples/ducky-1x/After/Registration.cs#after-read)]

## Tombstones (DUCKYM001 to DUCKYM008)

A 1.x type that user code derives from, implements or calls statically is kept as a member-less interface in its 1.x namespace, marked obsolete as an error. The compiler then reports its DUCKYM code on each use, with the replacement in the message. The tombstones are removed in 3.0.

| ID | 1.x type (namespace) | Ducky 2.0 |
|---|---|---|
| [DUCKYM001](../diagnostics/DUCKYM001.md) | `IState`, `IKeyedAction` (`Ducky`) | delete the base: any object is an action or a state |
| [DUCKYM002](../diagnostics/DUCKYM002.md) | `SliceReducers<TState>`, `ISlice<TState>` (`Ducky`) | `sealed class X : Slice<TState>` with `protected override TState Initial =>` |
| [DUCKYM003](../diagnostics/DUCKYM003.md) | `AsyncEffect<TAction>`, `IAsyncEffect` (`Ducky`) | `Effect<TAction>`: constructor injection, `Handle(action, EffectContext, CancellationToken)`, `ctx.Dispatch`, `ctx.State` |
| [DUCKYM004](../diagnostics/DUCKYM004.md) | `IReactiveEffect` (`Ducky`) | Ducky.Reactive's `ReactiveEffect` (R3 `Observable<object>`) |
| [DUCKYM005](../diagnostics/DUCKYM005.md) | `MemoizedSelector` (`Ducky`) | `Selector.Define(...)` + `def.Create()` |
| [DUCKYM006](../diagnostics/DUCKYM006.md) | `Fsa`, `Fsa<TPayload>`, `Fsa<TPayload, TMeta>`, `FsaError`, `FsaError<TMeta>`, `FsaMeta<TMeta>` (`Ducky`) | a plain `record` (no base type) |
| [DUCKYM007](../diagnostics/DUCKYM007.md) | `NormalizedState<TKey, TEntity, TState>` (`Ducky`) | an `EntityState<TKey, TEntity>` member of the state record |
| [DUCKYM008](../diagnostics/DUCKYM008.md) | `DuckyLayout<TState>` (`Ducky.Blazor`) | the non-generic `DuckyLayout` + `Select` in `OnInitialized` |

## Mapping table

Every other public type of Ducky 1.0.292 and Ducky.Blazor 1.0.292 (`docs/migration/ducky-1x-api.txt`), then the APIs that changed shape. The `Docs` build fails when a listed type has neither a tombstone nor a row here. Rows and sections marked (dev) cover APIs of the unreleased 1.x `dev` branch, absent from 1.0.292.

| 1.x | Ducky 2.0 |
|---|---|
| `IDispatcher`, `Dispatcher` (`Ducky`) | inject `IStore`, which is an `IDispatcher` (`Dispatch`, `DispatchAsync`); the dispatcher itself is internal |
| `IDispatcher.ActionStream` | a Ducky.Reactive `ReactiveEffect` (its `actions` observable) |
| `IDispatcher.LastAction` | `TestStore.Processed` in tests, or a `Middleware` in the app |
| `DuckyStore`, `DuckyStoreFactory`, `ObservableSlices` (`Ducky`) | inject `IStore`; `AddDucky` builds it. `RootStateObservable` becomes `Store.Select(selector, onChange)` |
| `Store.StateChanged +=` (dev) | `Store.Select(s => s, onChange)`; dispose the `Selection` to unsubscribe |
| `IRootState`, `RootState` (`Ducky`) | `StateSnapshot` (`store.State`) |
| `IRootState.GetSliceState<T>()`, `GetSliceState<T>(key)`; (dev) `store.GetSlice<T>()`, `GetSliceByKey`, `TryGetSlice`, `GetSliceKeys` | `State.Get<T>()`, `State.Get(key)`, `State.TryGet<T>(out …)`, `State.Keys` |
| `IRootStateSerializer`, `RootStateSerializer` (`Ducky`) | `IStore.Json` (`DuckyJson`), over the context passed to `UseJson` |
| `ISlice` (`Ducky`) | `Slice`, the non-generic base of `Slice<TState>` (`IStore.Slices`) |
| `DuckyComponent<TState>` (`Ducky.Blazor`) | same name and arity; `State` is one whole-slice selection |
| `DuckyComponent<TState>.Dispatcher.X(...)` | `Store.X(...)`: the generated helpers work on `IStore`, and components have no `Dispatcher` property |
| `protected override void OnAfterSubscribed()` | `OnInitialized()` after the `Select` calls (2.0 doesn't seal it) |
| `Ducky.DependencyInjections` (`AddDuckyCore(DuckyOptions)`), `Ducky.Blazor.DependencyInjections` (`AddDucky(IConfiguration?, Action<DuckyOptions>?)`), `DuckyOptions` | `AddDucky(d => …)`. Same name, incompatible signature, so the compiler reports CS1503; binding from the `Ducky` configuration section is removed |
| `DuckyOptions.AssemblyNames`, implicit scan; (dev) `ScanAssemblies` | `AddDuckyGenerated_{Asm}()`, generated per assembly |
| `AddDuckyBlazor`, `BlazorDuckyBuilder` (dev) | `AddDucky(d => d.AddBlazor())` |
| `StoreInitialized` (`Ducky`) | same name, a plain record with no `TypeKey`; an `Effect<StoreInitialized>` does the initial load |
| `ReactiveEffect` (`Ducky`), `CustomOperators`, `ReactiveSelectorExtensions`, `StateActionPair<TState, TAction>` | install Ducky.Reactive: `ReactiveEffect.Handle(Observable<object> actions, Observable<StateSnapshot> state)`, `actions.OfActionType<T>()` and R3's own operators. No tombstone: it would be ambiguous with `Ducky.Reactive.ReactiveEffect` for code importing both namespaces |
| `ObservableExtensions` (`FirstSync`) | `store.State`, or `Selection<T>.Value` |
| `ReactiveEffectTestHelper` | Ducky.Testing + R3's `FakeTimeProvider` |
| `ActionMeta`, `IFsaMeta<TMeta>`, `IFsaPayload<TPayload>` (`Ducky`) | properties of the plain action record, as for `Fsa` (DUCKYM006) |
| `IEntity<TKey>` (`Ducky`) | unchanged: `EntityState<TKey, TEntity>` keys entities by `IEntity<TKey>.Id` |
| `INormalizedStateCollectionMethods<TKey, TEntity, TState>` (`Ducky`) | the methods of `EntityState<TKey, TEntity>`, as for `NormalizedState` (DUCKYM007) |
| `MergeStrategy` (`Ducky`) | same name: `FailIfDuplicate` is unchanged, `Overwrite` becomes `PreferIncoming`, `PreferExisting` is new |
| `ValueCollection<T>`, `EnumerableExtensions` (`ToValueCollection`) (`Ducky`) | `ImmutableArray<T>` or `ImmutableList<T>`: change detection compares references, so value equality buys nothing |
| `StateChange<TState>`, `StateLoggerObserver<TState>`, `LoggerProvider`, `ConsoleFallbackLogger<T>` (`Ducky`) | `ILogger` from DI (Ducky logs through `ILoggerFactory`), `ActivitySource` and `Meter`; a `Middleware` with `AfterReduce` to observe each change |
| `IStoreEventPublisher`, `*EventArgs` (dev) | `ILogger`, `ActivitySource`, `Meter` |
| `DuckyException` (`Ducky`) | `DuckyConfigurationException` (its `Errors` carry DUCKY3xx codes) for configuration errors; failures while running are `ReducerFailed` and `EffectFailed` actions |
| `DuckyVersioning` (`Ducky`) | removed: read `typeof(IStore).Assembly.GetName().Version` |
| `IMiddleware` (`Ducky`) | `Middleware`. No tombstone: it would clash with `Microsoft.AspNetCore.Http.IMiddleware`, which Web SDK implicit usings import |
| `UseDefaultMiddlewares` (dev) | nothing |
| `EnablePersistence(...)` (dev) | `Persist<T>(...)` per slice |
| `EnableDevTools` (dev) | `UseDevTools()` guarded by `IsDevelopment()` |
| `EnableJsLogging` (dev) | removed: use DevTools and `ILogger` |
| `CrossTabSync` (dev) | `o.SyncAcrossTabs = true` |
| `WhenSliceChanges` (dev) | `Select(selector, onChange, comparer)` |
| `IExceptionHandler`, `DuckyErrorBoundary` (dev) | an effect on `ReducerFailed` or `EffectFailed` |
| `IsHydrated` (dev) | `State.Get<PersistenceState>().Status` |
| `StoreInitializer` (dev) | optional `<DuckyInitializer>` |
| `global using Ducky.Generators;` | `using Ducky;` |
| global `ActionDispatcher` | `{Record}DispatchExtensions` in the record's namespace |
| `[DuckyEffect]`, `[DuckyReducer]`, `[InitialState]`, `[DuckyComponent]` (dev) | removed |
| `RouterStoreState`, `RouterReducers`, `RouterCancelAction`, `RouterErrorAction`, `RouterNavigatedAction`, `RouterNavigationAction`, `RouterRequestAction`, `RouterRequestAction.ActionPayload` (`Ducky.Blazor.Router`) | no 2.0 equivalent: removed. Keep routing out of the store and use `NavigationManager` |

## Behaviour changes

These compile unchanged. Each comes with the test that pins it. Sections marked (dev) describe the behaviour of the unreleased 1.x `dev` branch, not 1.0.292.

### Change detection compares references

A reducer that returns a new instance is a change, even when it equals the old one; returning the same instance is not. 1.x compared with `Equals`. [DUCKY011](../diagnostics/DUCKY011.md) flags `s with { }` in a reducer.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#change-detection)]

### Handlers match the exact action type

Reducers and effects see only the exact type they name: an `Effect<TBase>` no longer sees subtypes. [DUCKY005](../diagnostics/DUCKY005.md) rejects an abstract type, an interface or `object`, which could never match.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#exact-type)]

### A synchronous Dispatch does not wait for the reduce

(dev) `Dispatch` reduces before it returns only when the calling thread becomes the drainer. While another thread drains, it queues the action and returns; a 1.x `dev` build blocked until that drain ended (1.0.292 already queued and returned). `await DispatchAsync(...)` completes once the action is reduced. The [threading contract](../threading.md) states the rules.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#sync-dispatch)]

### Reducer exceptions become ReducerFailed

A throwing reducer is never rethrown to the dispatcher: the store dispatches `ReducerFailed`, and the action commits nothing, in any slice.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#reducer-failed)]

### MaxReentrantDepth becomes MaxDispatchDepth

(dev) 1.x bounded the re-entrant queue length. `MaxDispatchDepth` (default 64) bounds the synchronous causal chain: an action dispatched while its parent is processed is one level deeper, and one above the limit is dropped with a `ReducerFailed(DispatchLoopException)`.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#dispatch-depth)]

### Subscribers and effects may run on a thread-pool thread

Whichever thread drains runs the reducers, the `onChange` callbacks and the effects' synchronous part, so after an effect's I/O that is a thread-pool thread. Marshal component work with `InvokeAsync`; `DuckyComponent` does so for its own re-render.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#subscriber-thread)]

### Actions dispatched before init are buffered

An action dispatched while the store initializes (a middleware's `InitializeAsync`, persistence's restore) waits in the init buffer and is reduced once the store is ready.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#init-buffer)]

### Persisted 1.x data is discarded

(dev) 2.0 persists a new envelope (`{"v":…,"at":…,"s":…}`) without assembly-qualified type names, and there is no 1.x reader. A stored 1.x value reads as not found (a Debug log, EventId 2030), so the slice starts from `Initial`, its load effect runs, and the next write replaces the value. Pinned by `Persist_OneXFormat_Discarded` in `test/Ducky.Blazor.Tests/Persistence/EnvelopeTests.cs`, over the 1.x `RootStateSerializer` and typed local-storage formats.

### DevTools options are trimmed

(dev) `Realtime`, `Latency`, `CollapseActions` and the `StackTrace*` options are removed, and `Trace` is off by default.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#devtools-options)]

### UseJson is required for DevTools payloads, persistence and prerender

Ducky never serializes by reflection: DevTools payloads, persisted slices and prerender seeds go through the context passed to `UseJson`. A persisted or prerendered state type it lacks fails at startup with DUCKY306.

[!code-csharp[](samples/ducky-1x/After.Tests/BehaviourChangeTests.cs#use-json)]

# Moving from DuckyNext

DuckyNext was the design lab for this release: a Blazor Server todo app whose `dev` branch mocked, by hand, the code an attribute-driven generator would emit, and whose `features/async-action` branch tried out middleware pipelines, effects, persistence, cross-tab sync and DevTools. It was never published, so there is no package to replace and no compiled before/after sample: this page maps its concepts to the Ducky 2.0 API that took them over. Each row links to the guide that shows the 2.0 side with compiled code.

## Checklist

1. Remove the DuckyNext sources (`Lib/ReduxAttributes.cs`, the hand-written `Lib/Generated.cs` and `Lib/GeneratedComponents.cs`, the pipeline and middleware folders) and reference `Ducky` and `Ducky.Blazor`.
2. Turn each `[ReduxSlice]` member of the `[ReduxRootState]` record into a slice of its own, and each `[ReduxReducers]` class into that slice's `On<T>` handlers.
3. Mark the actions `[DuckyAction]`, and replace each `[ReduxAsyncEffect]` class with an `Effect<T>` that calls `context.Run`.
4. Replace `[assembly: ReduxServices]` and `AddGeneratedRedux()` with `AddDucky(d => d.AddDuckyGenerated_{Assembly}())`, and add `UseJson` with a `JsonSerializerContext`.
5. Read state with `State.Get<T>()`, and move Rx middleware to Ducky.Reactive.

## Mapping table

| DuckyNext | Ducky 2.0 |
|---|---|
| `[ReduxRootState] partial record AppState([ReduxSlice] TodoState Todos, …)` | no root record: each slice state is its own record, held by a `Slice<TState>` with `protected override TState Initial`; the store composes them ([Slices and keys](../guides/core/slices-and-keys.md)) |
| a generated typed root (`store.GetState().Todos`, `IStore<AppState>`) | `store.State.Get<TodoState>()` on the non-generic `IStore`, or `State.Get(key)` by slice key |
| `[ReduxReducers] static partial class TodoReducers` with `private static partial TodoState OnAdd(TodoState state, AddTodoAction action)` | `On<AddTodoAction>((state, action) => …)` in the slice's constructor: one handler per action type, matched on the exact type |
| the generated `Reduce(state, object action)` switch and `AppReducers` composition | nothing: the store routes each action to every slice with a handler for its type |
| `ProfileReducers = true` (`{State}Reducers_Profiling`) | the store's `ActivitySource` and `Meter` |
| `[ReduxAction(typeof(TodoState))] record AddTodoAction(…)` | `[DuckyAction] record AddTodoAction(…)`: an action belongs to no slice, every slice may handle it |
| generated action creators (`AddTodoActionCreator.Create`, `Dispatch(this IStore<AppState>, …)`) | the generated `{Record}DispatchExtensions` (`store.AddTodoAction(…)`) on `IStore`, an effect's `EffectContext` and a `TestStore` |
| `AsFluxStandardAction()` | removed: actions are plain records; DevTools and persistence serialize through `UseJson` |
| `[ReduxAsyncEffect(AsyncEffectKind.Fetch)]` (the generated `Xxx`/`XxxSuccess`/`XxxError` triad around a `XxxCore` partial method) | an `Effect<T>` that injects its services and calls `context.Run(started, work, succeeded, failed)`, with a concurrency `Policy` ([Effects](../guides/core/effects.md)) |
| `[ReduxSelector]` properties (generated `GetTodosItems()`) | `Select(s => s.Get<TodoState>().Items)` in a component, or a memoized `Selector.Define(...)` ([Selectors](../guides/core/selectors.md)) |
| `GenerateComponentBases` (`TodoStateComponent`) | `DuckyComponent<TodoState>`, or `DuckyComponent` with `Select` calls in `OnInitialized` |
| `GenerateEquality` (`IEquatable<T>`, `With(...)`) | records; change detection compares references, so equality is never needed |
| `Persistable = true` (generated JSON converters, hydration from localStorage, `HydrationManager`) | `Persist<TSlice>(…)` or `[Persist]` per slice, over the `JsonSerializerContext` passed to `UseJson`; actions dispatched during the restore are buffered |
| `[assembly: ReduxServices]`, `AddGeneratedRedux()` | `AddDucky(d => d.AddDuckyGenerated_{Assembly}())` ([Getting started](../guides/core/getting-started.md)) |
| `[ReduxReducersTests("…")]` (generated test stubs) | `SliceTest.For<TSlice, TState>().Given(…).When(…).Then(…)` from Ducky.Testing ([Testing](../guides/core/testing.md)) |
| `IReduxMiddleware` (onion `next`, or the phased Before/After pipeline with `ActionContext`) | a `Middleware` with synchronous `MayDispatch`, `BeforeReduce` and `AfterReduce` over an `ActionContext`, registered with `d.Use<TMiddleware>()`; async work goes in an effect |
| `IPipelineEventPublisher` (Started/Completed/Errored/Aborted) | `ILogger`, `ActivitySource` and `Meter`; failures become `ReducerFailed` and `EffectFailed` actions |
| `RxMiddleware` epics (System.Reactive: an action stream mapped to new actions) | Ducky.Reactive's `ReactiveEffect`: `Handle(Observable<object> actions, Observable<StateSnapshot> state)` returns the actions to dispatch, on R3; filter with `actions.OfActionType<T>()`; register with `AddReactiveEffect<T>()` |
| `AsyncEffectRetry` (Polly), `DebounceMiddleware`, `RetryCircuitBreakerMiddleware` | inside the effect: retry around the `work` passed to `context.Run`, `Switch` with a `context.Time` delay for a debounce, or the matching R3 operators in a `ReactiveEffect` |
| `CorrelationId` middleware | the `ducky.dispatch` activity the store emits per action, tagged `ducky.correlation_id` |
| `DevTools` middleware (time travel) | `UseDevTools()` guarded by `IsDevelopment()` |
| `CrossTabSyncModule` | `o.SyncAcrossTabs = true` on a persisted slice |
| `JsLogging` middleware, `JsModule` base | removed: DevTools and `ILogger` |
| `CodeGen.Core`, `CodeGen.Cli` (the SyntaxFactory generator playground) | the generators shipped inside the `Ducky` package |

## What changes in behaviour

- DuckyNext's `dev` store found reducers by reflection and ran its middleware sync-over-async. Ducky 2.0 has no reflection activation, and its middleware hooks are synchronous; anything that awaits is an effect.
- The reducer for an action ran in the slice named by `[ReduxAction(typeof(…))]`. In Ducky 2.0 an action reaches every slice that declares a handler for its exact type.
- A reducer that returns a new instance is a change even when it is equal to the old one, so a reducer returns its input to mean "no change" (DUCKY011 flags `s with { }`).

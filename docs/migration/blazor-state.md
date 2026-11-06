# Moving from the blazor-state monorepo

`blazor-state` gathered four Blazor state libraries side by side, plus Mutty and two samples; `blazor-state-experiments` archives seven earlier prototypes. Both are concept sources for Ducky 2.0, not packages to replace: each library maps to the Ducky 1.x, Bustand, TheBlazorState, BlazorMVU or Mutty migration guide, and the experiments map to the 2.0 feature that took their idea over.

## Checklist

1. Find the library each app uses in the table below, and follow its guide: the libraries in the monorepo are the same code as their standalone repositories.
2. Reference `Ducky` and `Ducky.Blazor` (and `Ducky.Reactive` or `Ducky.Draft` where the table says so), and remove the monorepo's project references.
3. Register the slices with `AddDucky(d => d.AddDuckyGenerated_{Assembly}())`, and pass a `JsonSerializerContext` with `UseJson`.
4. Run the app's tests.

## The monorepo's libraries

| blazor-state | Ducky 2.0 | Guide |
|---|---|---|
| `libs/ducky` (Ducky 1.x) | Ducky 2.0 | [Migrating from Ducky 1.x](ducky-1x.md) |
| `libs/bustand` (Bustand: `ZustandStore<TState>`, `Set`, `UseState`) | `SliceStore<TState>`, with an `Effect` for work that awaits a service | the Bustand guide (`bustand.md`) and [SliceStore](../guides/core/slice-store.md) |
| `libs/the-blazor-state` (TheBlazorState: `[Persist]` and `[Shared]` properties) | a slice field plus `Persist<TSlice>(…)`; a `[Shared]` POCO becomes a `SliceStore<TState>`; `LoadFrom` becomes an `Effect<StoreInitialized>` | the TheBlazorState guide (`theblazorstate.md`) |
| `libs/mvu` (BlazorMVU: `Init`, `Update`, `Cmd`, `Sub`) | `Initial`, one `On<Msg>` per case, an `Effect<Msg>` per command, a `ReactiveEffect` or a `LongRunning` effect per subscription | the BlazorMVU guide (`blazormvu.md`) |
| `utils/mutty` (Mutty) | Ducky.Draft | [Migrating from Mutty](mutty.md) |
| `samples/mvvm-todo` (CommunityToolkit MVVM) | no state library to replace: keep it, or move its view models' state into slices | [Getting started](../guides/core/getting-started.md) |
| `samples/reactive-patterns` (Rx, object calisthenics) | Rx pipelines over actions become a Ducky.Reactive `ReactiveEffect` on R3 | – |

## The experiments

| blazor-state-experiments | Ducky 2.0 |
|---|---|
| NotRedux (a per-circuit Redux store with .NET reducers and effects) | the store itself: slices, `On<T>` handlers and `Effect<T>`, scoped per circuit on Blazor Server |
| ReactiveStore (an Rx `BehaviorSubject` store, `ReducerBuilder.AddCase`, `FluxComponent<TState>`) | `Slice<TState>` with `On<T>`, and `DuckyComponent<TState>` |
| ReactiveBlazorKit (`ReactiveComponentBase`, `BindTo`, `ReactiveStore<TState>`) | `DuckyComponent` with `Select(selector)` in `OnInitialized` (its selections are disposed with the component), and Ducky.Reactive for Rx pipelines |
| ReduxInterop / ReduxNET (redux.js hosted in JS, a .NET reducer called through interop) | a .NET store; Redux DevTools through `UseDevTools()`, persistence through `Persist<T>`, cross-tab sync through `SyncAcrossTabs` |
| R3dux.DebugTest (mutable R3 `ReactiveProperty` slices, reflection-based undo) | immutable slice records; Redux DevTools for history |
| FluxState (a finite state machine with guards) | no equivalent: model the machine's state as a slice and its transitions as `On<T>` handlers |
| RxDDD.Domain (observable repositories) | no equivalent: load data in an `Effect` and reduce the result into a slice |

## What changes in behaviour

- One store holds every slice, instead of one store per library or per component: a component reads only what it selects, and re-renders only when that selection changes.
- Every state change goes through an action and a reducer, including the `Set` calls of a `SliceStore`, so updates are never lost when two run at once.
- Persisted data from these libraries is not read back: each slice starts from its `Initial` value, and the next write stores the 2.0 format.

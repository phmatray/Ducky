# Migrating from a hand-written state container

Many Blazor apps keep shared state in a service of their own: the [in-memory state container](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-10.0#in-memory-state-container-service) of Microsoft's Blazor docs, a class with properties, methods that change them and an `OnChange` event that components subscribe to. On .NET 10 the same service can carry its values from prerendering into the interactive render with [`[PersistentState]` and `RegisterPersistentService`](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0#serialize-state-for-services).

That pattern needs no library and is often enough. This guide is for the container that has grown: several components selecting from it, loads that overlap, changes from background threads, a prerendered page that loads twice. It moves the container to a Ducky 2.0 `SliceStore<TState>`, which keeps the shape of the service (methods that change state) and adds a store underneath.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/state-container`. `Before` uses ASP.NET Core alone, as Microsoft's docs describe it. `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests and one test per behaviour change below.

## Checklist

1. Target `net10.0`. Add `Ducky` and `Ducky.Blazor`, and `Ducky.Testing` to test projects.
2. Gather the container's properties into one immutable record and derive the container from `SliceStore<TState>`; each method that changes state calls `Set` (see [The container becomes a SliceStore](#the-container-becomes-a-slicestore)).
3. Move each method that awaits a service into an effect that injects the service and the SliceStore, triggered by an action: callers of `LoadAsync()` now dispatch `LoadCount` (see [Async methods become effects](#async-methods-become-effects)).
4. Move the first load into an effect on `StoreInitialized` that returns early when the slice was restored (the load rule), and register the slice with `Prerender<TSlice, TState>(include)`, whose predicate seeds only a loaded state (see [Registration](#registration)).
5. In components, inherit `DuckyComponent`, `Select` in `OnInitialized`, and delete the `OnChange` subscription, the `InvokeAsync` wrapper and `IDisposable` (see [Components](#components)). In `_Imports.razor` and the `.razor` files that means `@using Ducky`, `@using Ducky.Blazor` and `@inherits DuckyComponent`; `@inject CounterStore` stays, and `@implements IDisposable` goes.
6. Replace the container's `AddScoped` or `AddSingleton`, and `RegisterPersistentService`, with `AddDucky` (see [Registration](#registration)).
7. Run the app's tests, then read [Behaviour changes](#behaviour-changes).

## Before and after

### The container becomes a SliceStore

[!code-csharp[](samples/state-container/Before/CounterStore.cs#before-store)]

[!code-csharp[](samples/state-container/After/CounterStore.cs#after-store)]

Callers keep calling `Increment(1)`. The `Set` mutator runs when the store reduces the change, against the latest state, so two concurrent calls can't overwrite each other, and the store notifies whoever selected a changed value. `SetAsync` returns a `Task<DispatchResult>` that completes once the change is reduced. `State` is a point-in-time read, not a subscription (see [SliceStore](../guides/core/slice-store.md)).

### Async methods become effects

A SliceStore has a parameterless constructor and never awaits. `LoadAsync` becomes an effect on a `LoadCount` action, which the SliceStore also reduces to set `IsLoading`:

[!code-csharp[](samples/state-container/After/CounterStore.cs#after-effect)]

The effect gets a cancellation token and a concurrency policy; `LoadCountOnStart` is the [load rule](../guides/blazor/render-modes.md#one-load-rule-in-every-mode), which loads once the store is initialized unless the prerender seed restored the slice. The tests let the store build the effect, so it injects the store's own `CounterStore` (see [Testing](../guides/core/testing.md)):

[!code-csharp[](samples/state-container/After.Tests/CounterStoreTests.cs#after-test)]

### Components

[!code-csharp[](samples/state-container/Before/CounterView.cs#before-component)]

[!code-csharp[](samples/state-container/After/CounterView.cs#after-component)]

### Registration

[!code-csharp[](samples/state-container/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/state-container/After/Registration.cs#after-registration)]

`AddDuckyGenerated_{Assembly}()` registers every slice, SliceStore and effect the assembly declares, and `CounterStore` is injectable as the store-owned instance. `AppJson` is a `JsonSerializerContext` listing `CounterState`: the prerender seed is serialized through it, never by reflection.

## What .NET 10 persistent state covers, and where Ducky goes further

`[PersistentState]` on a service property, registered with `RegisterPersistentService<T>(RenderMode)`, serializes the property during prerendering and restores it when the interactive render mode resolves the service. In a `.Client` project the service must be a singleton. It carries values into the interactive render, and on Blazor Server across a paused and resumed circuit; it doesn't write to browser storage, follow other tabs or coordinate async work.

| Need | .NET 10, no library | Ducky 2.0 |
|---|---|---|
| Prerendered values reach the interactive render | `[PersistentState]` + `RegisterPersistentService` ([docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0#serialize-state-for-services)) | `Prerender<TSlice>()`, written once the prerender store is idle ([Prerender](../guides/blazor/prerender.md)) |
| A failed prerender load is retried in the browser | your own check | the `include` predicate leaves the slice out of the seed |
| Serialization | System.Text.Json with default settings, [not trimmer safe](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0) without preserving the types; a [`PersistentComponentStateSerializer<T>`](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0#serialization-extensibility-for-persistent-component-state) replaces it per type | your `JsonSerializerContext`, source-generated |
| Survives a reload, per browser or per user | no ([docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-10.0#in-memory-state-container-service): "The example doesn't persist data without further development") | `Persist<TSlice>()`: `localStorage`, `sessionStorage` or `IDistributedCache` ([Persistence](../guides/blazor/persistence.md)) |
| Follows the user's other tabs | no | `SyncAcrossTabs` ([Cross-tab sync](../guides/blazor/cross-tab.md)) |
| Changes from other threads | each component wraps `StateHasChanged` in `InvokeAsync` ([docs](https://learn.microsoft.com/en-us/aspnet/core/blazor/state-management/?view=aspnetcore-10.0#support-state-modifications-from-outside-blazors-synchronization-context)) | Ducky's components marshal their own re-render |
| Overlapping async loads | your own cancellation | effect policies: `Switch`, `Exhaust`, `Queue`, `Merge` |
| History and time travel | no | Redux DevTools (`UseDevTools()`) |

Keep `[PersistentState]` for component state that isn't in the store.

## Mapping table

| Hand-written container | Ducky 2.0 |
|---|---|
| a service class with mutable properties | a `SliceStore<TState>` over one immutable `record` |
| the initial values of the fields | `protected override TState Initial` |
| a method that sets properties and raises `OnChange` | a method that calls `Set(state => state with { ... })` |
| an `async` method that awaits a service, then sets properties | an `Effect<TAction>` that injects the service and the SliceStore and calls its methods |
| `event Action OnChange` | nothing: selections are notified when their value changes |
| `OnChange += StateHasChanged` in `OnInitialized`, `-=` in `Dispose` | `Select(...)` in `OnInitialized` of a `DuckyComponent`; it disposes its selections |
| `InvokeAsync(StateHasChanged)` for changes from other threads | nothing: `DuckyComponent` marshals its re-render; your own `onChange` callbacks still use `InvokeAsync` |
| a service subscriber outside components (`OnChange += ...`) | `store.Select(selector, onChange)`; dispose the `Selection` to unsubscribe |
| `AddSingleton<T>()` in WebAssembly, `AddScoped<T>()` on the server | `AddDucky(...)`: the store is a singleton in the browser and scoped on the server |
| `[PersistentState]` on service properties + `RegisterPersistentService<T>(RenderMode)` | `Prerender<TSlice>()` |
| `localStorage` through your own JS interop | `Persist<TSlice>()` |
| `CascadingValueSource<T>` notifications | a `Select` in each consuming component |

## Behaviour changes

Each comes with the test that pins it.

### No lost updates

`Count += amount` on a plain property is a read followed by a write; two threads can read the same value and one increment is lost, unless the container takes a lock. A SliceStore's mutators run one at a time when the store reduces them, each against the latest state:

[!code-csharp[](samples/state-container/After.Tests/BehaviourChangeTests.cs#no-lost-updates)]

### Changes from other threads need no InvokeAsync

The container raises `OnChange` on the thread that changed it, so each component must marshal with `InvokeAsync`, or Blazor throws "The current thread is not associated with the Dispatcher". `DuckyComponent` marshals its own re-render to the renderer:

[!code-csharp[](samples/state-container/After.Tests/BehaviourChangeTests.cs#off-thread-change)]

### Components re-render only for the values they selected

Every `OnChange` subscriber re-renders on every change of the container. A Ducky component re-renders when a value it selected changed:

[!code-csharp[](samples/state-container/After.Tests/BehaviourChangeTests.cs#selected-rerender)]

### A restored slice is not loaded again

A component that loads in `OnInitializedAsync` loads twice on a prerendered page unless it checks what was restored. With `Prerender<T>` and the load rule, a slice the seed restored reads as restored, and its load effect returns early:

[!code-csharp[](samples/state-container/After.Tests/BehaviourChangeTests.cs#load-rule)]

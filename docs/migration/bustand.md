# Migrating from Bustand

Bustand's Zustand-style stores map to Ducky 2.0's `SliceStore<TState>`. For a store without injected services the move is a rename. A store that injects a service, or has a method that awaits I/O and then sets state, needs one extra piece: an effect.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/bustand`. `Before` builds against Bustand: Bustand was never published to nuget.org, so the sample uses a verbatim copy of its source in `docs/migration/samples/legacy/Bustand`. `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests.

## Checklist

1. Replace the Bustand packages with `Ducky` and `Ducky.Blazor`, and add `Ducky.Testing` to test projects.
2. Derive each store from `SliceStore<TState>` and rename `InitialState` to `Initial`.
3. Move constructor-injected services, `InitializeAsync` and every store method that awaits a service into effects (see [The load becomes an effect](#the-load-becomes-an-effect)).
4. Replace `UseState` with `Select` in `OnInitialized` (see [Components](#components)).
5. Replace `AddBustand` with `AddDucky` (see [Registration](#registration)), then run the app's tests.

## Before and after

### The store

[!code-csharp[](samples/bustand/Before/UserStore.cs#before-store)]

[!code-csharp[](samples/bustand/After/UserStore.cs#after-store)]

`Set` and `SetAsync` keep their names and stay `protected`, as in Bustand. Both take a mutator: `Set(Func)` is unchanged, and `Set(TState)` and `SetAsync(TState)` become `Set(_ => value)` and `SetAsync(_ => value)`. The mutator runs when the store reduces the change, against the latest state, so concurrent changes no longer overwrite each other (Bustand's lost updates). `SetAsync` returns `Task<DispatchResult>` and completes once the change is reduced: awaiting it gives read-your-writes. A SliceStore is still a slice, so it can also react to actions with `On<T>` (see [SliceStore](../guides/core/slice-store.md)).

### The load becomes an effect

A SliceStore has a parameterless constructor and never awaits. `InitializeAsync`, a constructor-injected service, or a store method that awaits a service and then sets state, becomes an effect. The effect injects the service and the SliceStore, awaits the work with its run token, then calls a public SliceStore method: here `SetUserAsync`, which wraps `SetAsync`. An `Effect<StoreInitialized>` runs once, when the store is ready; work started by a user action is an `Effect<TAction>` on that action, with a concurrency policy (Switch, Exhaust) Bustand's methods never had.

[!code-csharp[](samples/bustand/After/UserStore.cs#after-effect)]

The effect's test lets the store build it, so it injects the store's own `UserStore`; `TestStore.GetSlice<UserStore>()` reads that instance (see [Testing](../guides/core/testing.md)):

[!code-csharp[](samples/bustand/After.Tests/UserStoreTests.cs#after-test)]

### Components

[!code-csharp[](samples/bustand/Before/UserBadge.cs#before-component)]

[!code-csharp[](samples/bustand/After/UserBadge.cs#after-component)]

`SliceStore.State` is a point-in-time read, not a subscription. Markup that must follow changes (the load, a restore from storage, changes made by other components) reads through a `Select` in `OnInitialized` or `<DuckySelect>`; mutations keep calling the SliceStore's methods.

### Registration

[!code-csharp[](samples/bustand/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/bustand/After/Registration.cs#after-registration)]

## Mapping table

| Bustand | Ducky 2.0 |
|---|---|
| `ZustandStore<TState>`, `InitialState` | `SliceStore<TState>`, `Initial` |
| `IStore<TState>` | inject the SliceStore itself (`AddSlice<T>()` registers it, resolved to the instance the store owns) |
| `Set(Func)` / `Set(TState)` | `Set(Func)` unchanged / `Set(_ => value)` |
| `SetAsync(Func)` / `SetAsync(TState)` | `SetAsync(Func)` (returns `Task<DispatchResult>` and completes after the reduce) / `SetAsync(_ => value)`; both are `protected`, as in Bustand |
| constructor-injected services, an overridden `InitializeAsync`, or a store method that awaits a service and then sets state | an `Effect` (an `Effect<StoreInitialized>` for the initial load) that injects the service and the SliceStore, awaits the work with its run token and calls a public SliceStore method wrapping `Set`, or returning `SetAsync`'s task for read-your-writes, like `SetUserAsync` above |
| `Subscribe(Action)`, `Subscribe<TSlice>(selector, callback)`, the `StateChanged` event | `store.Select<TState, T>(selector, onChange)`; dispose the `Selection` to unsubscribe. `onChange` runs on the draining thread |
| `IsInitialized` | `State.Get<PersistenceState>().Status`, or an effect on `StoreInitialized` |
| an `OnStateChanged()` override | an `onChange` callback, or a `Middleware` with `AfterReduce` |
| `IMiddleware<TState>` / `IAsyncMiddleware<TState>` | `Middleware` / an `Effect`: middleware hooks are synchronous |
| `ZustandComponent<TStore,TState>.UseState(sel)`, `UseStateResult<T>` | `DuckyComponent.Select(sel)` in `OnInitialized` (or `<DuckySelect>`), `Selection<T>` |
| `ZustandComponentScoped<TStore,TState>`, `ZustandScope` | `DuckyComponent` over the app store; 2.0 has no component-scoped store |
| `[BustandStore]` + Scrutor | the generated `AddDuckyGenerated_{Asm}()` |
| `[Persist(StorageType)]` | `[Persist(Storage = PersistStorage.Session)]` (or `Local`), or `Persist<T>(...)` in the registration |
| Bustand.DevTools | Redux DevTools (`UseDevTools()`) |
| `BustandCircuitHandler` | nothing: interactivity is automatic |

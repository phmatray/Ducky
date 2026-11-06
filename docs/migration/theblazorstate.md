# Migrating from TheBlazorState

TheBlazorState keeps state in generated partial properties: `[Persist]` on a component property, `[Shared]` on a property of a POCO in DI. In Ducky 2.0 that state moves into the store: a persisted property becomes a field of a slice state, a shared POCO a `SliceStore<TState>`, and the configuration moves from `ConfigureState` to the registration.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/theblazorstate`. `Before` builds against TheBlazorState and its generators: TheBlazorState was never published to nuget.org, so the sample uses a verbatim copy of its source in `docs/migration/samples/legacy`. `After` is the same app on Ducky 2.0, and `After.Tests` holds its tests.

## Checklist

1. Replace TheBlazorState with `Ducky` and `Ducky.Blazor`, and add `Ducky.Testing` to test projects.
2. Move each `[Persist]` property into a slice state, and register `Persist<TSlice>(...)` for it (see [Persisted properties](#persisted-properties)).
3. Turn each `LoadFrom` factory into an `Effect<StoreInitialized>` that checks `WasRestored<TState>()`.
4. Turn each `[Shared]` POCO into a `SliceStore<TState>` (see [Shared state](#shared-state)).
5. Add a `JsonSerializerContext` with the persisted state types and pass it with `UseJson(AppJson.Default)`: persistence and the prerender handoff serialize through it.
6. Replace `AddTheBlazorState` with `AddDucky` (see [Registration](#registration)), then run the app's tests.

## Before and after

### Persisted properties

[!code-csharp[](samples/theblazorstate/Before/Dashboard.cs#before-persist)]

[!code-csharp[](samples/theblazorstate/After/Dashboard.cs#after-persist)]

`LoadFrom` becomes an effect on `StoreInitialized`. It runs once, when the store is ready, after storage was read, and loads only when nothing was restored:

[!code-csharp[](samples/theblazorstate/After/Dashboard.cs#after-load)]

The component reads the slice; `StateMeta.WasRestored` becomes `State.WasRestored<T>()`:

[!code-csharp[](samples/theblazorstate/After/Dashboard.cs#after-component)]

### Shared state

[!code-csharp[](samples/theblazorstate/Before/AppState.cs#before-shared)]

[!code-csharp[](samples/theblazorstate/After/AppStore.cs#after-shared)]

The injected `AppStore` is the store's own slice, so every component shares it. Its `State` is a point-in-time read: markup that must follow changes selects it (`Select((AppState s) => s.UserName)` in a `DuckyComponent`'s `OnInitialized`, or `<DuckySelect>`). See [SliceStore](../guides/core/slice-store.md).

### Registration

[!code-csharp[](samples/theblazorstate/Before/Registration.cs#before-registration)]

[!code-csharp[](samples/theblazorstate/After/Registration.cs#after-registration)]

`BlazorOptions.Scope` replaces `KeySuffix`. It applies to every persisted key, and it is resolved again when the user changes instead of being frozen when the component initializes. A `null` scope means no reads and no writes for those keys, never a shared key.

## Mapping table

| TheBlazorState | Ducky 2.0 |
|---|---|
| `[Persist(TimeToLive = ttl)] partial` property of a component, with a `LocalStorage()`, `SessionStorage()` or `ServerMemoryCache()` strategy | a field of a slice state, plus `Persist<TSlice>(o => o.MaxAge = ttl)` and `Prerender<TSlice>()` (TheBlazorState always ran the prerender handoff too) |
| `[Persist]` property on the default strategy (`DefaultStorage` is `StorageStrategy.PrerenderHtml()`) | a field of a slice state, plus `Prerender<TSlice>()` alone: nothing was ever written to browser storage |
| `[Shared]` POCO | a `SliceStore<TState>` whose setters are `Set` methods |
| `ConfigureState` + `LoadFrom(factory)` | an `Effect<StoreInitialized>` that loads when `context.State.WasRestored<TState>()` is false |
| `KeySuffix(...)` | `BlazorOptions.Scope` (re-resolved on user change, not frozen) |
| `StateMeta.WasRestored` | `State.WasRestored<T>()` |
| `StorageStrategy.LocalStorage()`, `SessionStorage()`, `ServerMemoryCache()` | `PersistOptions.Storage = PersistStorage.Local`, `Session` or `Server` |
| `StorageStrategy.PrerenderHtml()`, the `PersistentComponentState` bridge | `Prerender<TSlice>()` |
| `CrossTabHub` | no equivalent (rejected: it could leak state across users) |
| `StorageStrategy.IndexedDb()` (`IndexedDbStrategy`) | not in 2.0; it comes in 2.1 |
| `AddTheBlazorState()` | `AddDucky(d => d.AddBlazor())` |

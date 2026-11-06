# SliceStore

A `SliceStore<TState>` is a slice with methods: components and effects call `cart.Add(line)` instead of dispatching an action. It is still a slice, with a key, reducers and every pipeline step (middleware, persistence, DevTools, effects).

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SliceStoreGuideTests.cs#slice-store)]

- `Set(mutator)` dispatches the change through the store. The mutator runs when the change is reduced, against the latest state, so concurrent calls never lose an update. Like a reducer, it must be pure, and returning the same reference commits nothing.
- DevTools names the change `{key}/{method}`: `cart/Add`.
- `SetAsync(mutator)` returns the `DispatchResult`, once the change is reduced.
- `State` is a point-in-time read, not a subscription. To follow changes, select.

## Use it

`AddSlice<CartStore>()` (or `AddDuckyGenerated_{Assembly}()`) registers `CartStore` as the store-owned instance, so you inject it:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SliceStoreGuideTests.cs#slice-store-use)]

In a Blazor component, inherit `DuckyComponent`, `@inject CartStore Cart`, select in `OnInitialized` and call `Cart.Add(...)` from event handlers. A component that only renders `Cart.Count` re-renders after its own events only, and misses changes made elsewhere.

## Work that needs a service

A SliceStore has a parameterless constructor and never awaits. Loading, saving and calling APIs belong in an effect that injects the service and the SliceStore:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SliceStoreGuideTests.cs#slice-store-effect)]

An `Effect<StoreInitialized>` runs once, when the store is initialized. Inside an effect run, `Set` follows the run's rules: once a `Switch` run is superseded, its `Set` calls are dropped, as its `context.Dispatch` calls are.

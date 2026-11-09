# Step 8: Keep the basket across reloads

## What you will build in this step

The basket is saved to the browser's `localStorage` after each change and restored when the page loads again. It also stays the same in every open tab of the shop. On the way, you give the saved data a version, so a later release can change its shape without losing your users' baskets.

## Why

Until now, a reload empties the basket: the store lives in the page's memory. Ducky can **persist** a slice: write it to storage when it changes, and **restore** it, put it back in the state, when the next store starts. You declare which slices persist; components and reducers don't change at all.

## Pin the slice's key

The storage entry is named after the slice's key. A key derived from the class name changes if you rename the class, and saved baskets would no longer be found. Pin it: add this property to `BasketSlice` in `Shop/Basket.cs`, above `Initial`:

[!code-csharp[](samples/DuckShop/Shop/Basket.cs#basket-key)]

The value is the key the slice already had, so nothing moves. Leaving it out is not an error, but [DUCKY012](../diagnostics/DUCKY012.md) reminds you.

## Describe the state as JSON

Persistence writes the state as JSON. Ducky only serializes through code that the .NET JSON source generator writes at build time, which works in a trimmed WebAssembly app, without reflection. Create `Shop/ShopJson.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/ShopJson.cs#shop-json)]

Only `BasketState` is needed for persistence. The other lines let debugging tools and logs show the other states and actions; the analyzer [DUCKY006](../diagnostics/DUCKY006.md) lists any your context misses.

## Persist the basket

Create `Shop/BasketPersistence.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/BasketPersistence.cs#basket-persistence)]

- `Persist<BasketSlice>(...)` saves the slice to `localStorage`, under `ducky:basket`: the default prefix `ducky`, then the slice's key.
- `Version` is stored with the data. Imagine an earlier release of the shop stored the basket as a map from duck id to quantity, `{"Items":{"classic":2}}`. This release stores lines, so it declares version 2 and a **migration** from version 1: a function that rewrites the old JSON into the new shape before it is read. A user who still has a version 1 basket gets it back as lines.
- `SyncAcrossTabs = true` is the one line for tabs: a change saved in one tab is restored in the shop's other tabs.

Then, in `Program.cs`, replace the `AddDucky` call with this one, which adds the last two lines:

[!code-csharp[](samples/DuckShop/Program.cs#program-store)]

`UseJson` gives the store your JSON context, and `PersistBasket()` is the method you just wrote.

## Restored slices and the load rule

The store reads storage while it starts, and dispatches `StoreInitialized` only after: when `LoadCatalogueEffect` runs, a saved basket is already restored. When storage held one, `State.WasRestored<BasketState>()` is then true: the slice holds what storage gave back.

The catalogue isn't persisted, so its effect loads it every time. If you persisted it too, to show the shelf at once after a reload, its effect would have to skip the load when storage restored it. That is the **load rule** of the [render modes guide](../guides/blazor/render-modes.md#one-load-rule-in-every-mode), shown here on a variant of the effect from the sample's tests:

[!code-csharp[](samples/DuckShop.Tests/PersistenceTests.cs#load-rule)]

This variant dispatches only `CatalogueLoaded`, so storage only ever holds a finished load: a restored catalogue is always complete. (It has no failure action; a real one would dispatch `CatalogueFailed`, which storage would then keep.)

## Run it

- Reload the page (`Program.cs` changed, so `dotnet watch` restarts the app). Add two Pirate Ducks and reload again. The badge says **Basket: 2 ducks** again.
- Open the browser's developer tools, then Application (Chrome, Edge) or Storage (Firefox), then Local Storage. The entry `ducky:basket` holds `{"v":2,"at":"…","s":{"Lines":[{"DuckId":"pirate","Quantity":2}]}}`: the version, when it was written, and the state.
- Open the shop in a second tab and add a duck there. The first tab's badge follows.
- Replace the entry's value with a version 1 basket, `{"v":1,"at":"2026-11-01T09:00:00Z","s":{"Items":{"classic":2}}}`, and reload: the basket holds two Classic Yellows.

The sample's [PersistenceTests.cs](samples/DuckShop.Tests/PersistenceTests.cs) checks the reload, the migration and the load rule, with an in-memory stand-in for `localStorage`.

## Checkpoint

- Which code changed in the reducers and components to persist the basket? (None. Persistence is declared at registration.)
- Why pin `Key`? (The storage entry is named after it; a derived key changes when the class is renamed.)
- When is a migration's function called? (When the store reads an entry whose version is older than `Version`.)

## Common mistake

**Leaving the state out of the JSON context.** Without `[JsonSerializable(typeof(BasketState))]`, the build warns, [DUCKY006](../diagnostics/DUCKY006.md), and the app fails at startup with a `DuckyConfigurationException`, [DUCKY306](../diagnostics/DUCKY306.md) (*missing JSON type information*), whose message spells out the line to add. Ducky refuses to persist through reflection, which a trimmed WebAssembly app may have removed. Fix: add the type to `ShopJson`, and pass `ShopJson.Default` to `UseJson`.

Next: [Step 9: Test the shop](09-testing.md).

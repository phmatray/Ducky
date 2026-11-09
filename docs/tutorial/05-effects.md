# Step 5: Load data with an effect

## What you will build in this step

The catalogue: six ducks loaded from a fake server that takes 600 ms to answer. While it loads, the shelf says "Loading ducks…"; if the server fails, it says why.

## Why

Reducers are pure, so they can't call a server or wait. Work that leaves the store goes in an **effect**: a class that runs after an action has been reduced, does the slow or outside work, and dispatches new actions with what it learned. The state then records each moment of the load (started, succeeded, failed) as data, and the components show whichever moment the state is in.

## A fake server

A real shop would call its server with `HttpClient`. The tutorial uses a fake server that lives in the browser, behind an interface, so tests can replace it. Create `Shop/CatalogueApi.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/CatalogueApi.cs#catalogue-api)]

The fake waits on a `TimeProvider`, .NET's abstraction of the clock, instead of the real clock. The app gives it the system clock; step 9's tests give it a fake clock they move forward instantly.

Register both in `Program.cs`, above the `AddDucky` call:

[!code-csharp[](samples/DuckShop/Program.cs#program-catalogue-api)]

## The catalogue slice

Create `Shop/Catalogue.cs`, with the catalogue's state, the three actions of a load, and its slice:

[!code-csharp[](samples/DuckShop/Shop/Catalogue.cs#catalogue-state)]

- The state has a field for each thing the page shows: the ducks, whether a load is running, and the last error.
- The actions have no `[DuckyAction]`: only the effect dispatches them, so they need no helpers.
- `On<CatalogueLoading>(state => ...)` uses the reducer form without the action, for an action that carries no data.

## The effect

Add the effect at the end of `Shop/Catalogue.cs`:

[!code-csharp[](samples/DuckShop/Shop/Catalogue.cs#catalogue-effect)]

- `Effect<StoreInitialized>` runs when the store dispatches `StoreInitialized`, its own first action, once it has started. Loading on `StoreInitialized` is the rule the [render modes guide](../guides/blazor/render-modes.md#one-load-rule-in-every-mode) gives for every Blazor host.
- The effect gets `ICatalogueApi` through its constructor, from dependency injection.
- `context.Run` dispatches `started`, awaits `work`, then dispatches `succeeded` with the result. If `work` throws, it dispatches `failed` with the exception instead.
- `cancellationToken` is cancelled when the store is disposed. Pass it to everything the effect awaits.

Like the slice, the effect is registered by `AddDuckyGenerated_DuckShop()`.

## Show the catalogue

Replace `Components/Shelf.razor` with a shelf that selects the catalogue. This is the whole file for now:

[!code-razor[](samples/DuckShop.Tests/Steps/Step5/Shelf.razor#step5-shelf)]

The shelf now selects the whole `CatalogueState`, so it re-renders when the load starts, ends or fails. Each card still selects its own quantity.

## Run it

Reload the page. For about half a second the shelf says **Loading ducks…**, then six cards appear: Classic Yellow, Mellow Yellow, Pirate Duck, Disco Duck, Ninja Duck and Rubber Ducky. The basket works on all of them.

To see the error state, make the fake fail: in `FakeCatalogueApi.GetDucksAsync`, replace `return Ducks;` with `throw new HttpRequestException("The warehouse is offline.");`. After the same half second, the shelf shows **Couldn't load the ducks: The warehouse is offline.** Put `return Ducks;` back. The sample's [ComponentTests.cs](samples/DuckShop.Tests/ComponentTests.cs) checks both screens, and [CatalogueEffectTests.cs](samples/DuckShop.Tests/CatalogueEffectTests.cs) the actions the effect dispatches.

## Checkpoint

- Why isn't the server called from a reducer? (Reducers are pure: no I/O, no waiting. The effect does it.)
- What does `context.Run` dispatch when the server answers? When it throws? (`started` then `succeeded`; `started` then `failed`.)
- When does `LoadCatalogueEffect` run? (Once, on `StoreInitialized`, when the store has started.)

## Common mistake

**Injecting the store into the effect** and calling `store.Dispatch(...)` from it. The build warns, [DUCKY008](../diagnostics/DUCKY008.md) (*store injected into an effect*): the store creates its effects while it starts, so the constructor can fail, and actions dispatched through the store skip the rules that drop the results of a cancelled run (step 6 relies on them). Fix: dispatch through the `context` the handler receives (`context.Dispatch`, `context.Run`, or a generated helper on `context`).

Next: [Step 6: Search, and effects that overlap](06-concurrency.md).

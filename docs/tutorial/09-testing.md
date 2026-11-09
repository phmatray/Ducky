# Step 9: Test the shop

## What you will build in this step

A test project with three kinds of tests: the basket's reducers on their own, the catalogue effect on its own, and the whole store running the shop, search and basket included. They run in about a second, with no browser.

## Why

Every rule of the shop lives in a reducer, an effect or a selector, never in a component. So you can test the rules without rendering anything. And because the fake servers wait on a `TimeProvider`, a test can move time forward by 600 ms instantly instead of waiting.

`Ducky.Testing` gives you a real store made for tests, with a fake clock. It works with any test framework: a failed check throws `DuckyAssertionException`. This step uses xUnit and the Shouldly assertions.

## Create the test project

Stop `dotnet watch` (Ctrl+C). From the folder that contains `DuckShop`, install the xUnit v3 templates, create a test project next to the app, reference the app, and add the test packages:

```shell
dotnet new install xunit.v3.templates
dotnet new xunit3 -f net10.0 -o DuckShop.Tests
cd DuckShop.Tests
dotnet add reference ../DuckShop/DuckShop.csproj
dotnet add package Ducky.Testing --prerelease
dotnet add package Shouldly
```

- `-f net10.0` matters: the template targets .NET 8 by default, and a .NET 8 project can't reference the .NET 10 app.
- With `-f net10.0`, the template also writes a `global.json` in `DuckShop.Tests`: `{ "test": { "runner": "Microsoft.Testing.Platform" } }`. On the .NET 10 SDK, `dotnet test` needs it to run xUnit v3 projects, which use Microsoft.Testing.Platform. Without it, `dotnet test` stops with *Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK and later*. Keep it, and run `dotnet test` from the `DuckShop.Tests` folder.

Delete the template's `UnitTest1.cs`. The test files below go in the namespace `DuckShop.Tests` and start with `using Shouldly;` and `using Ducky.Testing;`; the ones that register services also need `using Ducky;`, `using Ducky.Generated;` and `using Microsoft.Extensions.DependencyInjection;`. Each snippet's caption links to the full file.

## Test the reducers

`SliceTest` gives a slice a starting state, dispatches actions and checks the result. Create `BasketTests.cs` with these tests in a `public sealed class BasketTests`:

[!code-csharp[](samples/DuckShop.Tests/BasketTests.cs#test-basket)]

- Without `Given`, the slice starts from its `Initial` state.
- `Then(basket => ...)` runs your assertions on the state. A record compares its lists by reference, so the tests check `Lines` rather than comparing whole `BasketState` records.
- `ThenUnchanged()` checks that the reducer returned the very same state, which is what lets the store skip a change that changes nothing.

## Test the effect

`EffectTest.Run` builds a store with one effect, dispatches one action, waits until the effect has finished and returns the actions it dispatched. The effect's server is a test double. Create `StubApis.cs` with two of them, one that answers at once and one that fails. This is the whole file:

[!code-csharp[](samples/DuckShop.Tests/StubApis.cs#test-stub-apis)]

Then create `CatalogueEffectTests.cs` with these tests in a `public sealed class CatalogueEffectTests`:

[!code-csharp[](samples/DuckShop.Tests/CatalogueEffectTests.cs#test-effect)]

`StoreInitialized` is the store's own action: `EffectTest` lets the store dispatch it when it starts, so the effect runs once.

## Test the whole shop

`TestStore` is a complete store for tests: your slices and effects, the services you register, and a fake clock in `store.Time`. Create `ShopTests.cs` with this test in a `public sealed class ShopTests`:

[!code-csharp[](samples/DuckShop.Tests/ShopTests.cs#test-store)]

- The test registers the app's own fake servers, `FakeCatalogueApi` and `FakeSearchApi`. They get the test store's fake clock, because the test store registers it as the `TimeProvider`.
- `store.Time.Advance(...)` moves that clock forward: the fake server's 600 ms pass at once.
- `await store.Settled()` waits until no action is queued and no effect is running.
- `ShelfSelectors.VisibleDucks.Evaluate(store.State)` runs the selector once against the current state, without a cache.
- `store.Failures` lists every reducer or effect that threw. The test store is strict: an action that nothing handles counts as a failure too.

## Run it

From the `DuckShop.Tests` folder:

```shell
dotnet test
```

The six tests pass, in about a second. The test project is also a program: `dotnet run` runs the same tests. The sample's test project has more: the [steps' screens](samples/DuckShop.Tests/ComponentTests.cs) rendered with bUnit, the [search policy](samples/DuckShop.Tests/SearchTests.cs) under `Switch` and `Merge`, the [selector](samples/DuckShop.Tests/ShelfSelectorsTests.cs) and [persistence](samples/DuckShop.Tests/PersistenceTests.cs).

## Checkpoint

- Why do the shop's tests need no browser? (The rules live in reducers, effects and selectors, which run on a plain store.)
- How does a test skip the fake server's delay? (The servers wait on the `TimeProvider`; the test store registers its fake clock, and the test advances it.)
- What does `Settled()` wait for? (An empty queue and no running effect.)

## Common mistake

**Forgetting to advance the clock.** Without `store.Time.Advance(FakeCatalogueApi.Latency)`, the fake server waits forever on a clock that never moves, so the effect never finishes, and `Settled()` throws `DuckyAssertionException` after 10 seconds of real time instead of hanging. Fix: advance the fake clock past the delay before you wait, as `ShopTests` does. [Testing with Ducky.Testing](../guides/core/testing.md) lists what else `TestStore` checks.

Next: [Where next](10-where-next.md).

# Tutorial: build a duck shop

In this tutorial you build the Duck Shop, a small Blazor WebAssembly app, one idea at a time. You need to know C# and the basics of Blazor components. You don't need to know Redux, Flux or any other state library: each term is explained the first time it appears.

## What you will build

- A **catalogue** of rubber ducks, loaded from a fake server that answers after a short delay, with a loading message and an error message.
- A **search box** that asks the server as you type and never shows the answer to an older search.
- A **shelf** that shows only the ducks that match, cheapest first.
- A **basket** that is still there after you reload the page, and stays the same in every tab of the shop.
- **Tests** for all of it, which run in about a second and need no browser.

The finished app is in the repository, in [docs/tutorial/samples/DuckShop](samples/DuckShop), with its tests in [docs/tutorial/samples/DuckShop.Tests](samples/DuckShop.Tests). A bigger shop built the same way runs live in the [Ducky Playground](https://phmatray.github.io/Ducky/playground/).

## What you need

- The .NET 10 SDK. Check it with `dotnet --version`: it prints `10.0.100` or later.
- An editor with C# support: Visual Studio, Visual Studio Code with the C# Dev Kit, or Rider.
- About an hour. Each step takes 5 to 10 minutes.

## The big picture

A Blazor app usually keeps its data in component fields, or in a service that raises an event when it changes. That works until two components show the same data, a slow server answer arrives after a newer one, or you need to know why a value changed. Ducky gives the data one home and one way to change:

```text
            ┌──────────── re-render, if a selected value changed ◀──────────┐
            ▼                                                               │
       component ── dispatch(action) ──▶ store: reducers ──▶ new state ─────┘
                                           │
                                           └──▶ effect ──▶ server ──▶ dispatch(result)
```

| Term | What it is | In the Duck Shop |
|---|---|---|
| State | The data your app shows, as immutable records: values that never change once created. | The basket's lines, the catalogue, the search |
| Slice | One part of the state, with its initial value and its reducers. | `BasketSlice`, `CatalogueSlice`, `SearchSlice` |
| Store | The object that holds every slice. There is one per app. | Registered in `Program.cs` |
| Action | A small record that says what happened. | `AddToBasket("pirate")` |
| Dispatch | Sending an action to the store. | Clicking "Add to basket" |
| Reducer | A pure function that takes the current state and an action, and returns the next state. Pure means it only computes: no server call, no clock, no change to its inputs. | "Add one pirate duck to the lines" |
| Dispatcher | The part of the store that takes actions one at a time, in order, and runs the reducers for each. | |
| Selector | A function that reads a value out of the state. A component re-renders only when its selected values change. | The basket's count, in the badge |
| Effect | Code that runs after an action is reduced, for work that leaves the store: calling a server, waiting. It dispatches new actions with what it learned. | Loading the catalogue |

## The steps

1. [Create the app and add Ducky](01-create-the-app.md): a new Blazor WebAssembly app with the store registered.
2. [Your first slice](02-first-slice.md): the basket's state, and why it is immutable.
3. [Actions and reducers](03-actions-and-reducers.md): how the basket changes.
4. [Show state in components](04-components.md): a badge and duck cards that select from the store and dispatch.
5. [Load data with an effect](05-effects.md): the catalogue from a fake server, with loading and error states.
6. [Search, and effects that overlap](06-concurrency.md): a search box where only the newest search answers.
7. [Derived data with a selector](07-selectors.md): the shelf shows only the ducks that match.
8. [Keep the basket across reloads](08-persistence.md): localStorage, versions and other tabs.
9. [Test the shop](09-testing.md): reducers, effects and the whole store, in a second.
10. [Where next](10-where-next.md): the guides by topic, the playground and DevTools.

## How to read a step

Each step starts with what you will build and why it matters, then adds code in small pieces: a sentence before each snippet says which file it goes in and where. Every snippet is cut from the finished app or its tests, which are compiled and tested on every build; the caption above a snippet links to its file. When a snippet is a whole file, the step says so.

Each step ends with **Run it** (what you should now see), a **Checkpoint** to check your understanding, and the **Common mistake** at that step, with the warning or symptom it gives and the fix.

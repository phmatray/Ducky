# Step 7: Derived data with a selector

## What you will build in this step

The shelf shows only the ducks that match the search, cheapest first, and says so when none matches. The list is computed by a **memoized selector**, defined once and reused.

## Why

The shelf's list is not stored anywhere: it is **derived** from two slices, the catalogue's ducks and the search's hits. Storing it as a third copy would mean keeping three things in step. Computing it in the component works, but it runs again at every render, and it builds a new list each time, so the component can't tell that nothing changed.

A selector defined with `Selector.Define` names its inputs. Its last function, the **projector**, runs again only when one of the inputs changed; otherwise the selector returns the list it computed last time, the very same instance. That is **memoization**. The shelf compares the value with the previous one, finds the same list, and doesn't re-render: adding a duck to the basket or typing in the box (before the answer) leaves the shelf alone.

## Define the selector

Create `Shop/ShelfSelectors.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/ShelfSelectors.cs#shelf-selectors)]

- The first two functions are the inputs: they read the ducks and the hits from the state, and they only read.
- The projector filters and sorts. Like a reducer, it must be pure: the same inputs always give the same list, and it changes nothing. The analyzers check projectors as they check reducers.
- `VisibleDucks` is a definition, which holds no cache, so a `static readonly` field is safe. Each component that uses it calls `Create()` and gets its own cache.

## Use it in the shelf

Update `Components/Shelf.razor`. It selects the visible ducks next to the catalogue, loops over them, and says when the search matched nothing. This is the whole file:

[!code-razor[](samples/DuckShop/Components/Shelf.razor#shelf)]

- `Select(ShelfSelectors.VisibleDucks.Create())` takes the memoized selector in place of a lambda.
- `_catalogue` still decides between loading, error and the shelf; `_visible` gives the ducks to show.
- `@using System.Collections.Immutable` at the top is for the `ImmutableList<Duck>` field.

## Run it

Reload the page and type `yel`: after the answer, the shelf shows two cards, Classic Yellow (€4.50) then Mellow Yellow (€5.20). Type `swan`: the box says **No duck matches.** and the shelf **No duck matches your search.** Clear the box: all six ducks come back, cheapest first, from Rubber Ducky (€3.90) to Disco Duck (€9.50). Add a duck to the basket: its card and the badge update, and the shelf doesn't re-render (you can't see this; the sample's ShelfSelectorsTests checks that the list stays the same instance).

The sample's [ShelfSelectorsTests.cs](samples/DuckShop.Tests/ShelfSelectorsTests.cs) checks the order and the filter, and that a basket change returns the same list instance.

## Checkpoint

- Why isn't the filtered list kept in a slice? (It can be computed from the catalogue and the search; a stored copy could disagree with them.)
- When does the projector run again? (When the ducks or the hits changed by reference.)
- Why does `VisibleDucks` sit in a `static readonly` field while each component calls `Create()`? (The definition has no cache; `Create()` gives each subscriber its own.)

## Common mistake

**Building a new list in a lambda selector**, as in `Select(state => state.Get<CatalogueState>().Ducks.Where(...).ToImmutableList())`. Nothing warns, and the list is right. But every selector runs again after each change in the store, and this one builds a new list each time, which never equals the previous one (an `ImmutableList` compares by reference). So the shelf, and every card in it, re-renders when you add a duck to the basket or type a letter. Fix: a `Selector.Define` selector. Its inputs run after each change, but its projector runs only when an input changed, and otherwise returns the same instance.

Next: [Step 8: Keep the basket across reloads](08-persistence.md).

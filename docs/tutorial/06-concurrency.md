# Step 6: Search, and effects that overlap

## What you will build in this step

A search box above the shelf. Each keystroke asks the server which ducks match, and the box says what is happening: "Searching for “yel”…", then "2 ducks match." The shelf itself starts following the search in step 7.

## Why

Type `y`, `e`, `l` quickly, and three searches are on their way: "y", "ye" and "yel". The server answers each one when it is ready, not in the order they were sent. Here, a short term matches more ducks and takes the fake server longer: "y" answers last. If every answer were applied, the last one to arrive, the answer to "y", would win, and the box would show three ducks for "yel".

An effect's **concurrency policy** decides what happens when its action arrives while an earlier run of the same effect is still going. For a search box, the newest search is the only one that matters.

## A fake search server

Create `Shop/SearchApi.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/SearchApi.cs#search-api)]

Register it in `Program.cs`, below the catalogue's registration:

[!code-csharp[](samples/DuckShop/Program.cs#program-search-api)]

## The search slice

Create `Shop/Search.cs`, with the search state, its two actions and the slice:

[!code-csharp[](samples/DuckShop/Shop/Search.cs#search-state)]

`TypeSearch` is dispatched by the search box, so it has `[DuckyAction]` and a `TypeSearch(...)` helper; `SearchAnswered` comes from the effect only.

## The effect, with Switch

Add the effect at the end of `Shop/Search.cs`:

[!code-csharp[](samples/DuckShop/Shop/Search.cs#search-effect)]

`Concurrency.Switch` cancels the run still going when a new `TypeSearch` arrives, and starts the new one. A cancelled run's `cancellationToken` is cancelled, so its server call stops waiting, and anything it would still dispatch is dropped. Only the run for the newest term can dispatch `SearchAnswered`. The four policies:

| Policy | A new action while a run is going | Use it for |
|---|---|---|
| `Merge` (the default) | starts another run; both go on | independent work, such as one save per item |
| `Switch` | cancels the running one; the newest wins | search boxes, "load the selected item" |
| `Exhaust` | starts no run until the running one finishes (the action is still reduced) | a submit button: a double click pays once |
| `Queue` | waits for the running one, then starts | work that must happen in order |

## The search box

Create `Components/SearchBox.razor`. This is the whole file:

[!code-razor[](samples/DuckShop/Components/SearchBox.razor#search-box)]

The box dispatches on every keystroke. It doesn't wait, debounce or cancel anything itself: that is the effect's policy.

Put the box on top of the shelf: in `Components/Shelf.razor`, add this line below `@inherits DuckyComponent`:

[!code-razor[](samples/DuckShop/Components/Shelf.razor#shelf-search)]

## Run it

Reload the page, then type `yel` quickly in the search box. It says **Searching for “yel”…**, then, about 300 ms later, **2 ducks match.** (Classic Yellow and Mellow Yellow). The answers to "y" and "ye" never show, though the fake server takes longer for them. Clear the box: the status line empties. The shelf still shows all six ducks; step 7 changes that.

The sample's [SearchTests.cs](samples/DuckShop.Tests/SearchTests.cs) types `y`, `ye`, `yel` on a fake clock: with `Switch`, one `SearchAnswered` arrives, with the two matches for "yel". The same effect with `Merge` gets three answers, and the last one, for "y", leaves three ducks for "yel".

## Checkpoint

- Why would `Merge` be wrong here? (Every search would run to the end and dispatch its answer; the slowest one, an old term, would arrive last and win.)
- What happens to a run that `Switch` cancels? (Its token is cancelled and anything it dispatches afterwards is dropped.)
- Which policy fits a "Pay" button? (`Exhaust`: clicks during a payment start no second payment.)

## Common mistake

**Keeping the default policy.** Without `public override Concurrency Policy => Concurrency.Switch;` the effect runs with `Merge`. Nothing warns: the search works when you type slowly and shows the wrong ducks when you type fast, the bug in the "Why" above. Fix: pick the policy for each effect on purpose. A related slip is not passing `cancellationToken` to the server call: `Switch` still drops the old run's answer, but the old request keeps running to the end for nothing.

Next: [Step 7: Derived data with a selector](07-selectors.md).

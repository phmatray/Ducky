# Step 4: Show state in components

## What you will build in this step

A page with a basket badge ("Basket: 2 ducks") and two duck cards. Each card has an "Add to basket" button, shows how many of its duck are in the basket, and can remove one.

## Why

The basket lives in the store; components need to show it and change it. A component **selects** the values it shows: it gives the store a small function that reads them from the state, and the store re-renders the component when, and only when, one of those values changes. The component changes the state by dispatching actions, never by touching the state.

You don't write any event, subscription or `StateHasChanged()` call yourself. And a component that selected only the basket's count doesn't re-render when the catalogue loads.

## The basket badge

Create a folder `Components`, and in it `BasketBadge.razor`. This is the whole file:

[!code-razor[](samples/DuckShop/Components/BasketBadge.razor#basket-badge)]

- `@inherits DuckyComponent` makes this a Ducky component: it gets `Select`, `Dispatch` and the `Store`.
- `Select((BasketState basket) => basket.Count)` registers a **selector**: a function from the basket's state to the value the badge shows. It returns a `Selection<int>`, whose `Value` is always the current count.
- Call `Select` in `OnInitialized`, once, and keep the selection in a field. Markup only reads `_count.Value`.

## The duck card

The card shows one duck, so it needs a type for ducks. Create `Shop/Duck.cs`. This is the whole file:

[!code-csharp[](samples/DuckShop/Shop/Duck.cs#duck)]

Then create `Components/DuckCard.razor`. This is the whole file:

[!code-razor[](samples/DuckShop/Components/DuckCard.razor#duck-card)]

- The card selects one number: how many of *its* duck are in the basket. The selector reads the `Duck` parameter, which is allowed: `Value` runs the selector again whenever it is read.
- The buttons dispatch with the helpers of step 3. `Store.AddToBasket(Duck.Id)` is the same as `Dispatch(new AddToBasket(Duck.Id))`.

## The shelf and the page

The cards need a parent. Create `Components/Shelf.razor`, with two ducks typed by hand for now; step 5 loads the real catalogue. This is the whole file for now:

[!code-razor[](samples/DuckShop.Tests/Steps/Step4/Shelf.razor#step4-shelf)]

The shelf selects nothing, so it is an ordinary component: only components that select need `DuckyComponent`. `@key="duck.Id"` tells Blazor which card belongs to which duck, so a card keeps its duck when the list changes.

Tell Razor where the components live: add this line at the end of `_Imports.razor`:

[!code-razor[](samples/DuckShop/_Imports.razor#imports-components)]

Last, replace the content of `Pages/Home.razor` with the shop's page. This is the whole file:

[!code-razor[](samples/DuckShop/Pages/Home.razor#home)]

For the shop's look (cards in a grid, a cream page), copy the sample's stylesheet, [app.css](samples/DuckShop/wwwroot/css/app.css), over your `wwwroot/css/app.css`. It is optional: everything works without it.

## Run it

Reload the page. It shows **Duck Shop**, **Basket: empty**, and two cards: Classic Yellow at €4.50 and Pirate Duck at €7.90.

- Click **Add to basket** on Classic Yellow twice. The badge says **Basket: 2 ducks**, and the card says **2 in your basket**.
- Click **Remove one**. The badge says **Basket: 1 duck**.
- The Pirate Duck card doesn't re-render while you click on Classic Yellow: its selected value, its own quantity, stays 0. (You can't see this; the sample's ComponentTests counts renders.)

The sample's [ComponentTests.cs](samples/DuckShop.Tests/ComponentTests.cs) renders these components with bUnit and checks adding, removing and the Pirate Duck's render count.

## Checkpoint

- When does the badge re-render? (When the count it selected changes, and only then.)
- Why is `Select` called in `OnInitialized`, not in the markup? (Selections are registered once, before the first render.)
- How does a card change the basket? (It dispatches an action; the slice's reducer computes the new basket.)

## Common mistake

**Calling `Select` in markup**, as in `<p>@Select((BasketState b) => b.Count).Value</p>`. Markup runs at every render, and a component registers its selections before its first render only. The build warns, [DUCKY009](../diagnostics/DUCKY009.md) (*Select after the first render*), and at run time the call throws `InvalidOperationException`, [DUCKY352](../diagnostics/DUCKY352.md). Fix: select once in `OnInitialized`, keep the `Selection<T>` in a field, and read its `Value` in markup.

Next: [Step 5: Load data with an effect](05-effects.md).

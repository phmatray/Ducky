# Step 3: Actions and reducers

## What you will build in this step

Two **actions**, "add a duck to the basket" and "remove one", and the **reducers** that turn each of them into the next basket.

## Why

The state can't be changed in place, so something has to produce the next state. In Ducky, nobody sets the basket directly. Code describes what happened, as an action, and the slice decides what that means for its state, in a reducer. That keeps every rule about the basket in one class, and every change goes through one door, which is what makes changes easy to follow, log and test.

## Declare the actions

An **action** is a small record that says what happened, in the past or as a request: `AddToBasket("pirate")`. It carries the data the change needs, and nothing else. Add the two actions to `Shop/Basket.cs`, between the records and the slice:

[!code-csharp[](samples/DuckShop/Shop/Basket.cs#basket-actions)]

`[DuckyAction]` asks the generator for **dispatch helpers**, methods named after the action. To **dispatch** an action is to send it to the store, and with the helpers, `store.AddToBasket("pirate")` does the same as `store.Dispatch(new AddToBasket("pirate"))`.

## Add the reducers

A **reducer** takes the current state and an action, and returns the next state. Add this constructor and helper method to `BasketSlice`, above `Initial`:

[!code-csharp[](samples/DuckShop/Shop/Basket.cs#basket-reducers)]

- `On<AddToBasket>(...)` registers the reducer for that action type, in the constructor. A slice has at most one reducer per action type.
- `WithQuantity` builds the new list and returns a new state with `state with { Lines = lines }`: a copy of the record with one property replaced. The old state is untouched.
- When nothing changes, for example removing a duck that isn't in the basket, `WithQuantity` returns the same `state` object. The store sees the same reference, keeps the old state as it is, notifies nobody and re-renders nothing.

A reducer is **pure**: given the same state and action it always returns the same result, and it does nothing else. No server call, no clock, no random number, no change to its inputs. Ducky's analyzers check the lambda you pass to `On<T>` as you type. They don't follow calls into a helper such as `WithQuantity`, so keep helpers pure too.

## One action at a time

The store's **dispatcher** takes actions in the order they are dispatched and reduces them one at a time: each reducer sees the state the previous action left. Two buttons clicked at once, or a click while a server answer arrives, can't interleave halfway through a reducer, so a reducer never needs a lock.

## Run it

Save. The build succeeds without a warning, and the page hasn't changed: the next step adds the components that dispatch these actions. The generator has written `AddToBasket(...)` and `RemoveFromBasket(...)` helpers: type `Store.AddTo` in a `.razor` file in the next step and your editor offers `AddToBasket`. The sample's [BasketTests.cs](samples/DuckShop.Tests/BasketTests.cs) runs these reducers: two `AddToBasket("classic")` give one line of two, and removing a duck that isn't there leaves the very same state.

## Checkpoint

- What is the difference between an action and a reducer? (The action says what happened; the reducer, in the slice, computes the next state from it.)
- Why does `WithQuantity` return the same `state` when nothing changes? (So the store keeps the old state and notifies nobody.)
- Why does a reducer never need a lock? (The dispatcher reduces one action at a time.)

## Common mistake

**Reading the clock, or making an id, inside a reducer.** Say each line should record when it was added, and the reducer reads the clock: `On<AddToBasket>((state, action) => state with { Lines = state.Lines.Add(new BasketLine(action.DuckId, 1) { AddedAt = DateTime.Now }) })`. The build warns, [DUCKY001](../diagnostics/DUCKY001.md) (*non-deterministic call in a reducer*). (Inside `WithQuantity` the same call is not reported: the analyzer reads only the lambda.) Either way the reducer is no longer pure: the same action gives a different state each time, so it can't be replayed or tested with a fixed input. Fix: compute the value where you dispatch, and put it in the action, as in `AddToBasket(DuckId, AddedAt)`.

Next: [Step 4: Show state in components](04-components.md).

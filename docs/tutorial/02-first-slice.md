# Step 2: Your first slice

## What you will build in this step

The basket's state: a record that lists which ducks are in the basket, and a **slice** that holds it in the store, starting empty.

## Why

In a component, a basket is often a `List<T>` field that event handlers change in place. Then a second component that shows the basket has its own copy, or has to be told when the list changed, and nothing records what changed or why.

Ducky keeps the basket in one place, the store, in one **slice**: a part of the state with its initial value and the rules that change it. The basket's state is **immutable**: once a `BasketState` exists, nothing can change it. A change creates a new `BasketState` instead. That gives you two things for free:

- Ducky knows that the basket changed by comparing two references, `old != new`, without comparing every line.
- A component that rendered the old basket can't see it change under its feet: what it rendered stays true until it renders again.

## Declare the state

Create a folder named `Shop` in the project, and in it a file `Basket.cs` with this content:

[!code-csharp[](samples/DuckShop/Shop/Basket.cs#basket-state)]

- A `record` is a class whose properties are set once, by its constructor. `BasketLine("pirate", 2)` means two pirate ducks.
- `ImmutableList<T>` (namespace `System.Collections.Immutable`) is a list that can't be changed in place: its `Add` returns a new list and leaves the old one as it was. Your state holds data this way all the way down.
- `Count` and `QuantityOf` only read the lines. A state type may compute values from its own data, as long as it never changes them.

## Declare the slice

Below the records, in the same file, add the slice:

[!code-csharp[](samples/DuckShop.Tests/Steps/Step2/Basket.cs#step2-basket-slice)]

- `Slice<BasketState>` says that this slice holds a `BasketState`. Each state type belongs to one slice.
- `Initial` is the state before anything has happened: a basket with no line. `new([])` creates a `BasketState` with an empty list.
- The slice has a **key**, the name it goes by in the store, in the browser's storage and in debugging tools. It comes from the class name: `BasketSlice` gives `basket`.

There is nothing to register: `AddDuckyGenerated_DuckShop()` from step 1 now includes `BasketSlice`, because the generator found it when the project built.

## Run it

Save the file. `dotnet watch` rebuilds without a warning, and the page still says **Hello, world!**: no component shows the basket yet. The store now holds an empty basket under the key `basket`; step 4 puts it on the page. The sample checks this step in [BasketSliceTests.cs](samples/DuckShop.Tests/Steps/Step2/BasketSliceTests.cs).

## Checkpoint

- What does "immutable" mean for `BasketState`? (Once created, it never changes. A change is a new `BasketState`.)
- How does Ducky tell that the basket changed? (It compares the old and the new state by reference.)
- What is `Initial` for? (It is the slice's state before any action.)

## Common mistake

**A mutable list in the state.** With `public List<BasketLine> Lines { get; set; }`, a reducer could call `state.Lines.Add(...)` and return the same state. The store compares references, sees the same object, and decides that nothing changed: no component re-renders, and the basket on screen is wrong. Written in the lambda you pass to `On<T>`, that call makes the build warn, [DUCKY003](../diagnostics/DUCKY003.md) (*mutation in a reducer*); inside a helper method such as the next step's `WithQuantity`, the analyzer can't see it and nothing warns. Fix: keep `ImmutableList<T>` in records, and make every change return a new state.

Next: [Step 3: Actions and reducers](03-actions-and-reducers.md).

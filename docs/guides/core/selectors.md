# Selectors

A selection is a value computed from the state that follows its changes. `IStore.Select` returns a `Selection<T>`: read `Value` (or use it where a `T` is expected), and dispose it when you are done.

## Select

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SelectorsGuideTests.cs#selector-select)]

- `Select((Todos todos) => ...)` projects one slice. It skips the projector while that slice's reference is unchanged.
- `onChange` runs after a commit, only when the projected value changed (by `EqualityComparer<T>.Default`, or the comparer you pass). It runs on the thread that reduced the action, which may be a thread-pool thread: in Blazor, Ducky's components marshal to the renderer for you, and your own callbacks must use `InvokeAsync` for UI work.
- `Value` is computed against the current state each time you read it.

## Reusable, memoized selectors

`Selector.Define` combines up to four inputs with a projector. The projector runs again only when an input changed (by reference, or by value for value types):

[!code-csharp[](../../../test/Ducky.Docs.Tests/Core/SelectorsGuideTests.cs#selector-defs)]

A `SelectorDef` holds no cache, so a static field is safe. `Create()` returns a selector with its own single-entry cache, one per subscriber; `Evaluate(state)` computes once without a cache. A definition with an argument takes it as a function in `Create(() => id)`, read on every evaluation, so the selection follows the argument when it changes.

Projectors must be pure, like reducers (DUCKY001 to DUCKY003 apply to them), and may run on two threads at once.

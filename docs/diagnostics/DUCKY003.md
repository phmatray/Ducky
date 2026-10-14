# DUCKY003: Mutation in a reducer, mutator or projector

**Package:** Ducky (analyzer `Ducky.Generators`). **Surface:** build warning on the write or the call (SPEC §16.2).

## What happened

A reducer passed to `On<T>`, a mutator passed to `SliceStore.Set`/`SetAsync` or a projector passed to
`Selector.Define` changes a value it doesn't own. The message
says which kind:

- **a parameter**: an assignment, compound assignment, `++`/`--` or `??=` to a member, element or indexer reached from
  the lambda's own parameters (the state and the action of a reducer, the state of a mutator, the inputs of a projector), as in
  `s.Box.Value = 1`, `s.Items[0] = 1` or each element of a deconstruction (`(s.Box.Value, _) = (1, 2)`); or a known
  mutator of a mutable collection (`Add`, `AddRange`, `Insert`, `Remove`, `RemoveAll`, `Clear`, `Sort`, `Reverse`,
  `Enqueue`, `Push`, `TryAdd`, `AddOrUpdate`, `UnionWith`… on a type of `System.Collections`, `.Generic`,
  `.ObjectModel` or `.Concurrent`) called on that graph, as in `s.Items.Add(item)`;
- **a captured variable**: a write to a local or parameter declared outside the lambda, or to a member of one;
- **a field**: a write to a field or property of the enclosing object (`_count++`, `this.X = …`) or to a static member.

The previous state is shared: the store keeps it for selectors, Redux DevTools and components that rendered it, and
change detection compares references, so a mutated state is never seen as changed. A projector is memoized on its
inputs, which it must not alter either.

Not reported: rebinding the parameter itself (`s = s with { … }`), locals and parameters declared inside the lambda,
including those of a nested lambda such as a recipe (`s.Produce(d => d.Items.Add(x))`), object and collection
initializers, `with` expressions, and immutable collections (`ImmutableList<T>.Add` returns a new list). A write whose
receiver is a draft is not reported either, wherever the draft comes from (a captured `CreateDraft()` draft, a field):
a type implementing `Ducky.Draft.IDraft<T>` (`draft.Count = 1`, `draft.Child.Value = 1`) or a Ducky.Draft collection draft
(`ListDraft`, `DictionaryDraft`, `SetDraft`: `draft.Items[0] = 1`) edits the draft, never the state it was made from.
A plain object or an array reached through a draft (`draft.Box.Value = 1`, `draft.Array[0] = 1`) is shared with that
state and is still reported. The types are matched by metadata name, so the rule needs no reference to Ducky.Draft. The analyzer
follows references, not data flow: an alias (`var items = s.Items; items.Add(x);`), a call result and a write through an `out` or `ref`
argument (`int.TryParse(text, out _count)`) are not tracked.
Only the lambda written directly as the `On<T>` reducer, the `Set`/`SetAsync` mutator or the `Define` projector is
analysed; method groups, stored
delegates and generated code are not.

## How to fix it

Return a new state: `s with { Count = s.Count + 1 }`, `s with { Items = s.Items.Add(item) }` with an immutable
collection, or a collection expression (`[.. s.Items, item]`). Keep counters and logs out of reducers: an effect or
middleware can observe actions instead.

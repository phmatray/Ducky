# Migrating from Mutty

Ducky.Draft is Mutty's successor: the same Immer-style `Produce` over records, rewritten around lazy copy-on-write drafts that keep the original instances of everything a recipe leaves unchanged. It does not depend on Ducky, so a project that used Mutty without Ducky can switch too.

**Ducky.Draft needs `net10.0`.** Mutty is deprecated on nuget.org. Its last release, Mutty 2.0.0, keeps working on older target frameworks, unmaintained: a project that can't move to `net10.0` can stay on it.

Every snippet on this page comes from the compiled sample in `docs/migration/samples/mutty`: `Before` builds against Mutty 2.0.0, `After` is the same code on Ducky.Draft, and `After.Tests` holds its tests.

## Checklist

1. Replace the `Mutty` package with `Ducky.Draft`. While both are referenced, the build warns with DUCKY901.
2. Replace `using Mutty;` with `using Ducky.Draft;`, and `[MutableGeneration] record R` with `[Draftable] partial record R`. A record that isn't a `partial record class` gets [DUCKY101](../diagnostics/DUCKY101.md).
3. Rename `MutableR` to `R.Draft`, and `FinishDraft()` to `Build()`.
4. Fix the collection edits the compiler reports, per the [mapping table](#mapping-table): `Find` and the other `List<T>` methods, assigning a `List<T>`, and the queue, stack and sorted-dictionary calls whose result is now discarded ([DUCKY109](../diagnostics/DUCKY109.md)).
5. Move `OnBeforeBuild` validation into the reducer, and replace `With{Prop}` calls with setters.
6. Run the tests, then read [Behaviour changes](#behaviour-changes).

## Before and after

### Records

[!code-csharp[](samples/mutty/Before/Todos.cs#before-state)]

[!code-csharp[](samples/mutty/After/Todos.cs#after-state)]

### Scalar and nested-record edits

Unchanged: the recipe text stays the same.

[!code-csharp[](samples/mutty/Before/Todos.cs#before-edits)]

[!code-csharp[](samples/mutty/After/Todos.cs#after-edits)]

### Lists

Mutty exposed an `ImmutableList<T>` or `ImmutableArray<T>` member as a `List<T>`, or a `List<MutableT>` when `T` was annotated. Ducky.Draft exposes a `ListDraft<T>`, or a `ListDraft<T, T.Draft>`, which copies nothing until the first change. `Find` becomes LINQ's `FirstOrDefault`; `Add` still takes the record and `RemoveAll` a predicate; a whole list is assigned as an immutable list. `AddRange` exists only on a `ListDraft<T>` of non-draftable elements; on a `ListDraft<T, T.Draft>` add the records in a loop, as for `InsertRange`.

[!code-csharp[](samples/mutty/Before/Todos.cs#before-lists)]

[!code-csharp[](samples/mutty/After/Todos.cs#after-lists)]

### Queues, stacks and sorted dictionaries

Mutty exposed `ImmutableQueue<T>`, `ImmutableStack<T>` and `ImmutableSortedDictionary<K,V>` members as a mutable `Queue<T>`, `Stack<T>` and `SortedDictionary<K,V>`. Ducky.Draft keeps their immutable type, whose methods return a new instance: assign it. Left as it was, `d.Pending.Enqueue(job)` would compile and lose the edit, so [DUCKY109](../diagnostics/DUCKY109.md) flags it.

[!code-csharp[](samples/mutty/Before/Todos.cs#before-queue)]

[!code-csharp[](samples/mutty/After/Todos.cs#after-queue)]

### Drafts edited in several steps

[!code-csharp[](samples/mutty/Before/Todos.cs#before-steps)]

[!code-csharp[](samples/mutty/After/Todos.cs#after-steps)]

## Mapping table

| Mutty | Ducky.Draft |
|---|---|
| `[MutableGeneration] record R` | `[Draftable] partial record R` |
| `MutableR` | `R.Draft` |
| `r.Produce(d => …)` | unchanged for scalar and nested-record edits; collection edits per the rows below |
| `List<T>`/`List<MutableT>` members (Mutty's view of `ImmutableList`/`ImmutableArray`) | `ListDraft<T>` / `ListDraft<T, T.Draft>`: replace `Find`/`FindIndex`/`ForEach` with LINQ `First` or an index (`d.Items.First(x => x.Id == id).Done = true`), use `AddRange`/`RemoveAll`, assign `.ToImmutableList()` (or the implicit conversion) instead of a `List<T>`, and pass the record, not a draft, to `Add`; `InsertRange`/`RemoveRange` become loops |
| `IReadOnlyList<T>`/`IReadOnlyCollection<T>` members (Mutty: `List<T>`) | plain get/set of the same type; switch the member to `ImmutableList<T>` to get a `ListDraft` |
| `ImmutableQueue`/`ImmutableStack`/`ImmutableSortedDictionary` members (Mutty: mutable `Queue`/`Stack`/`SortedDictionary`) | plain get/set: assign the result (`d.Q = d.Q.Enqueue(x)`); [DUCKY109](../diagnostics/DUCKY109.md) flags a discarded one |
| `[MutableGeneration] record struct` | not supported ([DUCKY101](../diagnostics/DUCKY101.md)): make it a `record class`, or edit it with `with` |
| `r.CreateDraft()` / `d.FinishDraft()` | `r.CreateDraft()` / `d.Build()` |
| `OnBeforeBuild` | removed (validate in the reducer) |
| `implicit operator MutableR(R)` (assigning a record to a nested mutable member) | kept as `implicit operator R.Draft?(R?)`, so `d.Child = value` still compiles; a list of draftable elements is replaced with `new ListDraft<T, T.Draft>(value, static x => new T.Draft(x))` |
| `With{Prop}`, other implicit/explicit operators, `AsMutable`/`ToImmutable` | removed (use setters / `Produce`) |
| `using Mutty;` | `using Ducky.Draft;` |

Mutty's mutable `Dictionary`/`HashSet`/`SortedSet` views become drafts: an `ImmutableDictionary<K,V>` member becomes a `DictionaryDraft` (for draftable values, a `DictionaryDraft<K, V, V.Draft>`: write with `Set(key, record)`, not the indexer or `Add`), an `ImmutableHashSet<T>` or `ImmutableSortedSet<T>` member a `SetDraft<T>` (`ISet<T>` only, no `Min`/`Max`). A mutable collection member (`List<T>`, `T[]`, `Dictionary<K,V>`) is a plain get/set, replaced wholesale, with a [DUCKY105](../diagnostics/DUCKY105.md) warning. Mutty's restriction to top-level records (MUTTY003) is gone: a record nested in partial types is supported, a generic one is not ([DUCKY103](../diagnostics/DUCKY103.md)).

## Behaviour changes

- **A no-op recipe returns the original instance.** Mutty's `Build()` always returned `_record with {...}`, a new record, so every reducer built on it made the store notify. Ducky.Draft compares each member with the original at `Build`, so a recipe that changes nothing (`SetFilter` to the current filter, `Toggle` of an unknown id) returns the same reference, and the store sees no change. Pinned by `SetFilter_SameValue_ReturnsSameReference` and `Toggle_UnknownId_ReturnsSameReference` in the sample's tests.
- **Unchanged members keep their instances.** Editing one todo returns a new list whose other elements are the original records, not copies.
- **A draft is revoked when `Produce` returns.** Using it later, or keeping it past a structural list operation or an assignment that replaced it, throws `ObjectDisposedException` instead of losing the write. [DUCKY106](../diagnostics/DUCKY106.md) flags a draft that escapes its recipe.

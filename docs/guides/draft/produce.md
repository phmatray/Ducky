# Ducky.Draft

`Ducky.Draft` lets a reducer write `draft.Cards.Add(card)` and `card.Done = true` instead of nested `with` expressions, and still return an immutable record. Its source generator writes a copy-on-write `Draft` class for each record you mark. It doesn't depend on Ducky: any code that edits immutable records can use it.

```shell
dotnet add package Ducky.Draft
```

## Mark the records

Mark each record you want to edit with `[Draftable]`, as a `partial record` class:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Draft/DraftGuideTests.cs#draft-state)]

The generator adds a nested `Board.Draft`, `board.Produce(recipe)` and `board.CreateDraft()`. Records nested in other types need partial containing types. Record structs (DUCKY101) and generic records (DUCKY103) are not supported.

## Edit in a reducer

`Produce` hands a draft to the recipe and returns the edited record:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Draft/DraftGuideTests.cs#draft-slice)]

- **Nothing changed, same instance.** When the recipe changes nothing (the card is already done, or there is no such card), `Produce` returns the original record, so the reducer reports no change and no subscriber is notified. A value set back to its original counts as no change too (below).
- **Untouched parts keep their references.** Completing card 1 builds a new board, a new list and a new card 1; card 2 is the same instance, so selectors over it don't recompute.
- **A collection operation is a change.** `Add`, `Insert` and `RemoveAt` build a new collection even when the contents end up equal (`Add` then `RemoveAt`). A `RemoveAll` that matches nothing and a `Clear` of an empty list change nothing.

[!code-csharp[](../../../test/Ducky.Docs.Tests/Draft/DraftGuideTests.cs#draft-revert)]

## Members and collections

| Member type | In the draft |
|---|---|
| a `[Draftable]` record | its `Draft`; assign a record to replace it (`draft.Owner = user`) |
| `ImmutableList<T>`, `ImmutableArray<T>` | a list draft; with `[Draftable]` elements, each element is a draft (`draft.Cards[0].Done = true`, LINQ over the drafts) |
| `ImmutableDictionary<K, V>` | a dictionary draft, with element drafts for `[Draftable]` values |
| `ImmutableHashSet<T>`, `ImmutableSortedSet<T>` | a set draft |
| anything else | the value itself, get and set |

- Replace a whole list of draftable elements with `draft.Cards = new(loaded, static card => new Card.Draft(card))`.
- `List<T>`, arrays and `Dictionary<K, V>` members are replaced whole (DUCKY105 warns).
- `ImmutableQueue<T>`, `ImmutableStack<T>`, `ImmutableSortedDictionary<K, V>` and `EntityState<TKey, TEntity>` stay values: assign what their methods return (`draft.Pending = draft.Pending.Enqueue(job)`). DUCKY109 flags a discarded result.
- Enumerating a list of draftable elements creates a draft per element visited.

## Several steps

`CreateDraft()` returns a draft to edit over several statements; `Build()` returns the result:

[!code-csharp[](../../../test/Ducky.Docs.Tests/Draft/DraftGuideTests.cs#draft-create)]

## Drafts don't outlive their recipe

When `Produce` returns, its draft is revoked: using it afterwards throws `ObjectDisposedException`, and DUCKY106 reports a draft that escapes its recipe at build time. An element draft also goes stale after a structural operation on its list (`var card = draft.Cards[0]; draft.Cards.RemoveAt(1); card.Done = true;` throws): read it again from the list.

Coming from Mutty? See [the Mutty migration guide](../../migration/mutty.md).

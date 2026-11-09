# Ducky.Draft

Ducky.Draft lets you edit an immutable C# record as if it were mutable, Immer-style: write `draft.Cards.Add(card)` and `card.Done = true` instead of nested `with` expressions, and get a new record back. Its source generator writes a copy-on-write `Draft` class for each record you mark with `[Draftable]`.

- Nothing changed, same instance: when the recipe changes nothing, `Produce` returns the original record.
- Untouched parts keep their references, so equality checks and memoized selectors over them stay cheap.

It does not depend on [Ducky](https://www.nuget.org/packages/Ducky): any code that edits immutable records can use it. It is the successor of Mutty.

## Install

```shell
dotnet add package Ducky.Draft --prerelease
```

Ducky.Draft targets .NET 10. Reference it directly from every project that declares `[Draftable]` records: the generator ships inside this package.

## Example

Mark each record you want to edit, as a `partial record` class:

```csharp
using System.Collections.Immutable;
using Ducky.Draft;

[Draftable]
public sealed partial record Board(ImmutableList<Card> Cards, string Filter);

[Draftable]
public sealed partial record Card(int Id, string Title, bool Done);
```

`Produce` hands a draft to the recipe and returns the edited record:

```csharp
var next = board.Produce(draft =>
{
    draft.Cards.Add(new Card(3, "Celebrate", Done: false));
    if (draft.Cards.FirstOrDefault(card => card.Id == 1) is { } card)
    {
        card.Done = true; // a new board, list and card 1; every other card keeps its reference
    }
});
```

`CreateDraft()` and `Build()` do the same in several steps.

## Learn more

- Guide: https://phmatray.github.io/Ducky/docs/guides/draft/produce.html
- Documentation: https://phmatray.github.io/Ducky/
- Changelog: https://github.com/phmatray/Ducky/blob/v2/CHANGELOG.md
- Issues and source: https://github.com/phmatray/Ducky

Ducky.Draft is licensed under the Apache License 2.0.

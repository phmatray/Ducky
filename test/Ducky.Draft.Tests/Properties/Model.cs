using System.Collections.Immutable;

namespace Ducky.Draft.Tests.Properties;

// The random nested records of the INV-26 properties (SPEC §17.7): every draft member kind of the §13.3 mapping table.
[Draftable]
internal sealed partial record Tag(string Name);

[Draftable]
internal sealed partial record Item(int Id, string Title, Tag Label, Tag? Extra);

[Draftable]
internal sealed partial record Root(int Count, string? Name, Item Main, Item? Spare)
{
    public required ImmutableList<Item> Items { get; init; }

    public required ImmutableArray<Item> Cards { get; init; }

    public required ImmutableArray<int> Numbers { get; init; }

    public required ImmutableList<Item?> Holes { get; init; }

    public required ImmutableDictionary<string, Item> ById { get; init; }

    public required ImmutableDictionary<string, int> Counts { get; init; }

    public required ImmutableHashSet<string> Tags { get; init; }

    public required ImmutableSortedSet<int> Ranks { get; init; }

    public ImmutableList<string>? Names { get; init; }
}

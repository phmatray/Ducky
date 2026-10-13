using System.Collections.Immutable;
using CsCheck;

namespace Ducky.Draft.Tests.Properties;

// Random nested records for the INV-26 properties. Small value pools make equal values, colliding keys and case-only
// key pairs (under OrdinalIgnoreCase) common; every string is a fresh instance, so an equal value is rarely the same one.
internal static class Arbitrary
{
    public static readonly IComparer<int> Descending = Comparer<int>.Create((x, y) => y.CompareTo(x));

    public static readonly Gen<string> Text = Gen.Int[0, 3].Select(n => new string('a', n) + "b");

    public static readonly Gen<string> Key = Gen.OneOfConst("a", "A", "b", "c").Select(Copy);

    public static readonly Gen<int> Small = Gen.Int[0, 4];

    public static readonly Gen<int> Seed = Gen.Int[0, 1000];

    public static readonly Gen<Tag> Tag = Text.Select(name => new Tag(name));

    public static readonly Gen<Item> Item = Gen.Select(Small, Text, Tag, Tag.Null(0.3), (id, title, label, extra) => new Item(id, title, label, extra));

    public static readonly Gen<ImmutableList<Item>> Items = Item.List[0, 4].Select(ImmutableList.CreateRange);

    public static readonly Gen<ImmutableArray<Item>> Cards = Item.Array[0, 4].Select(ImmutableArray.Create);

    public static readonly Gen<ImmutableArray<int>> Numbers = Small.Array[0, 5].Select(ImmutableArray.Create);

    public static readonly Gen<ImmutableList<Item?>> Holes = Item.Null(0.4).List[0, 4].Select(ImmutableList.CreateRange);

    // Value comparers that ignore a member: an Item compared by Id, an int by parity. An element edit the ValueComparer calls
    // equal must still be kept, and a write it calls equal is no change (SetItem), on the draft as on the `with` side.
    public static readonly IEqualityComparer<Item> ItemsById = EqualityComparer<Item>.Create((x, y) => x?.Id == y?.Id, x => x.Id);

    public static readonly IEqualityComparer<int> Parity = EqualityComparer<int>.Create((x, y) => x % 2 == y % 2, x => x % 2);

    public static readonly Gen<ImmutableDictionary<string, Item>> ById = Dictionary(Item, ItemsById);

    public static readonly Gen<ImmutableDictionary<string, int>> Counts = Dictionary(Small, Parity);

    public static readonly Gen<ImmutableHashSet<string>> Tags =
        Gen.Select(Gen.OneOfConst(StringComparer.Ordinal, StringComparer.OrdinalIgnoreCase), Key.List[0, 4], ImmutableHashSet.CreateRange);

    public static readonly Gen<ImmutableSortedSet<int>> Ranks =
        Gen.Select(Gen.OneOfConst(Comparer<int>.Default, Descending), Small.List[0, 4], ImmutableSortedSet.CreateRange);

    public static readonly Gen<ImmutableList<string>?> Names = Text.List[0, 3].Select(ImmutableList.CreateRange).Null(0.3);

    public static readonly Gen<Root> Root = Gen.Select(
        Gen.Select(Small, Text.Null(0.3), Item, Item.Null(0.4)),
        Gen.Select(Items, Cards, Numbers, Holes),
        Gen.Select(ById, Counts, Tags, Ranks, Names),
        (scalars, lists, others) => new Root(scalars.Item1, scalars.Item2, scalars.Item3, scalars.Item4)
        {
            Items = lists.Item1,
            Cards = lists.Item2,
            Numbers = lists.Item3,
            Holes = lists.Item4,
            ById = others.Item1,
            Counts = others.Item2,
            Tags = others.Item3,
            Ranks = others.Item4,
            Names = others.Item5,
        });

    // Either key comparer (case-only key pairs collide under OrdinalIgnoreCase) and either value comparer, so the
    // comparer-identity checks of Structural are never vacuous.
    private static Gen<ImmutableDictionary<string, T>> Dictionary<T>(Gen<T> value, IEqualityComparer<T> valueComparer) => Gen.Select(
        Gen.OneOfConst(StringComparer.Ordinal, StringComparer.OrdinalIgnoreCase),
        Gen.OneOfConst(EqualityComparer<T>.Default, valueComparer),
        Gen.Select(Key, value).List[0, 4],
        (keys, values, pairs) => pairs.Aggregate(ImmutableDictionary.Create<string, T>(keys, values), (d, p) => d.SetItem(p.Item1, p.Item2)));

    // A copy equal to the original but never the same instance (a string, or a record rebuilt member by member).
    public static string Copy(string value) => new(value.AsSpan());

    [return: System.Diagnostics.CodeAnalysis.NotNullIfNotNull(nameof(value))]
    public static string? CopyOrNull(string? value) => value is null ? null : Copy(value);

    // A deep copy: no shared instance with the original apart from the comparers, which must stay the same (SPEC §13.3).
    public static Root Clone(Root r) => new(r.Count, CopyOrNull(r.Name), Clone(r.Main), CloneOrNull(r.Spare))
    {
        Items = ImmutableList.CreateRange(r.Items.Select(Clone)),
        Cards = ImmutableArray.CreateRange(r.Cards.Select(Clone)),
        Numbers = ImmutableArray.CreateRange(r.Numbers),
        Holes = ImmutableList.CreateRange(r.Holes.Select(CloneOrNull)),
        ById = ImmutableDictionary.Create<string, Item>(r.ById.KeyComparer, r.ById.ValueComparer)
            .AddRange(r.ById.Select(p => KeyValuePair.Create(Copy(p.Key), Clone(p.Value)))),
        Counts = ImmutableDictionary.Create<string, int>(r.Counts.KeyComparer, r.Counts.ValueComparer)
            .AddRange(r.Counts.Select(p => KeyValuePair.Create(Copy(p.Key), p.Value))),
        Tags = ImmutableHashSet.CreateRange(r.Tags.KeyComparer, r.Tags.Select(Copy)),
        Ranks = ImmutableSortedSet.CreateRange(r.Ranks.KeyComparer, r.Ranks),
        Names = r.Names is null ? null : ImmutableList.CreateRange(r.Names.Select(Copy)),
    };

    public static Item Clone(Item i) => new(i.Id, Copy(i.Title), new Tag(Copy(i.Label.Name)), i.Extra is null ? null : new Tag(Copy(i.Extra.Name)));

    public static Item? CloneOrNull(Item? i) => i is null ? null : Clone(i);
}

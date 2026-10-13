using System.Collections.Immutable;
using CsCheck;

namespace Ducky.Draft.Tests.Properties;

// One edit of a random script, written twice: through the draft (Produce) and with `with` expressions (the manual oracle).
// Path names the member it writes ("Main.Title"); Element is the index seed of an element-only list edit (no index shift).
// Indices are seeds taken modulo the current count on each side, so both sides pick the same element while they agree.
internal sealed class Edit(string path, string label, Action<Root.Draft> draft, Func<Root, Root> manual, int? element = null)
{
    public string Path => path;

    public int? Element => element;

    public void Apply(Root.Draft d) => draft(d);

    public Root Apply(Root r) => manual(r);

    public override string ToString() => label;
}

internal static class Edits
{
    // SPEC §17.7: assigning null and null elements in nullable-element lists, collection replacement through an implicit
    // conversion, nested draft replacement, assigning a record to a draftable member and no-op ImmutableArray<T> edits.
    public static readonly Gen<Edit> Any = Gen.OneOf(
    [
        .. Scalars(),
        .. Nested(),
        .. ItemList(),
        .. CardArray(),
        .. NumberArray(),
        .. HoleList(),
        .. Dictionaries(),
        .. Sets(),
    ]);

    public static readonly Gen<Edit[]> Script = Any.Array[0, 24];

    public static Root Manual(Root r, IEnumerable<Edit> edits) => edits.Aggregate(r, (current, edit) => edit.Apply(current));

    public static Root Produce(Root r, IEnumerable<Edit> edits) => r.Produce(d =>
    {
        foreach (var edit in edits)
        {
            edit.Apply(d);
        }
    });

    public static int At(int seed, int count) => seed % count;

    private static IEnumerable<Gen<Edit>> Scalars() =>
    [
        Gen.Int[-2, 2].Select(v => new Edit("Count", $"Count = {v}", d => d.Count = v, r => r with { Count = v })),
        Arbitrary.Text.Null(0.4).Select(s => new Edit("Name", $"Name = {s ?? "null"}", d => d.Name = s, r => r with { Name = s })),
        Arbitrary.Names.Select(l => new Edit("Names", $"Names = {Show(l)}", d => d.Names = l, r => r with { Names = l })),
        Arbitrary.Text.Select(s => new Edit("Names", $"Names?.Add({s})", d => d.Names?.Add(s), r => r with { Names = r.Names?.Add(s) })),

        // Reads draft every member kind (and an element of each element-drafted collection) without writing anything.
        Arbitrary.Seed.Select(n => new Edit("(read)", $"read every member, element #{n}", d => Read(d, n), r => r)),
    ];

    private static void Read(Root.Draft d, int seed)
    {
        _ = d.Main.Label.Name;
        _ = d.Main.Extra?.Name;
        _ = d.Spare?.Label.Name;
        IfAny(d.Items.Count, seed, at => _ = d.Items[at].Label.Name);
        IfAny(d.Cards.Count, seed, at => _ = d.Cards[at].Extra?.Name);
        IfAny(d.Holes.Count, seed, at => _ = d.Holes[at]);
        IfAny(d.Numbers.Count, seed, at => _ = d.Numbers[at]);
        _ = d.ById.Values.Select(v => v.Title).ToList();
        _ = d.Counts.Count + d.Tags.Count + d.Ranks.Count + (d.Names?.Count ?? 0);
    }

    private static IEnumerable<Gen<Edit>> Nested() =>
    [
        Arbitrary.Text.Select(s => new Edit("Main.Title", $"Main.Title = {s}", d => d.Main.Title = s, r => r with { Main = r.Main with { Title = s } })),
        Arbitrary.Small.Select(v => new Edit("Main.Id", $"Main.Id = {v}", d => d.Main.Id = v, r => r with { Main = r.Main with { Id = v } })),
        Arbitrary.Text.Select(s => new Edit(
            "Main.Label.Name", $"Main.Label.Name = {s}", d => d.Main.Label.Name = s, r => r with { Main = r.Main with { Label = r.Main.Label with { Name = s } } })),
        Arbitrary.Tag.Null(0.4).Select(t => new Edit(
            "Main.Extra", $"Main.Extra = {t?.Name ?? "null"}", d => d.Main.Extra = t, r => r with { Main = r.Main with { Extra = t } })),
        Arbitrary.Text.Select(s => new Edit(
            "Main.Extra.Name",
            $"Main.Extra?.Name = {s}",
            d =>
            {
                if (d.Main.Extra is { } extra)
                {
                    extra.Name = s;
                }
            },
            r => r.Main.Extra is null ? r : r with { Main = r.Main with { Extra = r.Main.Extra with { Name = s } } })),
        Arbitrary.Item.Select(i => new Edit("Main", $"Main = {Show(i)}", d => d.Main = i, r => r with { Main = i })),
        Gen.Select(Arbitrary.Item, Arbitrary.Text, (i, s) => new Edit(
            "Main",
            $"Main = draft of {Show(i)} with Title {s}",
            d =>
            {
                var replacement = i.CreateDraft();
                replacement.Title = s;
                d.Main = replacement;
            },
            r => r with { Main = i with { Title = s } })),
        Arbitrary.Item.Null(0.4).Select(i => new Edit("Spare", $"Spare = {Show(i)}", d => d.Spare = i, r => r with { Spare = i })),
        Arbitrary.Text.Select(s => new Edit(
            "Spare.Title",
            $"Spare?.Title = {s}",
            d =>
            {
                if (d.Spare is { } spare)
                {
                    spare.Title = s;
                }
            },
            r => r.Spare is null ? r : r with { Spare = r.Spare with { Title = s } })),
    ];

    private static IEnumerable<Gen<Edit>> ItemList() =>
    [
        Arbitrary.Item.Select(i => new Edit("Items", $"Items.Add({Show(i)})", d => d.Items.Add(i), r => r with { Items = r.Items.Add(i) })),
        Gen.Select(Arbitrary.Seed, Arbitrary.Item, (n, i) => new Edit(
            "Items",
            $"Items.Insert(#{n}, {Show(i)})",
            d => d.Items.Insert(At(n, d.Items.Count + 1), i),
            r => r with { Items = r.Items.Insert(At(n, r.Items.Count + 1), i) })),
        Gen.Select(Arbitrary.Seed, Arbitrary.Item, (n, i) => new Edit(
            "Items",
            $"Items.Set(#{n}, {Show(i)})",
            d => IfAny(d.Items.Count, n, at => d.Items.Set(at, i)),
            r => IfAny(r, r.Items.Count, n, at => r with { Items = r.Items.SetItem(at, i) }),
            n)),
        Arbitrary.Seed.Select(n => new Edit(
            "Items",
            $"Items.RemoveAt(#{n})",
            d => IfAny(d.Items.Count, n, d.Items.RemoveAt),
            r => IfAny(r, r.Items.Count, n, at => r with { Items = r.Items.RemoveAt(at) }))),
        Gen.Select(Arbitrary.Seed, Arbitrary.Text, (n, s) => new Edit(
            "Items",
            $"Items[#{n}].Title = {s}",
            d => IfAny(d.Items.Count, n, at => d.Items[at].Title = s),
            r => IfAny(r, r.Items.Count, n, at => r with { Items = r.Items.SetItem(at, r.Items[at] with { Title = s }) }),
            n)),
        Gen.Select(Arbitrary.Seed, Arbitrary.Text, (n, s) => new Edit(
            "Items",
            $"Items[#{n}].Label.Name = {s}",
            d => IfAny(d.Items.Count, n, at => d.Items[at].Label.Name = s),
            r => IfAny(r, r.Items.Count, n, at => r with { Items = r.Items.SetItem(at, r.Items[at] with { Label = r.Items[at].Label with { Name = s } }) }),
            n)),
        Arbitrary.Small.Select(v => new Edit(
            "Items",
            $"Items.RemoveAll(Id == {v})",
            d => d.Items.RemoveAll(x => x.Id == v),
            r => r with { Items = r.Items.RemoveAll(x => x.Id == v) })),
        Gen.Const(new Edit("Items", "Items.Clear()", d => d.Items.Clear(), r => r with { Items = r.Items.Clear() })),
        Arbitrary.Items.Select(l => new Edit(
            "Items", $"Items = new({Show(l)})", d => d.Items = new(l, static x => new Item.Draft(x)), r => r with { Items = l })),
    ];

    private static IEnumerable<Gen<Edit>> CardArray() =>
    [
        Arbitrary.Item.Select(i => new Edit("Cards", $"Cards.Add({Show(i)})", d => d.Cards.Add(i), r => r with { Cards = r.Cards.Add(i) })),
        Gen.Select(Arbitrary.Seed, Arbitrary.Item, (n, i) => new Edit(
            "Cards",
            $"Cards.Insert(#{n}, {Show(i)})",
            d => d.Cards.Insert(At(n, d.Cards.Count + 1), i),
            r => r with { Cards = r.Cards.Insert(At(n, r.Cards.Length + 1), i) })),
        Arbitrary.Seed.Select(n => new Edit(
            "Cards",
            $"Cards.RemoveAt(#{n})",
            d => IfAny(d.Cards.Count, n, d.Cards.RemoveAt),
            r => IfAny(r, r.Cards.Length, n, at => r with { Cards = r.Cards.RemoveAt(at) }))),
        Gen.Select(Arbitrary.Seed, Arbitrary.Text, (n, s) => new Edit(
            "Cards",
            $"Cards[#{n}].Title = {s}",
            d => IfAny(d.Cards.Count, n, at => d.Cards[at].Title = s),
            r => IfAny(r, r.Cards.Length, n, at => r with { Cards = r.Cards.SetItem(at, r.Cards[at] with { Title = s }) }),
            n)),
        Gen.Select(Arbitrary.Seed, Arbitrary.Tag.Null(0.5), (n, t) => new Edit(
            "Cards",
            $"Cards[#{n}].Extra = {t?.Name ?? "null"}",
            d => IfAny(d.Cards.Count, n, at => d.Cards[at].Extra = t),
            r => IfAny(r, r.Cards.Length, n, at => r with { Cards = r.Cards.SetItem(at, r.Cards[at] with { Extra = t }) }),
            n)),
        Arbitrary.Cards.Select(a => new Edit(
            "Cards", $"Cards = new({Show(a)})", d => d.Cards = new(a, static x => new Item.Draft(x)), r => r with { Cards = a })),
    ];

    private static IEnumerable<Gen<Edit>> NumberArray() =>
    [
        Arbitrary.Small.Select(v => new Edit("Numbers", $"Numbers.Add({v})", d => d.Numbers.Add(v), r => r with { Numbers = r.Numbers.Add(v) })),
        Arbitrary.Small.Array[0, 3].Select(vs => new Edit(
            "Numbers", $"Numbers.AddRange({Show(vs)})", d => d.Numbers.AddRange(vs), r => r with { Numbers = r.Numbers.AddRange(vs) })),
        Gen.Select(Arbitrary.Seed, Arbitrary.Small, (n, v) => new Edit(
            "Numbers",
            $"Numbers[#{n}] = {v}",
            d => IfAny(d.Numbers.Count, n, at => d.Numbers[at] = v),
            r => IfAny(r, r.Numbers.Length, n, at => r with { Numbers = r.Numbers.SetItem(at, v) }))),
        Arbitrary.Seed.Select(n => new Edit(
            "Numbers",
            $"Numbers.RemoveAt(#{n})",
            d => IfAny(d.Numbers.Count, n, d.Numbers.RemoveAt),
            r => IfAny(r, r.Numbers.Length, n, at => r with { Numbers = r.Numbers.RemoveAt(at) }))),
        Arbitrary.Small.Select(v => new Edit(
            "Numbers", $"Numbers.RemoveAll(== {v})", d => d.Numbers.RemoveAll(x => x == v), r => r with { Numbers = r.Numbers.RemoveAll(x => x == v) })),
        Gen.Const(new Edit("Numbers", "Numbers.Sort()", d => d.Numbers.Sort((x, y) => x.CompareTo(y)), r => r with { Numbers = r.Numbers.Sort() })),
        Arbitrary.Numbers.Select(a => new Edit("Numbers", $"Numbers = {Show(a)}", d => d.Numbers = a, r => r with { Numbers = a })),

        // No-op ImmutableArray<T> edits: an element set to its own value, and the member set to its own current array.
        Arbitrary.Seed.Select(n => new Edit("(no-op Numbers)", $"Numbers[#{n}] = itself", d => IfAny(d.Numbers.Count, n, at => d.Numbers[at] = d.Numbers[at]), r => r)),
        Gen.Const(new Edit("(no-op Numbers)", "Numbers = Numbers.BuildArray()", d => d.Numbers = d.Numbers.BuildArray(), r => r)),
    ];

    private static IEnumerable<Gen<Edit>> HoleList() =>
    [
        Arbitrary.Item.Null(0.5).Select(i => new Edit("Holes", $"Holes.Add({Show(i)})", d => d.Holes.Add(i), r => r with { Holes = r.Holes.Add(i) })),
        Gen.Select(Arbitrary.Seed, Arbitrary.Item.Null(0.5), (n, i) => new Edit(
            "Holes",
            $"Holes[#{n}] = {Show(i)}",
            d => IfAny(d.Holes.Count, n, at => d.Holes[at] = i),
            r => IfAny(r, r.Holes.Count, n, at => r with { Holes = r.Holes.SetItem(at, i) }))),
        Arbitrary.Seed.Select(n => new Edit(
            "Holes",
            $"Holes.RemoveAt(#{n})",
            d => IfAny(d.Holes.Count, n, d.Holes.RemoveAt),
            r => IfAny(r, r.Holes.Count, n, at => r with { Holes = r.Holes.RemoveAt(at) }))),
        Arbitrary.Holes.Select(l => new Edit("Holes", $"Holes = {Show(l)}", d => d.Holes = l, r => r with { Holes = l })),
    ];

    private static IEnumerable<Gen<Edit>> Dictionaries() =>
    [
        Gen.Select(Arbitrary.Key, Arbitrary.Item, (k, i) => new Edit(
            "ById", $"ById.Set({k}, {Show(i)})", d => d.ById.Set(k, i), r => r with { ById = r.ById.SetItem(k, i) })),
        Arbitrary.Key.Select(k => new Edit("ById", $"ById.Remove({k})", d => d.ById.Remove(k), r => r with { ById = r.ById.Remove(k) })),

        // An element edit: the flush keeps the stored key (SetItem(k) would rename it to k under OrdinalIgnoreCase) and the
        // edit even when the ValueComparer calls it equal, so the manual side replaces the entry under the stored key.
        Gen.Select(Arbitrary.Key, Arbitrary.Text, (k, s) => new Edit(
            "ById",
            $"ById[{k}]?.Title = {s}",
            d =>
            {
                if (d.ById.TryGetValue(k, out var value))
                {
                    value.Title = s;
                }
            },
            r => r.ById.TryGetKey(k, out var stored) ? r with { ById = r.ById.Remove(stored).Add(stored, r.ById[stored] with { Title = s }) } : r)),
        Gen.Const(new Edit("ById", "ById.Clear()", d => d.ById.Clear(), r => r with { ById = r.ById.Clear() })),
        Arbitrary.ById.Select(m => new Edit(
            "ById", $"ById = new({Show(m)})", d => d.ById = new(m, static x => new Item.Draft(x)), r => r with { ById = m })),
        Gen.Select(Arbitrary.Key, Arbitrary.Small, (k, v) => new Edit(
            "Counts", $"Counts[{k}] = {v}", d => d.Counts[k] = v, r => r with { Counts = r.Counts.SetItem(k, v) })),
        Arbitrary.Key.Select(k => new Edit("Counts", $"Counts.Remove({k})", d => d.Counts.Remove(k), r => r with { Counts = r.Counts.Remove(k) })),
        Gen.Const(new Edit("Counts", "Counts.Clear()", d => d.Counts.Clear(), r => r with { Counts = r.Counts.Clear() })),
        Arbitrary.Counts.Select(m => new Edit("Counts", $"Counts = {Show(m)}", d => d.Counts = m, r => r with { Counts = m })),
    ];

    private static IEnumerable<Gen<Edit>> Sets() =>
    [
        Arbitrary.Key.Select(k => new Edit("Tags", $"Tags.Add({k})", d => d.Tags.Add(k), r => r with { Tags = r.Tags.Add(k) })),
        Arbitrary.Key.Select(k => new Edit("Tags", $"Tags.Remove({k})", d => d.Tags.Remove(k), r => r with { Tags = r.Tags.Remove(k) })),
        Arbitrary.Tags.Select(s => new Edit("Tags", $"Tags = {Show(s)}", d => d.Tags = s, r => r with { Tags = s })),
        Arbitrary.Small.Select(v => new Edit("Ranks", $"Ranks.Add({v})", d => d.Ranks.Add(v), r => r with { Ranks = r.Ranks.Add(v) })),
        Arbitrary.Small.Select(v => new Edit("Ranks", $"Ranks.Remove({v})", d => d.Ranks.Remove(v), r => r with { Ranks = r.Ranks.Remove(v) })),
        Arbitrary.Ranks.Select(s => new Edit("Ranks", $"Ranks = {Show(s)}", d => d.Ranks = s, r => r with { Ranks = s })),
    ];

    private static void IfAny(int count, int seed, Action<int> edit)
    {
        if (count > 0)
        {
            edit(At(seed, count));
        }
    }

    private static Root IfAny(Root r, int count, int seed, Func<int, Root> edit) => count > 0 ? edit(At(seed, count)) : r;

    private static string Show(Item? i) => i is null ? "null" : $"Item({i.Id}, {i.Title}, {i.Label.Name}, {i.Extra?.Name ?? "null"})";

    private static string Show<T>(IEnumerable<T>? values) => values is null ? "null" : $"[{string.Join(", ", values)}]";
}

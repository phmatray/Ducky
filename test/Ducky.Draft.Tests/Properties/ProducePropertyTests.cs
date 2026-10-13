using System.Collections.Immutable;
using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Draft.Tests.Properties;

// INV-26 (SPEC §13.3, §17.7) on random nested records and random edit scripts, through the generated Root.Draft.
public sealed class ProducePropertyTests
{
    private const int EqualAssignments = 14;

    private const int StructuralOps = 5;

    private static readonly string[] _members = ["Count", "Name", "Main", "Spare", "Items", "Cards", "Numbers", "Holes", "ById", "Counts", "Tags", "Ranks", "Names"];

    private static readonly Gen<(Root Root, Edit[] Edits)> _scripts = Gen.Select(Arbitrary.Root, Edits.Script);

    [Fact]
    public void Produce_EqualsManualWithExpressions() =>
        Property.Check(_scripts, input => Structural.ShouldMatch(Edits.Produce(input.Root, input.Edits), Edits.Manual(input.Root, input.Edits)));

    // Every member no edit wrote keeps its reference (an ImmutableArray<T> its underlying array), and so do the members of
    // the nested Main and the elements of a list that only saw element edits.
    [Fact]
    public void Produce_UntouchedBranches_ReferenceEqual() =>
        Property.Check(_scripts, input =>
        {
            var (root, edits) = input;
            var result = Edits.Produce(root, edits);

            foreach (var member in _members.Concat(["Main.Title", "Main.Label", "Main.Extra"]).Where(m => !Touched(edits, m)))
            {
                SameBranch(result, root, member).ShouldBeTrue(member);
            }

            UntouchedElements(edits, "Items", root.Items, result.Items);
            UntouchedElements(edits, "Cards", root.Cards, result.Cards);
        });

    [Fact]
    public void Produce_BaseDeepEqualBeforeAndAfter() =>
        Property.Check(_scripts, input =>
        {
            var (root, edits) = input;
            var before = Arbitrary.Clone(root);

            _ = Edits.Produce(root, edits);

            Structural.ShouldMatch(root, before);
        });

    // After Produce returns, the draft and every member, nested, collection and element draft it handed out throw. Drafts of
    // records from another assembly: the test of the same name in Ducky.Draft.Generators.Tests (Lib and App compilations).
    [Fact]
    public void Produce_RevokedDraftAccess_Throws() =>
        Property.Check(Gen.Select(Arbitrary.Root, Edits.Script, Arbitrary.Item), input =>
        {
            var (root, edits, item) = input;
            List<(string Name, Action Access)> accesses = [];

            _ = root.Produce(d =>
            {
                foreach (var edit in edits)
                {
                    edit.Apply(d);
                }

                accesses.AddRange(Accesses(d, item));
            });

            foreach (var (name, access) in accesses)
            {
                Should.Throw<ObjectDisposedException>(access, name);
            }
        });

    // Writing a value equal to the member's (a fresh string, the same int), directly or after a different value (a revert),
    // on scalars of the root, of nested drafts and of element drafts, and equal values into collections, returns the base.
    [Fact]
    public void Produce_AssignEqualScalar_IsNoOp() =>
        Property.Check(Gen.Select(Arbitrary.Root, Gen.Select(Gen.Int[0, EqualAssignments - 1], Arbitrary.Seed, Gen.Bool).Array[0, 16]), input =>
        {
            var (root, assignments) = input;

            var result = root.Produce(d =>
            {
                foreach (var (kind, seed, revert) in assignments)
                {
                    AssignEqual(root, d, kind, seed, revert);
                }
            });

            result.ShouldBeSameAs(root);
        });

    // A structural list operation (insert, remove, clear) flushes the cached element drafts and revokes them; replacing an
    // element revokes its draft. The edit made before the operation survives; a write through the stale draft throws.
    [Fact]
    public void Produce_ElementDraftAfterStructuralOp_Throws() =>
        Property.Check(Gen.Select(Arbitrary.Root, Arbitrary.Item, Gen.Int[0, StructuralOps - 1], Arbitrary.Seed, Arbitrary.Seed, Arbitrary.Text), input =>
        {
            var (root, item, op, seed, other, title) = input;
            var r = root with { Items = root.Items.Insert(0, item), Cards = root.Cards.Insert(0, item) };
            var at = Edits.At(seed, r.Items.Count);
            var expected = StructuralOp(r with { Items = r.Items.SetItem(at, r.Items[at] with { Title = title }) }, op, at, other, item);

            var result = r.Produce(d =>
            {
                var stale = d.Items[at];
                stale.Title = title;
                StructuralOp(d, op, at, other, item);
                Should.Throw<ObjectDisposedException>(() => stale.Title = "lost");
                var card = d.Cards[Edits.At(seed, d.Cards.Count)];
                d.Cards.RemoveAt(Edits.At(other, d.Cards.Count));
                Should.Throw<ObjectDisposedException>(() => card.Title = "lost");
            });

            Structural.ShouldMatch(result, expected with { Cards = r.Cards.RemoveAt(Edits.At(other, r.Cards.Length)) });
        });

    // Assigning a record, a nested draft, null or a collection revokes the draft it replaces; the same instance is kept.
    [Fact]
    public void Produce_ReplacedChildDraft_Throws() =>
        Property.Check(Gen.Select(Arbitrary.Root, Edits.Script, Arbitrary.Item, Arbitrary.Text), input =>
        {
            var (root, edits, item, title) = input;

            var result = root.Produce(d =>
            {
                foreach (var edit in edits)
                {
                    edit.Apply(d);
                }

                foreach (var (name, replace, write) in Replacements(d, item))
                {
                    replace();
                    Should.Throw<ObjectDisposedException>(write, name);
                }

                var kept = d.Main;
                d.Main = kept;
                kept.Title = title;
            });

            Structural.ShouldMatch(result, ReplacedManually(Edits.Manual(root, edits), item, title));
        });

    private static bool Touched(Edit[] edits, string path) =>
        edits.Any(e => e.Path == path || e.Path.StartsWith(path + ".", StringComparison.Ordinal) || path.StartsWith(e.Path + ".", StringComparison.Ordinal));

    private static bool SameBranch(Root result, Root root, string member) => member switch
    {
        "Count" => result.Count == root.Count,
        "Name" => ReferenceEquals(result.Name, root.Name),
        "Main" => ReferenceEquals(result.Main, root.Main),
        "Spare" => ReferenceEquals(result.Spare, root.Spare),
        "Items" => ReferenceEquals(result.Items, root.Items),
        "Cards" => result.Cards == root.Cards,
        "Numbers" => result.Numbers == root.Numbers,
        "Holes" => ReferenceEquals(result.Holes, root.Holes),
        "ById" => ReferenceEquals(result.ById, root.ById),
        "Counts" => ReferenceEquals(result.Counts, root.Counts),
        "Tags" => ReferenceEquals(result.Tags, root.Tags),
        "Ranks" => ReferenceEquals(result.Ranks, root.Ranks),
        "Names" => ReferenceEquals(result.Names, root.Names),
        "Main.Title" => ReferenceEquals(result.Main.Title, root.Main.Title),
        "Main.Label" => ReferenceEquals(result.Main.Label, root.Main.Label),
        "Main.Extra" => ReferenceEquals(result.Main.Extra, root.Main.Extra),
        _ => throw new ArgumentOutOfRangeException(nameof(member), member, null),
    };

    // Element-only edits (no index shift) on a list leave every element they did not address reference-equal.
    private static void UntouchedElements(Edit[] edits, string member, IReadOnlyList<Item> before, IReadOnlyList<Item> after)
    {
        var listEdits = edits.Where(e => e.Path == member).ToList();
        if (before.Count == 0 || listEdits.Any(e => e.Element is null))
        {
            return;
        }

        var addressed = listEdits.Select(e => Edits.At(e.Element!.Value, before.Count)).ToHashSet();
        foreach (var index in Enumerable.Range(0, before.Count).Where(i => !addressed.Contains(i)))
        {
            after[index].ShouldBeSameAs(before[index], $"{member}[{index}]");
        }
    }

    private static List<(string, Action)> Accesses(Root.Draft d, Item item)
    {
        IDraft<Root> draft = d;
        var main = d.Main;
        var label = main.Label;
        var items = d.Items;
        var cards = d.Cards;
        var numbers = d.Numbers;
        var holes = d.Holes;
        var byId = d.ById;
        var counts = d.Counts;
        var tags = d.Tags;
        var ranks = d.Ranks;
        List<(string, Action)> accesses =
        [
            ("root read", () => _ = d.Count),
            ("root write", () => d.Name = "x"),
            ("root Build", () => draft.Build()),
            ("Main read", () => _ = main.Title),
            ("Main write", () => main.Title = "x"),
            ("Main.Label write", () => label.Name = "x"),
            ("Items read", () => _ = items.Count),
            ("Items write", () => items.Add(item)),
            ("Cards write", () => cards.Add(item)),
            ("Numbers read", () => _ = numbers.Count),
            ("Numbers write", () => numbers.Add(1)),
            ("Holes write", () => holes.Add(null)),
            ("ById read", () => _ = byId.Count),
            ("ById write", () => byId.Set("z", item)),
            ("Counts read", () => _ = counts.Count),
            ("Counts write", () => counts["z"] = 1),
            ("Tags write", () => tags.Add("z")),
            ("Ranks read", () => _ = ranks.Count),
            ("Ranks write", () => ranks.Add(9)),
        ];
        if (d.Spare is { } spare)
        {
            accesses.Add(("Spare write", () => spare.Title = "x"));
        }

        if (d.Names is { } names)
        {
            accesses.Add(("Names write", () => names.Add("x")));
        }

        if (items.Count > 0)
        {
            var element = items[0];
            accesses.Add(("Items[0] write", () => element.Title = "x"));
        }

        if (cards.Count > 0)
        {
            var element = cards[0];
            accesses.Add(("Cards[0] write", () => element.Extra = null));
        }

        if (byId.Keys.FirstOrDefault() is { } key)
        {
            var value = byId[key];
            accesses.Add(("ById value write", () => value.Title = "x"));
        }

        return accesses;
    }

    private static void AssignEqual(Root root, Root.Draft d, int kind, int seed, bool revert)
    {
        switch (kind)
        {
            case 0:
                Revert(revert, () => d.Count = seed);
                d.Count = root.Count;
                break;
            case 1:
                Revert(revert, () => d.Name = "other");
                d.Name = Arbitrary.CopyOrNull(root.Name);
                break;
            case 2:
                Revert(revert, () => d.Main.Title = "other");
                d.Main.Title = Arbitrary.Copy(root.Main.Title);
                break;
            case 3:
                Revert(revert, () => d.Main.Id = seed);
                d.Main.Id = root.Main.Id;
                break;
            case 4:
                Revert(revert, () => d.Main.Label.Name = "other");
                d.Main.Label.Name = Arbitrary.Copy(root.Main.Label.Name);
                break;
            case 5:
                if (d.Spare is { } spare)
                {
                    Revert(revert, () => spare.Title = "other");
                    spare.Title = Arbitrary.Copy(root.Spare!.Title);
                }

                break;
            case 6:
                if (root.Items.Count > 0)
                {
                    var at = Edits.At(seed, root.Items.Count);
                    Revert(revert, () => d.Items[at].Title = "other");
                    d.Items[at].Title = Arbitrary.Copy(root.Items[at].Title);
                }

                break;
            case 7:
                if (root.Cards.Length > 0)
                {
                    var at = Edits.At(seed, root.Cards.Length);
                    Revert(revert, () => d.Cards[at].Id = seed);
                    d.Cards[at].Id = root.Cards[at].Id;
                }

                break;
            case 8:
                if (root.ById.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault() is { } key)
                {
                    Revert(revert, () => d.ById[key].Title = "other");
                    d.ById[key].Title = Arbitrary.Copy(root.ById[key].Title);
                }

                break;
            case 9:
                // Equal values written into collections (no revert: a mutated collection draft builds a new instance).
                if (root.Numbers.Length > 0)
                {
                    var at = Edits.At(seed, root.Numbers.Length);
                    d.Numbers[at] = root.Numbers[at];
                }

                break;
            case 10:
                foreach (var pair in root.Counts)
                {
                    d.Counts[Arbitrary.Copy(pair.Key)] = pair.Value;
                }

                break;
            case 11:
                foreach (var tag in root.Tags)
                {
                    d.Tags.Add(Arbitrary.Copy(tag));
                }

                break;
            case 12:
                if (root.Holes.Count > 0)
                {
                    var at = Edits.At(seed, root.Holes.Count);
                    d.Holes[at] = Arbitrary.CloneOrNull(root.Holes[at]);
                }

                break;
            default:
                d.Numbers = root.Numbers;
                foreach (var rank in root.Ranks)
                {
                    d.Ranks.Add(rank);
                }

                d.Ranks.Remove(-1);
                break;
        }
    }

    private static void Revert(bool revert, Action write)
    {
        if (revert)
        {
            write();
        }
    }

    // op: 0 Insert, 1 RemoveAt, 2 RemoveAll (matching the edited element's Id), 3 Clear, 4 Set of the edited index.
    private static void StructuralOp(Root.Draft d, int op, int at, int other, Item item)
    {
        var items = d.Items;
        switch (op)
        {
            case 0:
                items.Insert(Edits.At(other, items.Count + 1), item);
                break;
            case 1:
                items.RemoveAt(Edits.At(other, items.Count));
                break;
            case 2:
                {
                    var id = items[at].Id;
                    items.RemoveAll(x => x.Id == id);
                    break;
                }

            case 3:
                items.Clear();
                break;
            default:
                items.Set(at, item);
                break;
        }
    }

    private static Root StructuralOp(Root r, int op, int at, int other, Item item) => op switch
    {
        0 => r with { Items = r.Items.Insert(Edits.At(other, r.Items.Count + 1), item) },
        1 => r with { Items = r.Items.RemoveAt(Edits.At(other, r.Items.Count)) },
        2 => r with { Items = r.Items.RemoveAll(x => x.Id == r.Items[at].Id) },
        3 => r with { Items = r.Items.Clear() },
        _ => r with { Items = r.Items.SetItem(at, item) },
    };

    private static IEnumerable<(string, Action, Action)> Replacements(Root.Draft d, Item item)
    {
        Item.Draft? child = null;
        Tag.Draft? label = null;
        Item.Draft? spare = null;
        ListDraft<Item, Item.Draft>? items = null;
        ListDraft<int>? numbers = null;
        Item.Draft? byId = null;
        DictionaryDraft<string, int>? counts = null;
        SetDraft<string>? tags = null;
        ListDraft<string>? names = null;
        return
        [
            ("Main by record", () => { child = d.Main; d.Main = item; }, () => child!.Title = "lost"),
            ("Main by draft", () => { child = d.Main; d.Main = item.CreateDraft(); }, () => child!.Title = "lost"),
            ("Main.Label by record", () => { label = d.Main.Label; d.Main.Label = new Tag("label"); }, () => label!.Name = "lost"),
            ("Spare by null", () => { d.Spare = item; spare = d.Spare; d.Spare = null; }, () => spare!.Title = "lost"),
            ("Items by new list", () => { items = d.Items; d.Items = new(ImmutableList.Create(item), static x => new Item.Draft(x)); }, () => items!.Add(item)),
            ("Numbers by conversion", () => { numbers = d.Numbers; d.Numbers = ImmutableArray.Create(7); }, () => numbers!.Add(1)),
            ("ById value by Set(case-variant key)", () =>
            {
                d.ById = new(CaseInsensitiveById(item), static x => new Item.Draft(x));
                byId = d.ById["q"];
                d.ById.Set("Q", item);
            }, () => byId!.Title = "lost"),
            ("Counts by conversion", () => { counts = d.Counts; d.Counts = ImmutableDictionary<string, int>.Empty; }, () => counts!["z"] = 1),
            ("Tags by conversion", () => { tags = d.Tags; d.Tags = ImmutableHashSet<string>.Empty; }, () => tags!.Add("z")),
            ("Names by null", () => { d.Names = ImmutableList.Create("n"); names = d.Names; d.Names = null; }, () => names!.Add("lost")),
        ];
    }

    private static Root ReplacedManually(Root r, Item item, string title) => r with
    {
        Main = item with { Label = new Tag("label"), Title = title },
        Spare = null,
        Items = [item],
        Numbers = [7],
        ById = CaseInsensitiveById(item).SetItem("Q", item),
        Counts = ImmutableDictionary<string, int>.Empty,
        Tags = ImmutableHashSet<string>.Empty,
        Names = null,
    };

    private static ImmutableDictionary<string, Item> CaseInsensitiveById(Item item) =>
        ImmutableDictionary.Create<string, Item>(StringComparer.OrdinalIgnoreCase).Add("q", item);
}

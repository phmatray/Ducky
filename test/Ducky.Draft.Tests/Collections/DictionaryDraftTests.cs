using System.Collections;
using System.Collections.Immutable;
using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Draft.Tests.Collections;

// SPEC §13.2 (DictionaryDraft<K,V>, DictionaryDraft<K,V,TDraft>), §13.3, INV-26. Non-normative tests of story M10-02.
public sealed class DictionaryDraftTests
{
    private static readonly Todo _a = new(1, "a");
    private static readonly Todo _b = new(2, "b");

    [Fact]
    public void DictionaryDraft_Clean_BuildReturnsSource()
    {
        var source = ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, [KeyValuePair.Create("a", 1)]);
        DictionaryDraft<string, int> draft = source;

        draft["A"] = 1; // an equal set is not a change
        draft.Remove("missing").ShouldBeFalse();
        ((ICollection<KeyValuePair<string, int>>)draft).Remove(KeyValuePair.Create("a", 2)).ShouldBeFalse();
        draft.Add("a", 1); // an equal add is not a change

        draft.IsDirty.ShouldBeFalse();
        draft.Build().ShouldBeSameAs(source);

        DictionaryDraft<string, int> empty = ImmutableDictionary<string, int>.Empty;
        empty.Clear();
        empty.IsDirty.ShouldBeFalse();

        // A dirty draft builds a new instance even when its contents end up equal, through the source's comparers.
        draft.Remove("a").ShouldBeTrue();
        draft["a"] = 1;
        draft.IsDirty.ShouldBeTrue();
        var built = draft.Build();
        built.ShouldNotBeSameAs(source);
        built.KeyComparer.ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
        built.ShouldBe(source);
    }

    [Fact]
    public void DictionaryDraft_Members_ReadAndWrite()
    {
        var source = ImmutableDictionary.CreateRange([KeyValuePair.Create(1, "x"), KeyValuePair.Create(2, "y")]);
        DictionaryDraft<int, string> draft = source;
        IDictionary<int, string> dictionary = draft;
        ICollection<KeyValuePair<int, string>> pairs = draft;

        draft.Count.ShouldBe(2);
        draft[1].ShouldBe("x");
        draft.ContainsKey(2).ShouldBeTrue();
        draft.TryGetValue(2, out var y).ShouldBeTrue();
        y.ShouldBe("y");
        draft.TryGetValue(3, out _).ShouldBeFalse();
        pairs.IsReadOnly.ShouldBeFalse();
        pairs.Contains(KeyValuePair.Create(1, "x")).ShouldBeTrue();
        pairs.Contains(KeyValuePair.Create(1, "y")).ShouldBeFalse();
        dictionary.Keys.ShouldBe([1, 2], ignoreOrder: true);
        dictionary.Values.ShouldBe(["x", "y"], ignoreOrder: true);
        ((IReadOnlyDictionary<int, string>)draft).Keys.ShouldBe([1, 2], ignoreOrder: true);
        ((IReadOnlyDictionary<int, string>)draft).Values.ShouldBe(["x", "y"], ignoreOrder: true);
        var copy = new KeyValuePair<int, string>[3];
        pairs.CopyTo(copy, 1);
        copy.Skip(1).ShouldBe(source, ignoreOrder: true);
        ((IEnumerable)draft).GetEnumerator().MoveNext().ShouldBeTrue();
        Should.Throw<KeyNotFoundException>(() => draft[3]);
        draft.IsDirty.ShouldBeFalse();

        pairs.Add(KeyValuePair.Create(3, "z"));
        draft[1] = "x2";
        pairs.Remove(KeyValuePair.Create(2, "y")).ShouldBeTrue();
        foreach (var pair in draft)
        {
            draft.Remove(pair.Key); // enumeration walks a snapshot
        }

        draft.Count.ShouldBe(0);
        draft.Add(4, "w");
        Should.Throw<ArgumentException>(() => draft.Add(4, "v"));
        draft.Build().ShouldBe([KeyValuePair.Create(4, "w")]);
        source.Count.ShouldBe(2);
    }

    [Fact]
    public void DictionaryDraft_Revoked_Throws()
    {
        DictionaryDraft<int, string> draft = ImmutableDictionary<int, string>.Empty.Add(1, "x");
        IDictionary<int, string> dictionary = draft;
        ICollection<KeyValuePair<int, string>> pairs = draft;

        ((IRevocable)draft).Revoke();

        Action[] members =
        [
            () => _ = draft.IsDirty,
            () => _ = draft.Count,
            () => _ = draft[1],
            () => draft[1] = "y",
            () => _ = dictionary.Keys,
            () => _ = dictionary.Values,
            () => _ = ((IReadOnlyDictionary<int, string>)draft).Keys,
            () => _ = ((IReadOnlyDictionary<int, string>)draft).Values,
            () => _ = pairs.IsReadOnly,
            () => draft.ContainsKey(1),
            () => draft.TryGetValue(1, out _),
            () => pairs.Contains(KeyValuePair.Create(1, "x")),
            () => pairs.CopyTo(new KeyValuePair<int, string>[1], 0),
            () => draft.GetEnumerator(),
            () => draft.Add(2, "y"),
            () => pairs.Add(KeyValuePair.Create(2, "y")),
            () => draft.Remove(1),
            () => pairs.Remove(KeyValuePair.Create(1, "x")),
            () => draft.Clear(),
            () => draft.Build(),
        ];

        foreach (var member in members)
        {
            Should.Throw<ObjectDisposedException>(member).Message.ShouldContain("draft revoked");
        }

        Should.Throw<ArgumentNullException>(() => _ = (DictionaryDraft<int, string>)(ImmutableDictionary<int, string>)null!)
            .ParamName.ShouldBe("source");
    }

    // A random edit script over a dictionary (default or case-insensitive-style key comparer) against a Dictionary model:
    // after each edit the draft reads as the model; at the end Build equals the model with the source's comparers, the source
    // is unchanged, and a draft whose edits never changed the contents is clean and returns its own source.
    [Fact]
    public void DictionaryDraft_ModelBased()
    {
        var edit = Gen.Select(Gen.Int[0, 6], Gen.Int[0, 7], Gen.Int[0, 3]);
        var cases = Gen.Select(Gen.Select(Gen.Int[0, 7], Gen.Int[0, 3]).List[0, 6], Gen.Bool, edit.List[0, 24]);
        Property.Check(cases, run =>
        {
            var (initial, modComparer, edits) = run;
            IEqualityComparer<int> comparer = modComparer ? new Mod4Comparer() : EqualityComparer<int>.Default;
            var source = ImmutableDictionary<int, int>.Empty.WithComparers(comparer).SetItems(initial.Select(p => KeyValuePair.Create(p.Item1, p.Item2)));
            DictionaryDraft<int, int> draft = source;
            var model = new Dictionary<int, int>(source, comparer);
            var snapshot = source.ToArray();
            var dirty = false;

            foreach (var (op, key, value) in edits)
            {
                var before = model.ToArray();
                Apply(draft, model, op, key, value);
                dirty |= model.Count != before.Length || before.Any(p => !model.TryGetValue(p.Key, out var v) || v != p.Value);
                ShouldMatch(draft, model);
            }

            var built = draft.Build();
            ShouldMatch(built, model);
            built.KeyComparer.ShouldBeSameAs(source.KeyComparer);
            built.ValueComparer.ShouldBeSameAs(source.ValueComparer);
            if (!dirty)
            {
                draft.IsDirty.ShouldBeFalse();
                built.ShouldBeSameAs(source);
            }

            source.ShouldBe(snapshot);
        });
    }

    // Compared through the comparer: a set with an equivalent key may keep either key instance.
    private static void ShouldMatch(IReadOnlyDictionary<int, int> actual, Dictionary<int, int> model)
    {
        actual.Count.ShouldBe(model.Count);
        foreach (var (key, value) in model)
        {
            actual.TryGetValue(key, out var got).ShouldBeTrue();
            got.ShouldBe(value);
        }
    }

    private static void Apply(DictionaryDraft<int, int> draft, Dictionary<int, int> model, int op, int key, int value)
    {
        switch (op)
        {
            case 0:
                draft[key] = value;
                model[key] = value;
                break;
            case 1 when !model.ContainsKey(key):
                draft.Add(key, value);
                model.Add(key, value);
                break;
            case 2:
                draft.Remove(key).ShouldBe(model.Remove(key));
                break;
            case 3:
                var pair = KeyValuePair.Create(key, value);
                ((ICollection<KeyValuePair<int, int>>)draft).Remove(pair).ShouldBe(((ICollection<KeyValuePair<int, int>>)model).Remove(pair));
                break;
            case 4 when key == 0:
                draft.Clear();
                model.Clear();
                break;
            default:
                draft.TryGetValue(key, out var got).ShouldBe(model.TryGetValue(key, out var expected));
                got.ShouldBe(expected);
                break;
        }
    }

    [Fact]
    public void DictionaryDraft_ElementDrafts_FlushAndRevoke()
    {
        var source = ImmutableDictionary.CreateRange(StringComparer.OrdinalIgnoreCase, [KeyValuePair.Create("a", _a), KeyValuePair.Create("b", _b)]);
        var created = 0;
        var draft = new DictionaryDraft<string, Todo, TodoDraft>(source, x =>
        {
            created++;
            return new TodoDraft(x);
        });

        created.ShouldBe(0); // lazy
        draft.Count.ShouldBe(2);
        draft.ContainsKey("A").ShouldBeTrue();
        var a = draft["A"];
        draft["a"].ShouldBeSameAs(a); // cached through the source's key comparer
        a.Title = "a2";
        a.Title = "a"; // reverted
        draft.TryGetValue("b", out var b).ShouldBeTrue();
        draft.TryGetValue("c", out _).ShouldBeFalse();
        created.ShouldBe(2);
        draft.IsDirty.ShouldBeFalse(); // reverted element drafts are clean
        draft.Build().ShouldBeSameAs(source); // and build the base

        b!.Title = "b2";
        draft.IsDirty.ShouldBeTrue();
        var built = draft.Build();
        built["a"].ShouldBeSameAs(_a);
        built["b"].ShouldBe(_b with { Title = "b2" });
        built.KeyComparer.ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
        b.Title = "b3"; // Build flushes without revoking
        draft.Build()["b"].Title.ShouldBe("b3");
        a.Title = "a2"; // read as "A": flushed under the stored key "a", as a with-edit would leave it
        draft.Build().Keys.ShouldBe(["a", "b"], ignoreOrder: true);

        draft.Set("a", _b); // a replaced value's cached draft is revoked
        Should.Throw<ObjectDisposedException>(() => a.Title = "x");
        draft["a"].ShouldNotBeSameAs(a);
        draft.Remove("b").ShouldBeTrue();
        Should.Throw<ObjectDisposedException>(() => b.Title = "x");
        draft.Remove("b").ShouldBeFalse();
        draft.Set("c", _a);
        draft.IsDirty.ShouldBeTrue(); // the values changed, no element draft cached
        draft.Keys.ShouldBe(["a", "c"], ignoreOrder: true);
        draft.Values.Select(t => t.Title).ShouldBe(["b", "a"], ignoreOrder: true);
        draft.Select(p => p.Key).ShouldBe(["a", "c"], ignoreOrder: true);
        ((IEnumerable)draft).GetEnumerator().MoveNext().ShouldBeTrue();
        var c = draft["c"];
        draft.Clear();
        Should.Throw<ObjectDisposedException>(() => c.Title = "x");
        draft.Build().ShouldBeEmpty();
        source.Count.ShouldBe(2);
    }

    // INV-26: Build writes an element draft's result by reference, so a ValueComparer that ignores the edited member (an
    // entity compared by Id, §13.3 keeps the source's ValueComparer) still gets the edit.
    [Fact]
    public void DictionaryDraft_ElementDrafts_ValueComparer_EditKept()
    {
        var byId = EqualityComparer<Todo>.Create((x, y) => x?.Id == y?.Id, x => x.Id);
        var source = ImmutableDictionary.Create<string, Todo>(StringComparer.Ordinal, byId).Add("a", _a);
        var draft = new DictionaryDraft<string, Todo, TodoDraft>(source, x => new TodoDraft(x));

        draft["a"].Title = "a2";

        draft.IsDirty.ShouldBeTrue();
        var built = draft.Build();
        built["a"].Title.ShouldBe("a2");
        built.ValueComparer.ShouldBeSameAs(byId);
    }

    // A removal or Clear inside a foreach fails fast, as List<T> does, instead of a KeyNotFoundException on a removed key;
    // edits through the visited drafts and Set are fine.
    [Fact]
    public void DictionaryDraft_ElementDrafts_StructuralOpDuringEnumeration_Throws()
    {
        var source = ImmutableDictionary.CreateRange([KeyValuePair.Create(1, _a), KeyValuePair.Create(2, _b)]);
        var draft = new DictionaryDraft<int, Todo, TodoDraft>(source, x => new TodoDraft(x));

        foreach (var (key, todo) in draft)
        {
            todo.Title += "!";
            draft.Set(3, _a);
            draft.Remove(4).ShouldBeFalse(); // removing a missing key is no change
        }

        draft.Build().Values.Select(t => t.Title).ShouldBe(["a!", "b!", "a"], ignoreOrder: true);
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var (key, _) in draft)
            {
                draft.Remove(key);
            }
        }).Message.ShouldContain("modified");
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var _ in draft.Values)
            {
                draft.Clear();
            }
        });
    }

    [Fact]
    public void DictionaryDraft_ElementDrafts_NullValueNotDrafted()
    {
        var source = ImmutableDictionary.CreateRange([KeyValuePair.Create(1, (Todo)null!), KeyValuePair.Create(2, _a)]);
        var created = 0;
        var draft = new DictionaryDraft<int, Todo, TodoDraft>(source, x =>
        {
            created++;
            return new TodoDraft(x);
        });

        draft[1].ShouldBeNull();
        draft.TryGetValue(1, out var none).ShouldBeTrue();
        none.ShouldBeNull();
        created.ShouldBe(0);
        draft.IsDirty.ShouldBeFalse();
        draft.Build().ShouldBeSameAs(source);
        Should.Throw<KeyNotFoundException>(() => draft[3]);
    }

    [Fact]
    public void DictionaryDraft_ElementDrafts_Revoked_Throws()
    {
        var draft = new DictionaryDraft<int, Todo, TodoDraft>(ImmutableDictionary<int, Todo>.Empty.Add(1, _a), x => new TodoDraft(x));
        var a = draft[1];
        Should.Throw<ArgumentNullException>(() => new DictionaryDraft<int, Todo, TodoDraft>(null!, x => new TodoDraft(x)))
            .ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => new DictionaryDraft<int, Todo, TodoDraft>(ImmutableDictionary<int, Todo>.Empty, null!))
            .ParamName.ShouldBe("createDraft");

        ((IRevocable)draft).Revoke();

        Should.Throw<ObjectDisposedException>(() => a.Title = "x");
        Action[] members =
        [
            () => _ = draft.IsDirty,
            () => _ = draft.Count,
            () => _ = draft[1],
            () => _ = draft.Keys,
            () => _ = draft.Values.First(),
            () => draft.ContainsKey(1),
            () => draft.TryGetValue(1, out _),
            () => draft.GetEnumerator(),
            () => draft.Set(2, _b),
            () => draft.Remove(1),
            () => draft.Clear(),
            () => draft.Build(),
        ];

        foreach (var member in members)
        {
            Should.Throw<ObjectDisposedException>(member);
        }
    }

    private sealed class Mod4Comparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % 4 == y % 4;

        public int GetHashCode(int obj) => obj % 4;
    }
}

using System.Collections;
using System.Collections.Immutable;
using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Draft.Tests.Collections;

// SPEC §13.2 (SetDraft<T>), §13.3, INV-26. Non-normative tests of story M10-02.
public sealed class SetDraftTests
{
    [Fact]
    public void SetDraft_Clean_BuildReturnsSource()
    {
        var hash = ImmutableHashSet.Create(StringComparer.OrdinalIgnoreCase, "a", "b");
        var sorted = ImmutableSortedSet.Create(StringComparer.Ordinal, "b", "a");
        SetDraft<string> fromHash = hash;
        SetDraft<string> fromSorted = sorted;

        foreach (var draft in new[] { fromHash, fromSorted })
        {
            draft.Add("a").ShouldBeFalse(); // adding a member is not a change
            draft.Remove("z").ShouldBeFalse();
            draft.UnionWith(["b"]);
            draft.IntersectWith(["a", "b", "c"]); // keeps everything: not a change
            draft.ExceptWith(["z"]);
            draft.SymmetricExceptWith([]);
            draft.IsDirty.ShouldBeFalse();
        }

        fromHash.Add("A").ShouldBeFalse(); // through the source's comparer
        fromHash.BuildHashSet().ShouldBeSameAs(hash);
        fromSorted.BuildSortedSet().ShouldBeSameAs(sorted);
        fromHash.BuildSortedSet().ShouldBe(["a", "b"]); // the other kind: a new instance of that kind
        fromSorted.BuildHashSet().ShouldBe(["a", "b"], ignoreOrder: true);

        SetDraft<string> empty = ImmutableHashSet<string>.Empty;
        empty.Clear();
        empty.IsDirty.ShouldBeFalse();
        empty.Add("a");
        empty.Clear(); // back to the shared empty set, which is the source itself: it builds the base
        empty.IsDirty.ShouldBeFalse();
        empty.BuildHashSet().ShouldBeSameAs(ImmutableHashSet<string>.Empty);

        // A dirty draft builds a new instance even when its contents end up equal, through the source's comparer.
        fromSorted.Remove("a").ShouldBeTrue();
        fromSorted.Add("a").ShouldBeTrue();
        fromSorted.IsDirty.ShouldBeTrue();
        fromSorted.BuildSortedSet().ShouldNotBeSameAs(sorted);
        fromSorted.BuildSortedSet().KeyComparer.ShouldBeSameAs(StringComparer.Ordinal);
        fromHash.SymmetricExceptWith(["b", "c"]); // same count, other contents: a change
        fromHash.IsDirty.ShouldBeTrue();
        fromHash.BuildHashSet().KeyComparer.ShouldBeSameAs(StringComparer.OrdinalIgnoreCase);
        fromHash.BuildHashSet().ShouldBe(["a", "c"], ignoreOrder: true);
        hash.ShouldBe(["a", "b"], ignoreOrder: true);
    }

    [Fact]
    public void SetDraft_Members_ReadAndWrite()
    {
        SetDraft<int> draft = ImmutableSortedSet.Create(1, 2, 3);
        ICollection<int> collection = draft;

        draft.Count.ShouldBe(3);
        collection.IsReadOnly.ShouldBeFalse();
        draft.Contains(2).ShouldBeTrue();
        ((IReadOnlySet<int>)draft).Contains(4).ShouldBeFalse();
        draft.IsSubsetOf([1, 2, 3]).ShouldBeTrue();
        draft.IsProperSubsetOf([1, 2, 3]).ShouldBeFalse();
        draft.IsSupersetOf([1, 2]).ShouldBeTrue();
        draft.IsProperSupersetOf([1, 2]).ShouldBeTrue();
        draft.Overlaps([3, 4]).ShouldBeTrue();
        draft.SetEquals([3, 2, 1]).ShouldBeTrue();
        var copy = new int[4];
        draft.CopyTo(copy, 1);
        copy.ShouldBe([0, 1, 2, 3]);
        ((IEnumerable)draft).GetEnumerator().MoveNext().ShouldBeTrue();

        collection.Add(4);
        foreach (var x in draft)
        {
            draft.Remove(x); // enumeration walks a snapshot
        }

        draft.Count.ShouldBe(0);
        draft.IsDirty.ShouldBeTrue();
        draft.BuildSortedSet().ShouldBeEmpty();
    }

    [Fact]
    public void SetDraft_Revoked_Throws()
    {
        SetDraft<int> draft = ImmutableHashSet.Create(1);
        ((IRevocable)draft).Revoke();

        Action[] members =
        [
            () => _ = draft.IsDirty,
            () => _ = draft.Count,
            () => _ = ((ICollection<int>)draft).IsReadOnly,
            () => draft.Contains(1),
            () => draft.CopyTo(new int[1], 0),
            () => draft.GetEnumerator(),
            () => draft.IsSubsetOf([]),
            () => draft.IsSupersetOf([]),
            () => draft.IsProperSubsetOf([]),
            () => draft.IsProperSupersetOf([]),
            () => draft.Overlaps([]),
            () => draft.SetEquals([]),
            () => draft.Add(2),
            () => ((ICollection<int>)draft).Add(2),
            () => draft.Remove(1),
            () => draft.Clear(),
            () => draft.UnionWith([]),
            () => draft.IntersectWith([]),
            () => draft.ExceptWith([]),
            () => draft.SymmetricExceptWith([]),
            () => draft.BuildHashSet(),
            () => draft.BuildSortedSet(),
        ];

        foreach (var member in members)
        {
            Should.Throw<ObjectDisposedException>(member).Message.ShouldContain("draft revoked");
        }

        Should.Throw<ArgumentNullException>(() => _ = (SetDraft<int>)(ImmutableHashSet<int>)null!).ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => _ = (SetDraft<int>)(ImmutableSortedSet<int>)null!).ParamName.ShouldBe("source");
    }

    // A random edit script over a hash or sorted source (default or custom comparer) against a HashSet/SortedSet model with
    // the same comparer: after each edit the draft reads as the model; at the end both Build* kinds hold the model, the
    // source's own kind keeps its comparer, a draft whose edits never changed the contents returns its own source, and IsDirty
    // says whether the build is a new instance.
    [Fact]
    public void SetDraft_ModelBased()
    {
        var edit = Gen.Select(Gen.Int[0, 7], Gen.Int[0, 7], Gen.Int[0, 7].Array[0, 3]);
        var cases = Gen.Select(Gen.Int[0, 7].Array[0, 6], Gen.Int[0, 3], edit.List[0, 24]);
        Property.Check(cases, run =>
        {
            var (initial, kind, edits) = run;
            var sorted = kind >= 2;
            var custom = kind % 2 == 1;
            IEqualityComparer<int> equality = custom ? new Mod5Comparer() : EqualityComparer<int>.Default;
            IComparer<int> order = custom ? Comparer<int>.Create((x, y) => y.CompareTo(x)) : Comparer<int>.Default;
            ISet<int> model = sorted ? new SortedSet<int>(initial, order) : new HashSet<int>(initial, equality);
            IImmutableSet<int> source = sorted
                ? ImmutableSortedSet.CreateRange(order, initial)
                : ImmutableHashSet.CreateRange(equality, initial);
            var draft = sorted ? (SetDraft<int>)(ImmutableSortedSet<int>)source : (ImmutableHashSet<int>)source;
            var dirty = false;

            foreach (var (op, item, other) in edits)
            {
                var before = model.ToArray();
                Apply(draft, model, op, item, other);
                dirty |= !model.SetEquals(before);
                model.SetEquals(draft).ShouldBeTrue();
                draft.Count.ShouldBe(model.Count);
                if (sorted)
                {
                    draft.ShouldBe(model); // same comparer, same order
                }
            }

            model.SetEquals(draft.BuildHashSet()).ShouldBeTrue();
            model.SetEquals(draft.BuildSortedSet()).ShouldBeTrue();
            if (!dirty)
            {
                draft.IsDirty.ShouldBeFalse();
            }

            if (sorted)
            {
                draft.BuildSortedSet().KeyComparer.ShouldBeSameAs(order);
            }
            else
            {
                draft.BuildHashSet().KeyComparer.ShouldBeSameAs(equality);
            }
        });
    }

    private static void Apply(SetDraft<int> draft, ISet<int> model, int op, int item, int[] other)
    {
        switch (op)
        {
            case 0:
                draft.Add(item).ShouldBe(model.Add(item));
                break;
            case 1:
                draft.Remove(item).ShouldBe(model.Remove(item));
                break;
            case 2:
                draft.UnionWith(other);
                model.UnionWith(other);
                break;
            case 3:
                draft.IntersectWith(other);
                model.IntersectWith(other);
                break;
            case 4:
                draft.ExceptWith(other);
                model.ExceptWith(other);
                break;
            case 5:
                draft.SymmetricExceptWith(other);
                model.SymmetricExceptWith(other);
                break;
            case 6 when item == 0:
                draft.Clear();
                model.Clear();
                break;
            default:
                draft.Contains(item).ShouldBe(model.Contains(item));
                draft.IsSubsetOf(other).ShouldBe(model.IsSubsetOf(other));
                draft.Overlaps(other).ShouldBe(model.Overlaps(other));
                break;
        }
    }

    private sealed class Mod5Comparer : IEqualityComparer<int>
    {
        public bool Equals(int x, int y) => x % 5 == y % 5;

        public int GetHashCode(int obj) => obj % 5;
    }
}

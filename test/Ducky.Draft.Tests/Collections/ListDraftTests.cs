using System.Collections;
using System.Collections.Immutable;
using CsCheck;
using Ducky.TestSupport;

namespace Ducky.Draft.Tests.Collections;

// SPEC §13.2 (ListDraft<T>), INV-26. Non-normative tests of story M10-01.
public sealed class ListDraftTests
{
    [Fact]
    public void ListDraft_Clean_BuildReturnsSource()
    {
        var list = ImmutableList.Create(1, 2, 3);
        var array = ImmutableArray.Create(1, 2, 3);
        ListDraft<int> fromList = list;
        ListDraft<int> fromArray = array;

        fromList[1] = 2; // an equal set is not a change
        fromArray[1] = 2;

        fromList.IsDirty.ShouldBeFalse();
        fromArray.IsDirty.ShouldBeFalse();
        fromList.BuildList().ShouldBeSameAs(list);
        fromArray.BuildArray().Equals(array).ShouldBeTrue(); // same underlying array

        // The Build* name only selects the kind: a clean draft of the other kind builds a new instance of it.
        var listAsArray = fromList.BuildArray();
        listAsArray.ShouldBe([1, 2, 3]);
        var arrayAsList = fromArray.BuildList();
        arrayAsList.ShouldBe([1, 2, 3]);

        // A dirty draft builds a new instance even when its contents end up equal (a structural operation).
        fromList.RemoveAt(2);
        fromList.Add(3);
        fromList.IsDirty.ShouldBeTrue();
        fromList.BuildList().ShouldNotBeSameAs(list);
        fromList.BuildList().ShouldBe([1, 2, 3]);
        list.ShouldBe([1, 2, 3]);
        fromArray[0] = 9;
        fromArray.IsDirty.ShouldBeTrue();
        fromArray.BuildArray().Equals(array).ShouldBeFalse();
        fromArray.BuildArray().ShouldBe([9, 2, 3]);
        array.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void ListDraft_NullList_Throws()
    {
        ImmutableList<int> none = null!;

        Should.Throw<ArgumentNullException>(() => _ = (ListDraft<int>)none).ParamName.ShouldBe("list");
    }

    // IList<T> contract: whatever the source kind and dirty state (an array source reads through the boxed array indexer).
    [Fact]
    public void ListDraft_IndexOutOfRange_ThrowsArgumentOutOfRange()
    {
        ListDraft<int> dirty = ImmutableArray.Create(1, 2);
        dirty.Add(3);
        ListDraft<int>[] drafts = [ImmutableList.Create(1, 2), ImmutableArray.Create(1, 2), default(ImmutableArray<int>), dirty];

        foreach (var draft in drafts)
        {
            Should.Throw<ArgumentOutOfRangeException>(() => draft[5]).ParamName.ShouldBe("index");
            Should.Throw<ArgumentOutOfRangeException>(() => draft[-1]);
            Should.Throw<ArgumentOutOfRangeException>(() => draft[draft.Count]);
            Should.Throw<ArgumentOutOfRangeException>(() => draft[5] = 0).ParamName.ShouldBe("index");
        }

        drafts[1].IsDirty.ShouldBeFalse();
    }

    // §13.2: a draft reads its source until the first mutation; an edit that changes nothing is not one.
    [Fact]
    public void ListDraft_NoOpEdits_StayClean()
    {
        var list = ImmutableList.Create(1, 2);
        var array = ImmutableArray.Create(1, 2);
        ListDraft<int> fromList = list;
        ListDraft<int> fromArray = array;
        ListDraft<int> empty = ImmutableList<int>.Empty;

        foreach (var draft in new[] { fromList, fromArray, empty })
        {
            draft.Remove(9).ShouldBeFalse();
            draft.RemoveAll(_ => false).ShouldBe(0);
            draft.AddRange([]);
            draft.IsDirty.ShouldBeFalse();
        }

        empty.Clear();
        empty.IsDirty.ShouldBeFalse();
        empty.BuildList().ShouldBeSameAs(ImmutableList<int>.Empty);
        fromList.BuildList().ShouldBeSameAs(list);
        fromArray.BuildArray().Equals(array).ShouldBeTrue();
        Should.Throw<ArgumentNullException>(() => fromList.AddRange(null!));
        Should.Throw<ArgumentNullException>(() => empty.RemoveAll(null!));
    }

    // Enumeration always walks a snapshot, so editing inside a foreach never throws, clean or dirty.
    [Fact]
    public void ListDraft_MutateWhileEnumerating_IteratesSnapshot()
    {
        ListDraft<int> clean = ImmutableList.Create(1, 2, 3);
        ListDraft<int> dirty = ImmutableArray.Create(1, 2, 3);
        dirty.Add(4);

        foreach (var draft in new[] { clean, dirty })
        {
            var seen = new List<int>();
            foreach (var x in draft)
            {
                seen.Add(x);
                draft.Remove(x);
            }

            seen.ShouldBe(draft == dirty ? [1, 2, 3, 4] : [1, 2, 3]);
            draft.ShouldBeEmpty();
        }
    }

    [Fact]
    public void ListDraft_DefaultArray_ReadsEmptyReturnsDefault()
    {
        ListDraft<int> draft = default(ImmutableArray<int>);

        draft.Count.ShouldBe(0);
        draft.ShouldBeEmpty();
        draft.IsDirty.ShouldBeFalse();
        draft.BuildArray().IsDefault.ShouldBeTrue();
        draft.BuildList().ShouldBeEmpty();

        draft.Add(4);

        draft.BuildArray().ShouldBe([4]);
        draft.BuildList().ShouldBe([4]);
    }

    [Fact]
    public void ListDraft_Members_ReadAndWrite()
    {
        var source = ImmutableList.Create(3, 1, 2);
        ListDraft<int> draft = source;
        IList<int> list = draft;

        list.IsReadOnly.ShouldBeFalse();
        ((IReadOnlyList<int>)draft).Count.ShouldBe(3);
        ((IReadOnlyList<int>)draft)[0].ShouldBe(3);
        draft.IndexOf(1).ShouldBe(1);
        draft.Contains(2).ShouldBeTrue();
        draft.Contains(7).ShouldBeFalse();
        var copy = new int[4];
        draft.CopyTo(copy, 1);
        copy.ShouldBe([0, 3, 1, 2]);
        var untyped = ((IEnumerable)draft).GetEnumerator(); // Cast<int>() would skip it: the draft is an IEnumerable<int>
        untyped.MoveNext().ShouldBeTrue();
        untyped.Current.ShouldBe(3);
        draft.IsDirty.ShouldBeFalse();

        draft.Insert(0, 5);
        draft.ShouldBe([5, 3, 1, 2]);
        draft.Remove(1).ShouldBeTrue();
        draft.Remove(7).ShouldBeFalse();
        draft.AddRange([6, 7]);
        draft.ShouldBe([5, 3, 2, 6, 7]);
        draft.RemoveAll(x => x % 2 == 0).ShouldBe(2);
        draft.Sort((a, b) => a.CompareTo(b));
        draft.ShouldBe([3, 5, 7]);
        draft.IndexOf(7).ShouldBe(2);
        draft.BuildArray().ShouldBe([3, 5, 7]);
        draft.Clear();
        draft.Count.ShouldBe(0);
        draft.BuildList().ShouldBeEmpty();
        source.ShouldBe([3, 1, 2]);
    }

    [Fact]
    public void ListDraft_Revoked_Throws()
    {
        ListDraft<int> draft = ImmutableList.Create(1, 2);
        ((IRevocable)draft).Revoke();
        Action[] members =
        [
            () => _ = draft.IsDirty,
            () => _ = draft.Count,
            () => _ = ((IList<int>)draft).IsReadOnly,
            () => _ = draft[0],
            () => draft[0] = 1,
            () => draft.IndexOf(1),
            () => draft.Contains(1),
            () => draft.CopyTo(new int[2], 0),
            () => draft.GetEnumerator(),
            () => draft.Add(3),
            () => draft.Insert(0, 3),
            () => draft.Remove(1),
            () => draft.Remove(9),
            () => draft.RemoveAt(0),
            () => draft.Clear(),
            () => draft.AddRange([3]),
            () => draft.AddRange([]),
            () => draft.RemoveAll(_ => true),
            () => draft.RemoveAll(_ => false),
            () => draft.Sort((a, b) => a.CompareTo(b)),
            () => draft.BuildList(),
            () => draft.BuildArray(),
        ];

        foreach (var member in members)
        {
            Should.Throw<ObjectDisposedException>(member).Message.ShouldContain("draft revoked");
        }

        ListDraft<int> dirty = ImmutableList.Create(1, 2);
        dirty.Add(3);
        ((IRevocable)dirty).Revoke();
        Should.Throw<ObjectDisposedException>(() => dirty.GetEnumerator());
    }

    [Fact]
    public void ListDraft_NotDependentOnDucky() =>
        typeof(ListDraft<>).Assembly.GetReferencedAssemblies().ShouldNotContain(name => name.Name == "Ducky");

    // A random edit script over a list or array source against a List<int> model: after each edit the draft reads as the
    // model; at the end both Build* kinds equal the model, the source is unchanged, and a draft whose only edits were
    // equal sets is clean and returns its own source.
    [Fact]
    public void ListDraft_ModelBased()
    {
        var edit = Gen.Select(Gen.Int[0, 8], Gen.Int[0, 15], Gen.Int[0, 3]);
        var cases = Gen.Select(Gen.Int[0, 3].List[0, 6], Gen.Bool, edit.List[0, 24]);
        Property.Check(cases, run =>
        {
            var (initial, fromArray, edits) = run;
            var list = initial.ToImmutableList();
            var array = initial.ToImmutableArray();
            var draft = fromArray ? (ListDraft<int>)array : list;
            var model = new List<int>(initial);
            var dirty = false;

            foreach (var (op, index, value) in edits)
            {
                dirty |= Apply(draft, model, op, index, value);
                draft.ShouldBe(model);
                draft.Count.ShouldBe(model.Count);
            }

            draft.IsDirty.ShouldBe(dirty);
            draft.BuildList().ShouldBe(model);
            draft.BuildArray().ShouldBe(model);
            list.ShouldBe(initial);
            array.ShouldBe(initial);
            if (!dirty)
            {
                (fromArray ? draft.BuildArray().Equals(array) : ReferenceEquals(draft.BuildList(), list)).ShouldBeTrue();
            }
        });
    }

    // Applies edit `op` to both; returns whether the draft must count it as a change: an edit that changes the contents, or
    // a sort (structural even when already sorted).
    private static bool Apply(ListDraft<int> draft, List<int> model, int op, int index, int value)
    {
        var at = model.Count == 0 ? 0 : index % model.Count;
        switch (op)
        {
            case 0 when model.Count > 0:
                var changed = model[at] != value;
                draft[at] = value;
                model[at] = value;
                return changed;
            case 1:
                draft.Add(value);
                model.Add(value);
                return true;
            case 2:
                at = index % (model.Count + 1);
                draft.Insert(at, value);
                model.Insert(at, value);
                return true;
            case 3 when model.Count > 0:
                draft.RemoveAt(at);
                model.RemoveAt(at);
                return true;
            case 4:
                var removed = model.Remove(value);
                draft.Remove(value).ShouldBe(removed);
                return removed;
            case 5:
                int[] items = index % 4 == 0 ? [] : [value, index];
                draft.AddRange(items);
                model.AddRange(items);
                return items.Length > 0;
            case 6:
                var count = model.RemoveAll(x => x == value);
                draft.RemoveAll(x => x == value).ShouldBe(count);
                return count > 0;
            case 7:
                draft.Sort((a, b) => b.CompareTo(a));
                model.Sort((a, b) => b.CompareTo(a));
                return true;
            case 8:
                var cleared = model.Count > 0;
                draft.Clear();
                model.Clear();
                return cleared;
            default:
                draft.IndexOf(value).ShouldBe(model.IndexOf(value));
                return false;
        }
    }
}

using System.Collections;
using System.Collections.Immutable;

namespace Ducky.Draft.Tests.Collections;

// SPEC §13.2 (ListDraft<T,TDraft>), §13.3 (collection-draft rule, revocation), INV-26. Non-normative tests of story M10-02.
public sealed class ElementListDraftTests
{
    private static readonly Todo _a = new(1, "a");
    private static readonly Todo _b = new(2, "b");
    private static readonly Todo _c = new(3, "c");

    // Non-normative runtime twin of Produce_ListStructuralOps_FlushElementDrafts: every structural operation flushes the
    // cached element drafts into the list, revokes them and clears the cache; a later access re-drafts from the flushed list.
    [Fact]
    public void ElementDraft_StructuralOpsFlushCache()
    {
        var source = ImmutableList.Create(_a, _b, _c);
        var created = 0;
        var draft = new ListDraft<Todo, TodoDraft>(source, x =>
        {
            created++;
            return new TodoDraft(x);
        });

        created.ShouldBe(0); // lazy
        var a = draft[0];
        draft[0].ShouldBeSameAs(a); // cached
        created.ShouldBe(1);
        a.Title = "a2";
        draft.IsDirty.ShouldBeTrue();

        draft.RemoveAt(1);

        Should.Throw<ObjectDisposedException>(() => a.Title = "x");
        draft[0].ShouldNotBeSameAs(a);
        draft[0].Title.ShouldBe("a2"); // re-drafted from the flushed list
        draft[1].Title.ShouldBe("c");

        var c = draft[1];
        c.Title = "c2";
        draft.Insert(0, _b);
        Should.Throw<ObjectDisposedException>(() => c.Title = "x");
        draft.Select(t => t.Title).ShouldBe(["b", "a2", "c2"]);

        var b = draft[0];
        b.Title = "b2";
        draft.RemoveAll(t => t.Title == "a2").ShouldBe(1);
        Should.Throw<ObjectDisposedException>(() => b.Title = "x");
        draft.Select(t => t.Title).ShouldBe(["b2", "c2"]);

        draft.BuildList().Select(t => t.Title).ShouldBe(["b2", "c2"]);
        var last = draft[1];
        draft.Clear();
        Should.Throw<ObjectDisposedException>(() => last.Title = "x");
        draft.Count.ShouldBe(0);
        draft.BuildList().ShouldBeEmpty();
        source.ShouldBe([_a, _b, _c]); // the base is never mutated
    }

    [Fact]
    public void ElementDraft_Clean_BuildReturnsSource()
    {
        var list = ImmutableList.Create(_a, _b);
        var array = ImmutableArray.Create(_a, _b);
        var fromList = new ListDraft<Todo, TodoDraft>(list, x => new TodoDraft(x));
        var fromArray = new ListDraft<Todo, TodoDraft>(array, x => new TodoDraft(x));

        fromList[0].Title.ShouldBe("a"); // read through an element draft: no change
        fromArray[1].Title = "b2";
        fromArray[1].Title = "b"; // reverted
        var first = fromList[0];
        fromList.RemoveAll(_ => false).ShouldBe(0); // nothing removed: no structural change, drafts kept
        first.Title = "a";

        fromList.IsDirty.ShouldBeFalse();
        fromList.BuildList().ShouldBeSameAs(list);
        fromArray.BuildArray().Equals(array).ShouldBeTrue(); // same underlying array
        fromList.BuildArray().ShouldBe([_a, _b]);
        fromArray.BuildList().ShouldBe([_a, _b]);

        var defaultArray = new ListDraft<Todo, TodoDraft>(default(ImmutableArray<Todo>), x => new TodoDraft(x));
        defaultArray.Count.ShouldBe(0);
        defaultArray.ShouldBeEmpty();
        defaultArray.BuildArray().IsDefault.ShouldBeTrue();
    }

    [Fact]
    public void ElementDraft_EditsBuildNewElements()
    {
        var array = ImmutableArray.Create(_a, _b, _c);
        var draft = new ListDraft<Todo, TodoDraft>(array, x => new TodoDraft(x));

        var b = draft[1];
        _ = draft[0];
        b.Title = "b2";
        draft.IsDirty.ShouldBeTrue(); // one dirty element draft among clean ones
        draft.Add(_a); // Add appends: no index shifts, so cached drafts stay live
        b.Title = "b3";
        var c = draft[2];
        draft.Set(2, _b); // replaces the element: its cached draft is revoked, the others stay live
        Should.Throw<ObjectDisposedException>(() => c.Title = "x");
        b.Title = "b4";

        draft.IsDirty.ShouldBeTrue();
        var built = draft.BuildArray();
        built.ShouldBe([_a, _b with { Title = "b4" }, _b, _a]);
        built[0].ShouldBeSameAs(_a); // untouched elements keep their references
        built[2].ShouldBeSameAs(_b);
        draft.BuildList().ShouldBe(built); // Build flushes without revoking: further edits are kept
        b.Title = "b5";
        draft.BuildList()[1].Title.ShouldBe("b5");
        array.ShouldBe([_a, _b, _c]);
        draft.RemoveAll(t => t.Title is "a" or "b5").ShouldBe(3); // removed from the end: earlier indices hold
        draft.BuildArray().ShouldBe([_b]);

        ((IEnumerable)draft).GetEnumerator().MoveNext().ShouldBeTrue();
    }

    // §13.2: the indexer never calls createDraft on a null element (an oblivious T); it returns null, which stays null.
    [Fact]
    public void ElementDraft_NullElement_NotDrafted()
    {
        var list = ImmutableList.Create(_a, null!, _c);
        var created = 0;
        var draft = new ListDraft<Todo, TodoDraft>(list, x =>
        {
            created++;
            return new TodoDraft(x);
        });

        draft[1].ShouldBeNull();
        draft.Count(x => x is null).ShouldBe(1);
        created.ShouldBe(2);
        draft[0].Title = "a2";
        draft.Insert(3, _b);

        draft.BuildList().ShouldBe([_a with { Title = "a2" }, null!, _c, _b]);
    }

    [Fact]
    public void ElementDraft_InvalidArguments_Throw()
    {
        Should.Throw<ArgumentNullException>(() => new ListDraft<Todo, TodoDraft>((ImmutableList<Todo>)null!, x => new TodoDraft(x)))
            .ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => new ListDraft<Todo, TodoDraft>(ImmutableList<Todo>.Empty, null!))
            .ParamName.ShouldBe("createDraft");
        Should.Throw<ArgumentNullException>(() => new ListDraft<Todo, TodoDraft>(ImmutableArray<Todo>.Empty, null!))
            .ParamName.ShouldBe("createDraft");
        var draft = new ListDraft<Todo, TodoDraft>(ImmutableList.Create(_a), x => new TodoDraft(x));
        Should.Throw<ArgumentOutOfRangeException>(() => draft[1]);
        Should.Throw<ArgumentOutOfRangeException>(() => draft.Set(1, _b));
        Should.Throw<ArgumentNullException>(() => draft.RemoveAll(null!));

        // An out-of-range structural call makes no change, so it flushes and revokes nothing.
        var held = draft[0];
        Should.Throw<ArgumentOutOfRangeException>(() => draft.Insert(2, _b));
        Should.Throw<ArgumentOutOfRangeException>(() => draft.Insert(-1, _b));
        Should.Throw<ArgumentOutOfRangeException>(() => draft.RemoveAt(1));
        Should.Throw<ArgumentOutOfRangeException>(() => draft.RemoveAt(-1));
        held.Title = "a2";
        draft.BuildList().ShouldBe([_a with { Title = "a2" }]);
    }

    // INV-26: the flush writes an element draft's result by reference, so an element whose Equals ignores the edited member
    // (an entity comparing by Id) still gets its edit, at Build and at a structural operation.
    [Fact]
    public void ElementDraft_IdOnlyEquals_EditKept()
    {
        var first = new Entity(1, "a");
        var second = new Entity(2, "b");
        var draft = new ListDraft<Entity, EntityDraft>(ImmutableList.Create(first, second), x => new EntityDraft(x));

        draft[0].Title = "a2";
        draft.IsDirty.ShouldBeTrue();
        draft.BuildList()[0].Title.ShouldBe("a2");
        draft.BuildArray()[0].Title.ShouldBe("a2");

        draft[1].Title = "b2";
        draft.RemoveAt(0);
        draft[0].Title.ShouldBe("b2"); // re-drafted from the flushed list
        draft.BuildList().Select(x => x.Title).ShouldBe(["b2"]);
    }

    // A structural operation inside a foreach fails fast, as List<T> does, instead of skipping or repeating elements;
    // edits through the visited drafts, Set and Add (no index shift) are fine.
    [Fact]
    public void ElementDraft_StructuralOpDuringEnumeration_Throws()
    {
        var draft = new ListDraft<Todo, TodoDraft>(ImmutableList.Create(_a, _b, _c), x => new TodoDraft(x));

        foreach (var todo in draft)
        {
            todo.Title += "!";
            draft.Set(0, _c);
            draft.Add(_a);
        }

        draft.Select(t => t.Title).ShouldBe(["c", "b!", "c!", "a", "a", "a"]);
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var _ in draft)
            {
                draft.Insert(0, _b);
            }
        }).Message.ShouldContain("modified");
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var _ in draft)
            {
                draft.RemoveAt(0);
            }
        });
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var _ in draft)
            {
                draft.RemoveAll(t => t.Title == "b!");
            }
        });
        Should.Throw<InvalidOperationException>(() =>
        {
            foreach (var _ in draft)
            {
                draft.Clear();
            }
        });
    }

    // Revoking a collection draft revokes its cached element drafts.
    [Fact]
    public void ElementDraft_Revoked_Throws()
    {
        var draft = new ListDraft<Todo, TodoDraft>(ImmutableList.Create(_a, _b), x => new TodoDraft(x));
        var a = draft[0];

        ((IRevocable)draft).Revoke();

        Should.Throw<ObjectDisposedException>(() => a.Title = "x");
        Action[] members =
        [
            () => _ = draft.IsDirty,
            () => _ = draft.Count,
            () => _ = draft[0],
            () => draft.GetEnumerator(),
            () => draft.Add(_c),
            () => draft.Insert(0, _c),
            () => draft.Set(0, _c),
            () => draft.RemoveAt(0),
            () => draft.RemoveAll(_ => true),
            () => draft.Clear(),
            () => draft.BuildList(),
            () => draft.BuildArray(),
        ];

        foreach (var member in members)
        {
            Should.Throw<ObjectDisposedException>(member);
        }
    }
}

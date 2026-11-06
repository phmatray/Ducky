using System.Collections.Immutable;

namespace TodoApp.Tests;

// The migrated recipes keep the Mutty app's behaviour, and a no-op recipe now returns the base (INV-26).
public sealed class TodoReducersTests
{
    private static readonly TodoState _base = new(
        [new Todo(1, "Write", false), new Todo(2, "Ship", true)],
        ["work"],
        ImmutableQueue<string>.Empty,
        new Owner("Ada"),
        "all");

    [Fact]
    public void SetFilter_ChangesScalar()
    {
        var next = TodoReducers.SetFilter(_base, "done");

        next.Filter.ShouldBe("done");
        next.Items.ShouldBeSameAs(_base.Items);
    }

    [Fact]
    public void SetFilter_SameValue_ReturnsSameReference() =>
        TodoReducers.SetFilter(_base, "all").ShouldBeSameAs(_base);

    [Fact]
    public void RenameOwner_EditsNestedRecord()
    {
        var next = TodoReducers.RenameOwner(_base, "Grace");

        next.Owner.ShouldBe(new Owner("Grace"));
        _base.Owner.Name.ShouldBe("Ada");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void Toggle_FlipsMatchingTodo(int id, bool done)
    {
        var next = TodoReducers.Toggle(_base, id);

        next.Items.Single(x => x.Id == id).Done.ShouldBe(done);
        next.Items.Single(x => x.Id != id).ShouldBeSameAs(_base.Items.Single(x => x.Id != id));
    }

    [Fact]
    public void Toggle_UnknownId_ReturnsSameReference() =>
        TodoReducers.Toggle(_base, 99).ShouldBeSameAs(_base);

    [Fact]
    public void AddTodo_AppendsRecord()
    {
        var todo = new Todo(3, "Test", false);

        TodoReducers.AddTodo(_base, todo).Items.ShouldBe([.. _base.Items, todo]);
    }

    [Fact]
    public void RemoveDone_DropsDoneTodos() =>
        TodoReducers.RemoveDone(_base).Items.Select(x => x.Id).ShouldBe([1]);

    [Fact]
    public void ReplaceTags_AssignsWholeList() =>
        TodoReducers.ReplaceTags(_base, ["home", "urgent"]).Tags.ShouldBe(["home", "urgent"]);

    [Fact]
    public void Enqueue_AppendsToQueue()
    {
        var next = TodoReducers.Enqueue(TodoReducers.Enqueue(_base, "a"), "b");

        next.Pending.ShouldBe(["a", "b"]);
        _base.Pending.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void RenameInSteps_BuildsDraft()
    {
        var next = TodoReducers.RenameInSteps(_base, 1, "Draft");

        next.Items[1].Title.ShouldBe("Draft");
        next.Items[0].ShouldBeSameAs(_base.Items[0]);
        next.Filter.ShouldBe("all");
    }
}

using System.Collections.Immutable;
using Ducky.Testing;

namespace Ducky.Docs.Tests.Core;

#region entity-state
public sealed record TodoItem(int Id, string Title, bool Done) : IEntity<int>;

public sealed record Todos(EntityState<int, TodoItem> Items)
{
    // The same Todos when the collection is the same instance, so an unchanged collection commits nothing.
    public Todos With(EntityState<int, TodoItem> items) => ReferenceEquals(items, Items) ? this : new(items);
}

public sealed record TodoAdded(TodoItem Todo);

public sealed record TodoToggled(int Id);

public sealed record TodoRemoved(int Id);

public sealed record TodosLoaded(ImmutableList<TodoItem> Todos);

public sealed class TodosSlice : Slice<Todos>
{
    public TodosSlice()
    {
        On<TodoAdded>((state, action) => state.With(state.Items.Upsert(action.Todo)));
        On<TodoToggled>((state, action) => state.With(state.Items.Update(action.Id, todo => todo with { Done = !todo.Done })));
        On<TodoRemoved>((state, action) => state.With(state.Items.Remove(action.Id)));
        On<TodosLoaded>((state, action) => state.With(state.Items.Merge(action.Todos, MergeStrategy.PreferIncoming)));
    }

    protected override Todos Initial => new(EntityState<int, TodoItem>.Empty);
}
#endregion

// The snippets of docs/guides/core/entity-state.md.
public sealed class EntityStateGuideTests
{
    // Non-normative (M15-02): the reducers add, toggle, remove and merge by id, in insertion order.
    [Fact]
    public async Task EntityState_Reducers_ChangeById()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>());

        await store.DispatchAsync(new TodoAdded(new TodoItem(1, "Write docs", Done: false)));
        await store.DispatchAsync(new TodoAdded(new TodoItem(2, "Test them", Done: false)));
        await store.DispatchAsync(new TodoToggled(1));
        await store.DispatchAsync(new TodosLoaded([new TodoItem(2, "Test every snippet", Done: false), new TodoItem(3, "Ship", Done: false)]));
        await store.DispatchAsync(new TodoRemoved(3));

        #region entity-read
        var todos = store.State.Get<Todos>().Items;
        var first = todos.Find(1);                         // by id, without scanning
        var exists = todos.Contains(3);                    // false: removed
        var open = todos.Items.Count(todo => !todo.Done); // Items keeps insertion order
        #endregion

        first.ShouldBe(new TodoItem(1, "Write docs", Done: true));
        exists.ShouldBeFalse();
        open.ShouldBe(1);
        todos.Items.ShouldBe([new TodoItem(1, "Write docs", Done: true), new TodoItem(2, "Test every snippet", Done: false)]);
        todos.Count.ShouldBe(2);
        store.Failures.ShouldBeEmpty();
    }

    // Non-normative (M15-02): an operation that changes nothing keeps the instance, so the store commits nothing.
    [Fact]
    public async Task EntityState_NoChange_NothingCommitted()
    {
        var todo = new TodoItem(1, "Write docs", Done: false);
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>());
        await store.DispatchAsync(new TodoAdded(todo));
        var before = store.State;

        await store.DispatchAsync(new TodoToggled(42));
        await store.DispatchAsync(new TodoRemoved(42));
        await store.DispatchAsync(new TodoAdded(todo));

        store.State.ShouldBeSameAs(before);
    }

    // Non-normative (M15-02): the guide's Merge, Add and constructor rules.
    [Fact]
    public void EntityState_MergeAndAdd_AsDocumented()
    {
        var stored = EntityState<int, TodoItem>.Empty.Add(new TodoItem(1, "Stored", Done: false));
        TodoItem[] incoming = [new TodoItem(1, "Incoming", Done: true)];

        stored.Merge(incoming, MergeStrategy.PreferIncoming).Find(1).ShouldBe(incoming[0]);
        stored.Merge(incoming, MergeStrategy.PreferExisting).Find(1)!.Title.ShouldBe("Stored");
        Should.Throw<ArgumentException>(() => stored.Merge(incoming, MergeStrategy.FailIfDuplicate));
        Should.Throw<ArgumentException>(() => stored.Add(incoming[0]));
        Should.Throw<ArgumentException>(() => new EntityState<int, TodoItem>([.. incoming, .. incoming]));
    }
}

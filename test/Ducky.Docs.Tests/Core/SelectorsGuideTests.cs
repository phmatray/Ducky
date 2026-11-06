using Ducky.Testing;

namespace Ducky.Docs.Tests.Core;

#region selector-defs
public static class TodoSelectors
{
    // A SelectorDef holds no cache, so a static field is safe.
    public static readonly SelectorDef<int> OpenCount = Selector.Define(
        (StateSnapshot state) => state.Get<Todos>().Items,
        items => items.Items.Count(todo => !todo.Done));

    public static readonly SelectorDef<int, TodoItem?> ById = Selector.Define(
        (StateSnapshot state) => state.Get<Todos>().Items,
        (EntityState<int, TodoItem> items, int id) => items.Find(id));
}
#endregion

// The snippets of docs/guides/core/selectors.md.
public sealed class SelectorsGuideTests
{
    // Non-normative (M15-02): the selections follow the state and the argument; onChange runs only on a changed value.
    [Fact]
    public async Task Selectors_Select_FollowStateArgumentAndChanges()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>());
        var selectedId = 1;
        var openBadge = -1;
        List<int> changes = [];

        #region selector-select
        using var open = store.Select(TodoSelectors.OpenCount.Create(), onChange: count => openBadge = count);
        using var selected = store.Select(TodoSelectors.ById.Create(() => selectedId));
        using var total = store.Select((Todos todos) => todos.Items.Count, onChange: changes.Add);

        await store.DispatchAsync(new TodoAdded(new TodoItem(1, "Write docs", Done: false)));

        int openCount = open;           // 1: a Selection<T> converts to T
        var title = selected.Value?.Title; // "Write docs"
        #endregion

        openCount.ShouldBe(1);
        title.ShouldBe("Write docs");
        openBadge.ShouldBe(1);
        total.Value.ShouldBe(1);

        await store.DispatchAsync(new TodoAdded(new TodoItem(2, "Done already", Done: true)));
        openBadge = -1;
        await store.DispatchAsync(new TodoAdded(new TodoItem(3, "Also done", Done: true)));
        selectedId = 3;

        openBadge.ShouldBe(-1);
        open.Value.ShouldBe(1);
        selected.Value.ShouldBe(new TodoItem(3, "Also done", Done: true));
        changes.ShouldBe([1, 2, 3]);
        TodoSelectors.OpenCount.Evaluate(store.State).ShouldBe(1);
        TodoSelectors.ById.Evaluate(store.State, 2)!.Title.ShouldBe("Done already");
    }

    // Non-normative (M15-02): a disposed selection is no longer notified.
    [Fact]
    public async Task Selectors_Disposed_NoMoreChanges()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<TodosSlice>());
        List<int> changes = [];
        var total = store.Select((Todos todos) => todos.Items.Count, onChange: changes.Add);
        await store.DispatchAsync(new TodoAdded(new TodoItem(1, "Write docs", Done: false)));

        total.Dispose();
        await store.DispatchAsync(new TodoAdded(new TodoItem(2, "Test them", Done: false)));

        changes.ShouldBe([1]);
    }
}

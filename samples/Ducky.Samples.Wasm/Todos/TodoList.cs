using System.Collections.Immutable;

namespace Ducky.Samples.Wasm.Todos;

public sealed record Todo(int Id, string Title, bool Done);

public sealed record TodoList(ImmutableList<Todo> Items, int NextId);

// The action name DevTools shows (and the trimmed smoke asserts) is the attribute's, not Namespace.Name.
[DuckyAction]
[ActionType("todos/added")]
public sealed record TodoAdded(string Title);

[DuckyAction]
[ActionType("todos/toggled")]
public sealed record TodoToggled(int Id);

[DuckyAction]
[ActionType("todos/removed")]
public sealed record TodoRemoved(int Id);

// Kept in localStorage and mirrored to every other tab of the origin (the generator turns [Persist] into
// PersistAttributeDefaults<TodosSlice>).
[Persist(SyncAcrossTabs = true)]
public sealed class TodosSlice : Slice<TodoList>
{
    public TodosSlice()
    {
        On<TodoAdded>((state, action) => state with
        {
            Items = state.Items.Add(new Todo(state.NextId, action.Title, Done: false)),
            NextId = state.NextId + 1,
        });
        On<TodoToggled>((state, action) => state with
        {
            Items = state.Items.Select(todo => todo.Id == action.Id ? todo with { Done = !todo.Done } : todo).ToImmutableList(),
        });
        On<TodoRemoved>((state, action) => state with { Items = state.Items.RemoveAll(todo => todo.Id == action.Id) });
        On<SuggestionsLoaded>((state, action) => state with
        {
            Items = state.Items.AddRange(action.Titles.Select((title, index) => new Todo(state.NextId + index, title, Done: false))),
            NextId = state.NextId + action.Titles.Length,
        });
    }

    protected override TodoList Initial => new([], NextId: 1);
}

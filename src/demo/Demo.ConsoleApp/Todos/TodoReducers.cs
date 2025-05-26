namespace Demo.ConsoleApp.Todos;

public sealed record TodoReducers : SliceReducers<TodoState>
{
    public TodoReducers()
    {
        On<AddTodo>(Reduce);
        On<ToggleTodo>(Reduce);
        On<RemoveTodo>(Reduce);
        On<ClearCompleted>(Reduce);
        On<ToggleAll>(Reduce);
    }

    public override TodoState GetInitialState()
        => TodoState.Create([
            new TodoItem("1", "Learn Ducky", true),
            new TodoItem("2", "Build Console Demo"),
            new TodoItem("3", "Test State Management")
        ]);

    private static TodoState Reduce(TodoState state, AddTodo action)
    {
        string id = action.Id ?? Guid.NewGuid().ToString();
        TodoItem newTodo = new(id, action.Title);
        return state.SetOne(newTodo);
    }

    private static TodoState Reduce(TodoState state, ToggleTodo action)
    {
        return state.UpdateOne(action.Id, todo => todo with { IsCompleted = !todo.IsCompleted });
    }

    private static TodoState Reduce(TodoState state, RemoveTodo action)
    {
        return state.RemoveOne(action.Id);
    }

    private static TodoState Reduce(TodoState state, ClearCompleted action)
    {
        string[] completedIds = state.SelectEntities(t => t.IsCompleted)
            .Select(t => t.Id)
            .ToArray();
        return state.RemoveMany(completedIds);
    }

    private static TodoState Reduce(TodoState state, ToggleAll action)
    {
        TodoItem[] updatedTodos = state.SelectEntities()
            .Select(t => t with { IsCompleted = action.IsCompleted })
            .ToArray();
        return state.SetMany(updatedTodos);
    }
}

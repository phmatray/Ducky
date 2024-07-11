namespace Demo.AppStore;

#region State

public record TodoState
{
    public required ImmutableList<TodoItem> Todos { get; init; }
    
    // Selectors
    public ImmutableList<TodoItem> SelectActiveTodos()
        => Todos
            .Where(todo => !todo.IsCompleted)
            .ToImmutableList();
    
    public int SelectActiveTodosCount()
        => SelectActiveTodos().Count;
    
    public ImmutableList<TodoItem> SelectCompletedTodos()
        => Todos
            .Where(todo => todo.IsCompleted)
            .ToImmutableList();
    
    public int SelectCompletedTodosCount()
        => SelectCompletedTodos().Count;
}

#endregion

#region Actions

public record CreateTodo(string Title) : IAction;

public record ToggleTodo(Guid Id) : IAction;

public record DeleteTodo(Guid Id) : IAction;

#endregion

#region Reducers

public class TodoReducers : ReducerCollection<TodoState>
{
    public TodoReducers()
    {
        Map<CreateTodo>(MapCreateTodo);
        Map<ToggleTodo>(MapToggleTodo);
        Map<DeleteTodo>(MapDeleteTodo);
    }

    private static TodoState MapCreateTodo(TodoState state, CreateTodo action)
        => new()
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState MapToggleTodo(TodoState state, ToggleTodo action)
        => new()
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState MapDeleteTodo(TodoState state, DeleteTodo action)
        => new()
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableList()
        };
}

#endregion

#region Slice

// ReSharper disable once UnusedType.Global
public record TodoSlice : Slice<TodoState>
{
    public override ReducerCollection<TodoState> Reducers { get; } = new TodoReducers();

    public override string GetKey() => "todos";

    public override TodoState GetInitialState() => new()
    {
        Todos =
        [
            new TodoItem("Learn Blazor", true),
            new TodoItem("Learn Redux"),
            new TodoItem("Learn Reactive Programming"),
            new TodoItem("Create a Todo App", true),
            new TodoItem("Publish a NuGet package")
        ]
    };
}

#endregion
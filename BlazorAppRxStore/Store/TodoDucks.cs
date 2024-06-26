namespace BlazorAppRxStore.Store;

// State
public record TodoState
{
    public IImmutableList<TodoItem> Todos { get; init; } =
    [
        new TodoItem("Learn Blazor") { IsCompleted = true },
        new TodoItem("Learn Redux"),
        new TodoItem("Learn Rx.NET")
    ];
    
    // Selectors
    public IImmutableList<TodoItem> ActiveTodos
        => Todos.Where(todo => !todo.IsCompleted).ToImmutableList();
    
    public int ActiveTodosCount
        => ActiveTodos.Count;
    
    public IImmutableList<TodoItem> CompletedTodos
        => Todos.Where(todo => todo.IsCompleted).ToImmutableList();
    
    public int CompletedTodosCount
        => CompletedTodos.Count;
}

// Actions
public record AddTodo(string Title) : IAction;

public record ToggleTodo(Guid Id) : IAction;

public record RemoveTodo(Guid Id) : IAction;

// Reducer
public class TodoReducer : ActionReducer<TodoState>
{
    public TodoReducer()
    {
        Register<AddTodo>(RegisterAddTodo);
        Register<ToggleTodo>(RegisterToggleTodo);
        Register<RemoveTodo>(RegisterRemoveTodo);
    }

    private static TodoState RegisterAddTodo(TodoState state, AddTodo action)
        => state with
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState RegisterToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState RegisterRemoveTodo(TodoState state, RemoveTodo action)
        => state with
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableList()
        };
}

public class TodoReducerFactory : IActionReducerFactory<TodoState>
{
    public IActionReducer<TodoState> CreateReducer(
        IDictionary<string, IActionReducer<TodoState>> reducers,
        TodoState initialState)
    {
        return new TodoReducer();
    }
}

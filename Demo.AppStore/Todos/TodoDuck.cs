namespace Demo.AppStore;

// State
public record TodoState
{
    public ImmutableArray<TodoItem> Todos { get; init; } =
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
public class TodoReducer : Reducer<TodoState>
{
    public TodoReducer()
    {
        Register<AddTodo>(ReduceAddTodo);
        Register<ToggleTodo>(ReduceToggleTodo);
        Register<RemoveTodo>(ReduceRemoveTodo);
    }

    private static TodoState ReduceAddTodo(TodoState state, AddTodo action)
        => state with
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState ReduceToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableArray()
        };

    private static TodoState ReduceRemoveTodo(TodoState state, RemoveTodo action)
        => state with
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableArray()
        };
}

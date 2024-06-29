namespace Demo.AppStore;

// State
public record TodoState
{
    public ImmutableList<TodoItem> Todos { get; init; } =
    [
        new TodoItem("Learn Blazor", true),
        new TodoItem("Learn Redux"),
        new TodoItem("Learn Reactive Programming"),
        new TodoItem("Create a Todo App", true),
        new TodoItem("Publish a NuGet package")
    ];
    
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

// Actions
public record CreateTodo(string Title) : IAction;

public record ToggleTodo(Guid Id) : IAction;

public record DeleteTodo(Guid Id) : IAction;

// Reducer
public class TodoReducer : Reducer<TodoState>
{
    public TodoReducer()
    {
        Register<CreateTodo>(ReduceCreateTodo);
        Register<ToggleTodo>(ReduceToggleTodo);
        Register<DeleteTodo>(ReduceDeleteTodo);
    }

    private static TodoState ReduceCreateTodo(TodoState state, CreateTodo action)
        => state with
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState ReduceToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState ReduceDeleteTodo(TodoState state, DeleteTodo action)
        => state with
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableList()
        };
}

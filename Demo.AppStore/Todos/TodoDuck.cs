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

// Reducers
public class TodoReducers : ReducerCollection<TodoState>
{
    public TodoReducers()
    {
        Map<CreateTodo>(MapCreateTodo);
        Map<ToggleTodo>(MapToggleTodo);
        Map<DeleteTodo>(MapDeleteTodo);
    }

    private static TodoState MapCreateTodo(TodoState state, CreateTodo action)
        => state with
        {
            Todos = state.Todos.Add(new TodoItem(action.Title))
        };

    private static TodoState MapToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState MapDeleteTodo(TodoState state, DeleteTodo action)
        => state with
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Id)
                .ToImmutableList()
        };
}

// Slice
// ReSharper disable once UnusedType.Global
public record TodoSlice : Slice<TodoState>
{
    public override string Key => "todos";
    public override TodoState InitialState { get; } = new();
    public override IReducer<TodoState> Reducers { get; } = new TodoReducers();
}
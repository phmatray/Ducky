namespace BlazorAppRxStore.Store;

// State
public record TodoState
{
    public List<TodoItem> Todos { get; init; } =
    [
        new TodoItem { Title = "Learn Blazor", IsCompleted = true },
        new TodoItem { Title = "Learn Redux" },
        new TodoItem { Title = "Learn Rx.NET" },
    ];
}

public record TodoItem
{
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }
}

// Actions
public record AddTodo(string Title) : IAction;

public record ToggleTodo(int Index) : IAction;

public record RemoveTodo(int Index) : IAction;

// Reducer
public class TodoReducer : ReducerBase<TodoState>
{
    public TodoReducer()
    {
        Register<AddTodo>((state, action)
            => state with { Todos = [..state.Todos, new TodoItem { Title = action.Title, IsCompleted = false }] });

        Register<ToggleTodo>((state, action)
            => state with
            {
                Todos = new List<TodoItem>(state.Todos)
                {
                    [action.Index] = state.Todos[action.Index] with { IsCompleted = !state.Todos[action.Index].IsCompleted }
                }
            });

        Register<RemoveTodo>((state, action)
            => state with
            {
                Todos = new List<TodoItem>(state.Todos).FindAll(item => item != state.Todos[action.Index])
            });
    }
}
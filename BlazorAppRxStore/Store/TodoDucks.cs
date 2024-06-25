using System.Collections.Immutable;

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
}

public record TodoItem(string Title)
{
    public bool IsCompleted { get; init; }
    
    public TodoItem ToggleIsCompleted()
        => this with { IsCompleted = !IsCompleted };
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
        Register<AddTodo>(RegisterAddTodo);
        Register<ToggleTodo>(RegisterToggleTodo);
        Register<RemoveTodo>(RegisterRemoveTodo);
    }

    private static TodoState RegisterAddTodo(TodoState state, AddTodo action)
        => state with { Todos = state.Todos.Add(new TodoItem(action.Title)) };

    private static TodoState RegisterToggleTodo(TodoState state, ToggleTodo action)
        => state with
        {
            Todos = state.Todos
                .Select((todo, idx) => idx == action.Index ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState RegisterRemoveTodo(TodoState state, RemoveTodo action)
        => state with { Todos = state.Todos.RemoveAt(action.Index) };
}
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

public sealed record CreateTodo
    : FluxStandardAction<CreateTodo.ActionPayload, CreateTodo.ActionMeta>
{
    // Optionally, you can use models defined within the action or external models like TodoItem.
    public sealed record ActionPayload(string Title);

    // Meta information can store additional data such as a timestamp.
    public sealed record ActionMeta(DateTime TimeStamp);

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey { get; init; } = "todos/create";

    // Action creators are represented as constructors.
    public CreateTodo(string title)
    {
        Payload = new ActionPayload(title);
        Meta = new ActionMeta(DateTime.UtcNow);
    }
}

public sealed record ToggleTodo
    : FluxStandardAction<ToggleTodo.ActionPayload, ToggleTodo.ActionMeta>
{
    // Payload containing the ID of the todo to be toggled.
    public sealed record ActionPayload(Guid Id);

    // Meta information can store additional data such as a timestamp.
    public sealed record ActionMeta(DateTime TimeStamp);

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey { get; init; } = "todos/toggle";

    // Action creators are represented as constructors.
    public ToggleTodo(Guid id)
    {
        Payload = new ActionPayload(id);
        Meta = new ActionMeta(DateTime.UtcNow);
    }
}

public sealed record DeleteTodo
    : FluxStandardAction<DeleteTodo.ActionPayload, DeleteTodo.ActionMeta>
{
    // Payload containing the ID of the todo to be deleted.
    public sealed record ActionPayload(Guid Id);

    // Meta information can store additional data such as a timestamp.
    public sealed record ActionMeta(DateTime TimeStamp);

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey { get; init; } = "todos/delete";

    // Action creators are represented as constructors.
    public DeleteTodo(Guid id)
    {
        Payload = new ActionPayload(id);
        Meta = new ActionMeta(DateTime.UtcNow);
    }
}

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
            Todos = state.Todos.Add(new TodoItem(action.Payload.Title))
        };

    private static TodoState MapToggleTodo(TodoState state, ToggleTodo action)
        => new()
        {
            Todos = state.Todos
                .Select(todo => todo.Id == action.Payload.Id ? todo.ToggleIsCompleted() : todo)
                .ToImmutableList()
        };

    private static TodoState MapDeleteTodo(TodoState state, DeleteTodo action)
        => new()
        {
            Todos = state.Todos
                .Where(todo => todo.Id != action.Payload.Id)
                .ToImmutableList()
        };

    public override TodoState GetInitialState()
    {
        return new TodoState
        {
            Todos =
            [
                new TodoItem(SampleIds.Id1, "Learn Blazor", true),
                new TodoItem(SampleIds.Id2, "Learn Redux"),
                new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
                new TodoItem(SampleIds.Id4, "Create a Todo App", true),
                new TodoItem(SampleIds.Id5, "Publish a NuGet package")
            ]
        };
    }
}

#endregion

#region Slice

// ReSharper disable once UnusedType.Global
public record TodoSlice : Slice<TodoState>
{
    public override ReducerCollection<TodoState> Reducers { get; } = new TodoReducers();
}

#endregion
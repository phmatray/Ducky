// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Todos;

#region State

public record TodoState
    : NormalizedState<Guid, TodoItem, TodoState>
{
    // Selectors
    public ValueCollection<TodoItem> SelectCompletedTodos()
    {
        return SelectEntities(todo => todo.IsCompleted);
    }

    public int SelectCompletedTodosCount()
    {
        return SelectCompletedTodos().Count;
    }

    public bool SelectHasCompletedTodos()
    {
        return !SelectCompletedTodos().IsEmpty;
    }

    public ValueCollection<TodoItem> SelectActiveTodos()
    {
        return SelectEntities(todo => !todo.IsCompleted);
    }

    public int SelectActiveTodosCount()
    {
        return SelectActiveTodos().Count;
    }

    public bool SelectHasActiveTodos()
    {
        return !SelectActiveTodos().IsEmpty;
    }
}

#endregion

#region Actions

public sealed record CreateTodo
    : Fsa<CreateTodo.ActionPayload, CreateTodo.ActionMeta>
{
    // Action creators are represented as constructors.
    public CreateTodo(string title)
        : base(new ActionPayload(title), new ActionMeta(DateTime.UtcNow))
    {
    }

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey => "todos/create";

    // Optionally, you can use models defined within the action or external models like TodoItem.
    public sealed record ActionPayload(string Title);

    // Meta information can store additional data such as a timestamp.
    public sealed record ActionMeta(DateTime TimeStamp);
}

public sealed record ToggleTodo
    : Fsa<ToggleTodo.ActionPayload, ToggleTodo.ActionMeta>
{
    // Action creators are represented as constructors.
    public ToggleTodo(in Guid id)
        : base(new ActionPayload(id), new ActionMeta(DateTime.UtcNow))
    {
    }

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey => "todos/toggle";

    // Payload containing the ID of the todo to be toggled.
    public sealed record ActionPayload(Guid Id);

    // Meta information can store additional data such as a timestamp.
    public sealed record ActionMeta(DateTime TimeStamp);
}

public sealed record DeleteTodo
    : Fsa<DeleteTodo.ActionPayload, ActionMeta>
{
    // Action creators are represented as constructors.
    // You can also use the ActionMeta.Create() method from Ducky to create a timestamp.
    public DeleteTodo(in Guid id)
        : base(new ActionPayload(id), ActionMeta.Create())
    {
    }

    // [Recommended] Write Action Types as domain/eventName
    public override string TypeKey => "todos/delete";

    // Payload containing the ID of the todo to be deleted.
    public sealed record ActionPayload(Guid Id);
}

#endregion

#region Reducers

public record TodoReducers : SliceReducers<TodoState>
{
    public TodoReducers()
    {
        On<CreateTodo>(ReduceCreateTodo);
        On<ToggleTodo>(ReduceToggleTodo);
        On<DeleteTodo>(ReduceDeleteTodo);
    }

    public override TodoState GetInitialState()
    {
        return TodoState.Create([
            new TodoItem(SampleIds.Id1, "Learn Blazor", true),
            new TodoItem(SampleIds.Id2, "Learn Redux"),
            new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
            new TodoItem(SampleIds.Id4, "Create a Todo App", true),
            new TodoItem(SampleIds.Id5, "Publish a NuGet package")
        ]);
    }

    private static TodoState ReduceCreateTodo(TodoState state, CreateTodo action)
    {
        return state.SetOne(new TodoItem(action.Payload.Title));
    }

    private static TodoState ReduceToggleTodo(TodoState state, ToggleTodo action)
    {
        return state.UpdateOne(action.Payload.Id, todo => todo.IsCompleted = !todo.IsCompleted);
    }

    private static TodoState ReduceDeleteTodo(TodoState state, DeleteTodo action)
    {
        return state.RemoveOne(action.Payload.Id);
    }
}

#endregion

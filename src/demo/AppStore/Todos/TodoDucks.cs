// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore;

#region State

public record TodoState
    : NormalizedState<Guid, TodoItem, TodoState>
{
    // Selectors
    public ImmutableList<TodoItem> SelectCompletedTodos()
        => SelectImmutableList(todo => todo.IsCompleted);

    public int SelectCompletedTodosCount()
        => SelectCompletedTodos().Count;

    public bool SelectHasCompletedTodos()
        => !SelectCompletedTodos().IsEmpty;

    public ImmutableList<TodoItem> SelectActiveTodos()
        => SelectImmutableList(todo => !todo.IsCompleted);

    public int SelectActiveTodosCount()
        => SelectActiveTodos().Count;

    public bool SelectHasActiveTodos()
        => !SelectActiveTodos().IsEmpty;
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
    public ToggleTodo(Guid id)
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
    // You can also use the ActionMeta.Create() method from R3dux to create a timestamp.
    public DeleteTodo(Guid id)
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
        Map<CreateTodo>(MapCreateTodo);
        Map<ToggleTodo>(MapToggleTodo);
        Map<DeleteTodo>(MapDeleteTodo);
    }

    public override TodoState GetInitialState()
        => TodoState.Create([
            new TodoItem(SampleIds.Id1, "Learn Blazor", true),
            new TodoItem(SampleIds.Id2, "Learn Redux"),
            new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
            new TodoItem(SampleIds.Id4, "Create a Todo App", true),
            new TodoItem(SampleIds.Id5, "Publish a NuGet package")
        ]);

    private static TodoState MapCreateTodo(TodoState state, CreateTodo action)
        => state.SetOne(new TodoItem(action.Payload.Title));

    private static TodoState MapToggleTodo(TodoState state, ToggleTodo action)
        => state.UpdateOne(action.Payload.Id, todo => todo.IsCompleted = !todo.IsCompleted);

    private static TodoState MapDeleteTodo(TodoState state, DeleteTodo action)
        => state.RemoveOne(action.Payload.Id);
}

#endregion

// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

#region State

public record TodoState
    : NormalizedState<Guid, TodoItem, TodoState>
{
    // Selectors
    public ImmutableArray<TodoItem> SelectCompletedTodos()
        => SelectEntities(todo => todo.IsCompleted);

    public int SelectCompletedTodosCount()
        => SelectCompletedTodos().Length;

    public bool SelectHasCompletedTodos()
        => !SelectCompletedTodos().IsEmpty;

    public ImmutableArray<TodoItem> SelectActiveTodos()
        => SelectEntities(todo => !todo.IsCompleted);

    public int SelectActiveTodosCount()
        => SelectActiveTodos().Length;

    public bool SelectHasActiveTodos()
        => !SelectActiveTodos().IsEmpty;
}

#endregion

#region Actions

[DuckyAction]
public sealed partial record CreateTodo(string Title);

[DuckyAction]
public sealed partial record ToggleTodo(Guid Id);

[DuckyAction]
public sealed partial record DeleteTodo(Guid Id);

#endregion

#region Reducers

public record TodoReducers : SliceReducers<TodoState>
{
    public TodoReducers()
    {
        On<CreateTodo>(Reduce);
        On<ToggleTodo>(Reduce);
        On<DeleteTodo>(Reduce);
    }

    public override TodoState GetInitialState()
        => TodoState.Create([
            new TodoItem(SampleIds.Id1, "Learn Blazor", true),
            new TodoItem(SampleIds.Id2, "Learn Redux"),
            new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
            new TodoItem(SampleIds.Id4, "Create a Todo App", true),
            new TodoItem(SampleIds.Id5, "Publish a NuGet package")
        ]);

    private static TodoState Reduce(TodoState state, CreateTodo action)
        => state.SetOne(new TodoItem(action.Title));

    private static TodoState Reduce(TodoState state, ToggleTodo action)
        => state.UpdateOne(action.Id, todo => new TodoItem(todo.Id, todo.Title, !todo.IsCompleted));

    private static TodoState Reduce(TodoState state, DeleteTodo action)
        => state.RemoveOne(action.Id);
}

#endregion

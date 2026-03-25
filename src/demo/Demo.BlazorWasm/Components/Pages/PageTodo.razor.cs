// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;

namespace Demo.BlazorWasm.Components.Pages;

public partial class PageTodo
{
    private string _newTodo = string.Empty;

    private ValueCollection<TodoItem> ActiveTodos
        => State.SelectActiveTodos();

    private int ActiveTodosCount
        => State.SelectActiveTodosCount();

    private bool HasActiveTodos
        => State.SelectHasActiveTodos();

    private ValueCollection<TodoItem> CompletedTodos
        => State.SelectCompletedTodos();

    private int CompletedTodosCount
        => State.SelectCompletedTodosCount();

    private void CreateTodoItem()
    {
        if (string.IsNullOrWhiteSpace(_newTodo))
        {
            return;
        }

        Dispatcher.CreateTodo(_newTodo);
        _newTodo = string.Empty;
    }

    private void ToggleTodoItem(Guid id)
    {
        Dispatcher.ToggleTodo(id);
    }

    private void DeleteTodoItem(Guid id)
    {
        Dispatcher.DeleteTodo(id);
    }
}

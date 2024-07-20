// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using R3dux.Selectors;

namespace R3dux.Tests.Extensions.Selectors;

internal sealed record TodoState : NormalizedState<Guid, TodoItem, TodoState>
{
    private readonly Func<TodoState, ImmutableList<TodoItem>> _selectCompletedTodos;
    private readonly Func<TodoState, int> _selectCompletedTodosCount;
    private readonly Func<TodoState, bool> _selectHasCompletedTodos;

    private readonly Func<TodoState, ImmutableList<TodoItem>> _selectActiveTodos;
    private readonly Func<TodoState, int> _selectActiveTodosCount;
    private readonly Func<TodoState, bool> _selectHasActiveTodos;

    public TodoState()
    {
        _selectCompletedTodos = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            state => state.SelectImmutableList(todo => todo.IsCompleted),
            state => state.ById);

        _selectCompletedTodosCount = MemoizedSelector.Compose(
            _selectCompletedTodos,
            todos => todos.Count,
            state => state.ById);

        _selectHasCompletedTodos = MemoizedSelector.Compose(
            _selectCompletedTodos,
            todos => !todos.IsEmpty,
            state => state.ById);

        _selectActiveTodos = MemoizedSelector.Create<TodoState, ImmutableList<TodoItem>>(
            state => state.SelectImmutableList(todo => !todo.IsCompleted),
            state => state.ById);

        _selectActiveTodosCount = MemoizedSelector.Compose(
            _selectActiveTodos,
            todos => todos.Count,
            state => state.ById);

        _selectHasActiveTodos = MemoizedSelector.Compose(
            _selectActiveTodos,
            todos => !todos.IsEmpty,
            state => state.ById);
    }

    // Memoized Selectors
    public ImmutableList<TodoItem> SelectCompletedTodos()
        => _selectCompletedTodos(this);

    public int SelectCompletedTodosCount()
        => _selectCompletedTodosCount(this);

    public bool SelectHasCompletedTodos()
        => _selectHasCompletedTodos(this);

    public ImmutableList<TodoItem> SelectActiveTodos()
        => _selectActiveTodos(this);

    public int SelectActiveTodosCount()
        => _selectActiveTodosCount(this);

    public bool SelectHasActiveTodos()
        => _selectHasActiveTodos(this);
}

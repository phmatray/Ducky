// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Tests.Todos;

public sealed class TodoReducersTests : IDisposable
{
    private const string Key = "todo";

    private readonly TodoReducers _sut = new();

    private readonly TodoState _initialState = TodoState.Create([
        new TodoItem(SampleIds.Id1, "Learn Blazor", true),
        new TodoItem(SampleIds.Id2, "Learn Redux"),
        new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
        new TodoItem(SampleIds.Id4, "Create a Todo App", true),
        new TodoItem(SampleIds.Id5, "Publish a NuGet package")
    ]);

    private bool _disposed;

    [Fact]
    public void TodoReducers_Should_Return_Initial_State()
    {
        // Act
        TodoState initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }

    [Fact]
    public void TodoReducers_Should_Return_Key()
    {
        // Act
        string key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void TodoReducers_Should_Return_Correct_State_Type()
    {
        // Act
        Type stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be<TodoState>();
    }

    [Fact]
    public void TodoReducers_Should_Return_Reducers()
    {
        // Act
        Dictionary<Type, Func<TodoState, IAction, TodoState>> reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(3);
    }

    [Fact]
    public void CreateTodo_ShouldAddNewTodoItem()
    {
        // Arrange
        TodoState state = TodoState.Create([]);
        const string newTitle = "New Todo";

        // Act
        TodoState newState = _sut.Reduce(state, new CreateTodo(newTitle));

        // Assert
        newState.SelectEntities().Should().ContainSingle(todo => todo.Title == newTitle && !todo.IsCompleted);
    }

    [Fact]
    public void ToggleTodo_ShouldToggleIsCompleted()
    {
        // Arrange
        TodoItem todoItem = new("Sample Todo");
        TodoState state = TodoState.Create([todoItem]);

        // Act
        TodoState newState = _sut.Reduce(state, new ToggleTodo(todoItem.Id));

        // Assert
        newState[todoItem.Id].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteTodo_ShouldRemoveTodoItem()
    {
        // Arrange
        TodoItem todoItem = new("Sample Todo");
        TodoState state = TodoState.Create([todoItem]);

        // Act
        TodoState newState = _sut.Reduce(state, new DeleteTodo(todoItem.Id));

        // Assert
        newState.SelectEntities().Should().NotContain(todo => todo.Id == todoItem.Id);
    }

    [Fact]
    public void SelectActiveTodos_ShouldReturnOnlyActiveTodos()
    {
        // Arrange
        TodoItem activeTodo = new("Active Todo");
        TodoItem completedTodo = new("Completed Todo", true);
        TodoState state = TodoState.Create([activeTodo, completedTodo]);

        // Act
        ValueCollection<TodoItem> activeTodos = state.SelectActiveTodos();

        // Assert
        activeTodos.Should().ContainSingle(todo => todo.Id == activeTodo.Id);
        activeTodos.Should().NotContain(todo => todo.Id == completedTodo.Id);
    }

    [Fact]
    public void SelectActiveTodosCount_ShouldReturnCorrectCount()
    {
        // Arrange
        TodoItem activeTodo = new("Active Todo");
        TodoItem completedTodo = new("Completed Todo", true);
        TodoState state = TodoState.Create([activeTodo, completedTodo]);

        // Act
        int activeTodosCount = state.SelectActiveTodosCount();

        // Assert
        activeTodosCount.Should().Be(1);
    }

    [Fact]
    public void SelectCompletedTodos_ShouldReturnOnlyCompletedTodos()
    {
        // Arrange
        TodoItem activeTodo = new("Active Todo");
        TodoItem completedTodo = new("Completed Todo", true);
        TodoState state = TodoState.Create([activeTodo, completedTodo]);

        // Act
        ValueCollection<TodoItem> completedTodos = state.SelectCompletedTodos();

        // Assert
        completedTodos.Should().ContainSingle(todo => todo.Id == completedTodo.Id);
        completedTodos.Should().NotContain(todo => todo.Id == activeTodo.Id);
    }

    [Fact]
    public void SelectCompletedTodosCount_ShouldReturnCorrectCount()
    {
        // Arrange
        TodoItem activeTodo = new("Active Todo");
        TodoItem completedTodo = new("Completed Todo", true);
        TodoState state = TodoState.Create([activeTodo, completedTodo]);

        // Act
        int completedTodosCount = state.SelectCompletedTodosCount();

        // Assert
        completedTodosCount.Should().Be(1);
    }

    public void Dispose()
    {
        Dispose(true);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _sut.Dispose();
        }

        _disposed = true;
    }
}

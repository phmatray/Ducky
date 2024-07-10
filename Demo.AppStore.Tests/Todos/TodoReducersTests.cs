using FluentAssertions;

namespace Demo.AppStore.Tests.Todos;

public class TodoReducersTests
{
    private readonly TodoReducers _reducers = new();

    [Fact]
    public void CreateTodo_ShouldAddNewTodoItem()
    {
        // Arrange
        var initialState = new TodoState { Todos = [] };
        const string newTitle = "New Todo";

        // Act
        var newState = _reducers.Reduce(initialState, new CreateTodo(newTitle));

        // Assert
        newState.Todos.Should().ContainSingle(todo => todo.Title == newTitle && !todo.IsCompleted);
    }

    [Fact]
    public void ToggleTodo_ShouldToggleIsCompleted()
    {
        // Arrange
        var todoItem = new TodoItem("Sample Todo");
        var initialState = new TodoState { Todos = [todoItem] };

        // Act
        var newState = _reducers.Reduce(initialState, new ToggleTodo(todoItem.Id));

        // Assert
        newState.Todos.Single(todo => todo.Id == todoItem.Id).IsCompleted.Should().Be(!todoItem.IsCompleted);
    }

    [Fact]
    public void DeleteTodo_ShouldRemoveTodoItem()
    {
        // Arrange
        var todoItem = new TodoItem("Sample Todo");
        var initialState = new TodoState { Todos = [todoItem] };

        // Act
        var newState = _reducers.Reduce(initialState, new DeleteTodo(todoItem.Id));

        // Assert
        newState.Todos.Should().NotContain(todo => todo.Id == todoItem.Id);
    }

    [Fact]
    public void SelectActiveTodos_ShouldReturnOnlyActiveTodos()
    {
        // Arrange
        var activeTodo = new TodoItem("Active Todo");
        var completedTodo = new TodoItem("Completed Todo").ToggleIsCompleted();
        var state = new TodoState { Todos = [activeTodo, completedTodo] };

        // Act
        var activeTodos = state.SelectActiveTodos();

        // Assert
        activeTodos.Should().ContainSingle(todo => todo.Id == activeTodo.Id);
        activeTodos.Should().NotContain(todo => todo.Id == completedTodo.Id);
    }

    [Fact]
    public void SelectActiveTodosCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var activeTodo = new TodoItem("Active Todo");
        var completedTodo = new TodoItem("Completed Todo").ToggleIsCompleted();
        var state = new TodoState { Todos = [activeTodo, completedTodo] };

        // Act
        var activeTodosCount = state.SelectActiveTodosCount();

        // Assert
        activeTodosCount.Should().Be(1);
    }

    [Fact]
    public void SelectCompletedTodos_ShouldReturnOnlyCompletedTodos()
    {
        // Arrange
        var activeTodo = new TodoItem("Active Todo");
        var completedTodo = new TodoItem("Completed Todo").ToggleIsCompleted();
        var state = new TodoState { Todos = [activeTodo, completedTodo] };

        // Act
        var completedTodos = state.SelectCompletedTodos();

        // Assert
        completedTodos.Should().ContainSingle(todo => todo.Id == completedTodo.Id);
        completedTodos.Should().NotContain(todo => todo.Id == activeTodo.Id);
    }

    [Fact]
    public void SelectCompletedTodosCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var activeTodo = new TodoItem("Active Todo");
        var completedTodo = new TodoItem("Completed Todo").ToggleIsCompleted();
        var state = new TodoState { Todos = [activeTodo, completedTodo] };

        // Act
        var completedTodosCount = state.SelectCompletedTodosCount();

        // Assert
        completedTodosCount.Should().Be(1);
    }
}
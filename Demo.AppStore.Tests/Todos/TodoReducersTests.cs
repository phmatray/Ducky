namespace Demo.AppStore.Tests.Todos;

public class TodoReducersTests
{
    private readonly TodoReducers _sut = new();

    private readonly TodoState _initialState = TodoState.Create([
        new TodoItem(SampleIds.Id1, "Learn Blazor", true),
        new TodoItem(SampleIds.Id2, "Learn Redux"),
        new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
        new TodoItem(SampleIds.Id4, "Create a Todo App", true),
        new TodoItem(SampleIds.Id5, "Publish a NuGet package")
    ]);
    
    private const string Key = "todo";

    [Fact]
    public void TodoReducers_Should_Return_Initial_State()
    {
        // Act
        var initialState = _sut.GetInitialState();

        // Assert
        initialState.Should().BeEquivalentTo(_initialState);
    }
    
    [Fact]
    public void TodoReducers_Should_Return_Key()
    {
        // Act
        var key = _sut.GetKey();

        // Assert
        key.Should().Be(Key);
    }

    [Fact]
    public void TodoReducers_Should_Return_Correct_State_Type()
    {
        // Act
        var stateType = _sut.GetStateType();

        // Assert
        stateType.Should().Be(typeof(TodoState));
    }
    
    [Fact]
    public void TodoReducers_Should_Return_Reducers()
    {
        // Act
        var reducers = _sut.Reducers;

        // Assert
        reducers.Should().HaveCount(3);
    }

    [Fact]
    public void CreateTodo_ShouldAddNewTodoItem()
    {
        // Arrange
        var state = TodoState.Create([]);
        const string newTitle = "New Todo";

        // Act
        var newState = _sut.Reduce(state, new CreateTodo(newTitle));

        // Assert
        newState.SelectImmutableList().Should().ContainSingle(todo => todo.Title == newTitle && !todo.IsCompleted);
    }

    [Fact]
    public void ToggleTodo_ShouldToggleIsCompleted()
    {
        // Arrange
        var todoItem = new TodoItem("Sample Todo");
        var state = TodoState.Create([todoItem]);

        // Act
        var newState = _sut.Reduce(state, new ToggleTodo(todoItem.Id));

        // Assert
        newState[todoItem.Id].IsCompleted.Should().BeTrue();
    }

    [Fact]
    public void DeleteTodo_ShouldRemoveTodoItem()
    {
        // Arrange
        var todoItem = new TodoItem("Sample Todo");
        var state = TodoState.Create([todoItem]);

        // Act
        var newState = _sut.Reduce(state, new DeleteTodo(todoItem.Id));

        // Assert
        newState.SelectImmutableList().Should().NotContain(todo => todo.Id == todoItem.Id);
    }

    [Fact]
    public void SelectActiveTodos_ShouldReturnOnlyActiveTodos()
    {
        // Arrange
        var activeTodo = new TodoItem("Active Todo");
        var completedTodo = new TodoItem("Completed Todo", true);
        var state = TodoState.Create([activeTodo, completedTodo]);

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
        var completedTodo = new TodoItem("Completed Todo", true);
        var state = TodoState.Create([activeTodo, completedTodo]);

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
        var completedTodo = new TodoItem("Completed Todo", true);
        var state = TodoState.Create([activeTodo, completedTodo]);

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
        var completedTodo = new TodoItem("Completed Todo", true);
        var state = TodoState.Create([activeTodo, completedTodo]);

        // Act
        var completedTodosCount = state.SelectCompletedTodosCount();

        // Assert
        completedTodosCount.Should().Be(1);
    }
}
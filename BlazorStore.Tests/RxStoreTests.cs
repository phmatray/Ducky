namespace BlazorStore.Tests;

public class RxStoreTests
{
    private readonly ActionsSubject _actionsSubject;
    private readonly ReducerManager<TodoState> _reducerManager;
    private readonly State<TodoState> _state;
    private readonly RxStore<TodoState> _store;

    public RxStoreTests()
    {
        _actionsSubject = new ActionsSubject();
        _reducerManager = new ReducerManager<TodoState>(
            _actionsSubject,
            new TodoState(),
            new Dictionary<string, IActionReducer<TodoState>> { { "TodoReducer", new TodoReducer() } },
            new TodoReducerFactory());
        _state = new State<TodoState>(_actionsSubject, _reducerManager.Reducers, new TodoState());
        _store = new RxStore<TodoState>(_state, _actionsSubject, _reducerManager);
    }

    [Fact]
    public async Task Dispatch_Should_Update_State()
    {
        // Arrange
        var newTodoAction = new AddTodo("Learn Unit Testing");

        // Act
        _store.Dispatch(newTodoAction);

        // Assert
        await Task.Delay(100); // Ensure async operations complete
        _store.State.FirstAsync().Wait().Todos.Should().HaveCount(4);
        _store.State.FirstAsync().Wait().Todos[3].Title.Should().Be("Learn Unit Testing");
    }

    [Fact]
    public void AddReducer_Should_Add_New_Reducer()
    {
        // Arrange
        var customReducer = new CustomReducer();

        // Act
        _store.AddReducer("CustomReducer", customReducer);

        // Assert
        _reducerManager.CurrentReducers.Should().ContainKey("CustomReducer");
    }

    [Fact]
    public void RemoveReducer_Should_Remove_Reducer()
    {
        // Arrange
        var customReducer = new CustomReducer();
        _store.AddReducer("CustomReducer", customReducer);

        // Act
        _store.RemoveReducer("CustomReducer");

        // Assert
        _reducerManager.CurrentReducers.Should().NotContainKey("CustomReducer");
    }
}

public class CustomReducer : IActionReducer<TodoState>
{
    public TodoState Invoke(TodoState state, IAction action)
    {
        // Custom reducer logic
        return state;
    }
}
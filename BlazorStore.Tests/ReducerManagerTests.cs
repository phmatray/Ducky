using Moq;

namespace BlazorStore.Tests;

public class ReducerManagerTests
{
    private readonly ActionsSubject _actionsSubject;
    private readonly Mock<IActionReducerFactory<TodoState>> _reducerFactoryMock;
    private readonly ReducerManager<TodoState> _reducerManager;
    private readonly Mock<IActionReducer<TodoState>> _todoReducerMock;
    private readonly TodoState _initialState;

    public ReducerManagerTests()
    {
        _actionsSubject = new ActionsSubject();
        _reducerFactoryMock = new Mock<IActionReducerFactory<TodoState>>();
        _todoReducerMock = new Mock<IActionReducer<TodoState>>();
        _initialState = new TodoState();

        _reducerFactoryMock
            .Setup(factory => factory.CreateReducer(It.IsAny<IDictionary<string, IActionReducer<TodoState>>>(), It.IsAny<TodoState>()))
            .Returns(_todoReducerMock.Object);

        _reducerManager = new ReducerManager<TodoState>(
            _actionsSubject,
            _initialState,
            new Dictionary<string, IActionReducer<TodoState>>
            {
                { nameof(TodoReducer), _todoReducerMock.Object }
            },
            _reducerFactoryMock.Object);
    }

    [Fact]
    public void Constructor_Should_Initialize_Properties()
    {
        // Assert
        _reducerManager.CurrentReducers.Should().ContainKey(nameof(TodoReducer));
        _reducerManager.Reducers.Should().NotBeNull();
    }

    [Fact]
    public void AddReducer_Should_Add_New_Reducer()
    {
        // Arrange
        var customReducerMock = new Mock<IActionReducer<TodoState>>();
        var initialCallCount = _reducerFactoryMock.Invocations.Count;

        // Act
        _reducerManager.AddReducer("CustomReducer", customReducerMock.Object);

        // Assert
        _reducerManager.CurrentReducers.Should().ContainKey("CustomReducer");
        _reducerFactoryMock.Verify(factory => factory.CreateReducer(It.IsAny<IDictionary<string, IActionReducer<TodoState>>>(), It.IsAny<TodoState>()), Times.Exactly(initialCallCount + 1));
    }

    [Fact]
    public void RemoveReducer_Should_Remove_Existing_Reducer()
    {
        // Arrange
        var customReducerMock = new Mock<IActionReducer<TodoState>>();
        _reducerManager.AddReducer("CustomReducer", customReducerMock.Object);
        var initialCallCount = _reducerFactoryMock.Invocations.Count;

        // Act
        _reducerManager.RemoveReducer("CustomReducer");

        // Assert
        _reducerManager.CurrentReducers.Should().NotContainKey("CustomReducer");
        _reducerFactoryMock.Verify(factory => factory.CreateReducer(It.IsAny<IDictionary<string, IActionReducer<TodoState>>>(), It.IsAny<TodoState>()), Times.Exactly(initialCallCount + 1));
    }

    [Fact]
    public void UpdateReducers_Should_Notify_Dispatcher()
    {
        // Arrange
        var customReducerMock = new Mock<IActionReducer<TodoState>>();
        var initialCallCount = _reducerFactoryMock.Invocations.Count;
        var dispatcherInvoked = false;
        
        _actionsSubject.Actions.Subscribe(action =>
        {
            if (action is UpdateReducersAction)
            {
                dispatcherInvoked = true;
            }
        });

        // Act
        _reducerManager.AddReducer("CustomReducer", customReducerMock.Object);

        // Assert
        dispatcherInvoked.Should().BeTrue();
        _reducerFactoryMock.Verify(factory => factory.CreateReducer(It.IsAny<IDictionary<string, IActionReducer<TodoState>>>(), It.IsAny<TodoState>()), Times.Exactly(initialCallCount + 1));
    }

    [Fact]
    public void Dispose_Should_Complete_Subject()
    {
        // Act
        _reducerManager.Dispose();

        // Assert
        _reducerManager.Invoking(rm => rm.Reducers.Subscribe()).Should().Throw<ObjectDisposedException>();
    }
}
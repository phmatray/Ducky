using Ducky.Middlewares.AsyncEffect;

namespace Ducky.Tests.Middlewares;

public class AsyncEffectGroupTests
{
    private readonly Mock<IDispatcher> _dispatcherMock = new();
    private readonly Mock<IRootState> _rootStateMock = new();
    private readonly Mock<ILogger<TestEffectGroup>> _loggerMock = new();

    private IDispatcher Dispatcher => _dispatcherMock.Object;
    private IRootState RootState => _rootStateMock.Object;
    private ILogger<TestEffectGroup> Logger => _loggerMock.Object;

    [Fact]
    public void Constructor_Should_NotThrow()
    {
        // Act & Assert
        Should.NotThrow(() => new TestEffectGroup(Logger));
    }

    [Fact]
    public void SetDispatcher_Should_SetDispatcherProperty()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);

        // Act
        effectGroup.SetDispatcher(Dispatcher);

        // Assert
        effectGroup.Dispatcher.ShouldBe(Dispatcher);
    }

    [Fact]
    public void SetDispatcher_WithNull_Should_ThrowArgumentNullException()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);

        // Act & Assert
        Should.Throw<ArgumentNullException>(() => effectGroup.SetDispatcher(null!))
            .ParamName
            .ShouldBe("dispatcher");
    }

    [Fact]
    public void CanHandle_WithRegisteredAction_Should_ReturnTrue()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        TestAction1 action = new();

        // Act
        bool result = effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public void CanHandle_WithUnregisteredAction_Should_ReturnFalse()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        UnregisteredAction action = new();

        // Act
        bool result = effectGroup.CanHandle(action);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HandleAsync_WithRegisteredAction_Should_ExecuteHandler()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        effectGroup.SetDispatcher(Dispatcher);
        TestAction1 action = new() { Value = "test" };

        // Act
        await effectGroup.HandleAsync(action, RootState);

        // Assert
        effectGroup.HandledActions.ShouldContain(action);
    }

    [Fact]
    public async Task HandleAsync_WithUnregisteredAction_Should_NotThrow()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        effectGroup.SetDispatcher(Dispatcher);
        UnregisteredAction action = new();

        // Act & Assert
        await Should.NotThrowAsync(() => effectGroup.HandleAsync(action, RootState));
        effectGroup.HandledActions.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_MultipleActions_Should_ExecuteCorrectHandlers()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        effectGroup.SetDispatcher(Dispatcher);
        TestAction1 action1 = new() { Value = "test1" };
        TestAction2 action2 = new() { Number = 42 };

        // Act
        await effectGroup.HandleAsync(action1, RootState);
        await effectGroup.HandleAsync(action2, RootState);

        // Assert
        effectGroup.HandledActions.Count.ShouldBe(2);
        effectGroup.HandledActions.ShouldContain(action1);
        effectGroup.HandledActions.ShouldContain(action2);
    }

    [Fact]
    public void LastAction_Should_ReturnDispatcherLastAction()
    {
        // Arrange
        var effectGroup = new TestEffectGroup(Logger);
        effectGroup.SetDispatcher(Dispatcher);
        var expectedAction = new TestAction1();
        _dispatcherMock.Setup(x => x.LastAction).Returns(expectedAction);

        // Act
        object? result = effectGroup.LastAction;

        // Assert
        result.ShouldBe(expectedAction);
    }

    [Fact]
    public async Task SharedHelperMethods_Should_BeAccessibleAcrossHandlers()
    {
        // Arrange
        TestEffectGroup effectGroup = new(Logger);
        effectGroup.SetDispatcher(Dispatcher);
        TestAction1 action1 = new() { Value = "shared" };
        TestAction2 action2 = new() { Number = 10 };

        // Act
        await effectGroup.HandleAsync(action1, RootState);
        await effectGroup.HandleAsync(action2, RootState);

        // Assert
        effectGroup.SharedCounter.ShouldBe(2); // Both handlers increment the shared counter
    }

    // Test implementations
    private record TestAction1 { public string Value { get; init; } = string.Empty; }
    private record TestAction2 { public int Number { get; init; } }
    private record UnregisteredAction;

    public class TestEffectGroup : AsyncEffectGroup
    {
        private readonly ILogger<TestEffectGroup> _logger;

        public List<object> HandledActions { get; } = [];
        public int SharedCounter { get; private set; }

        public TestEffectGroup(ILogger<TestEffectGroup> logger)
        {
            _logger = logger;

            On<TestAction1>(HandleTestAction1Async);
            On<TestAction2>(HandleTestAction2Async);
        }

        private Task HandleTestAction1Async(TestAction1 action, IRootState rootState)
        {
            _logger.LogInformation("Handling TestAction1: {Value}", action.Value);
            HandledActions.Add(action);
            IncrementSharedCounter();
            return Task.CompletedTask;
        }

        private Task HandleTestAction2Async(TestAction2 action, IRootState rootState)
        {
            _logger.LogInformation("Handling TestAction2: {Number}", action.Number);
            HandledActions.Add(action);
            IncrementSharedCounter();
            return Task.CompletedTask;
        }

        // Shared helper method
        private void IncrementSharedCounter()
        {
            SharedCounter++;
        }
    }
}

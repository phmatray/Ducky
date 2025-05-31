using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ducky.Tests.Middlewares;

/// <summary>
/// Tests for the ReactiveEffectMiddleware.
/// </summary>
public class ReactiveEffectMiddlewareTests : IDisposable
{
    private readonly Mock<IStoreEventPublisher> _eventPublisherMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IStore> _storeMock;
    private readonly TestRootState _rootState;
    private ReactiveEffectMiddleware? _middleware;

    private const int ProcessingDelayMs = 50;

    public ReactiveEffectMiddlewareTests()
    {
        _eventPublisherMock = new Mock<IStoreEventPublisher>();
        _dispatcherMock = new Mock<IDispatcher>();
        _storeMock = new Mock<IStore>();
        _rootState = new TestRootState();
        _storeMock.Setup(s => s.CurrentState).Returns(_rootState);
    }

    public void Dispose()
    {
        _middleware?.Dispose();
    }

    private async Task<ReactiveEffectMiddleware> CreateInitializedMiddleware(params IReactiveEffect[] effects)
    {
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);
        return middleware;
    }

    [Fact]
    public void Constructor_WithNullEffects_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware(null!, _eventPublisherMock.Object));
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware([], null!));
    }

    [Fact]
    public async Task InitializeAsync_ShouldCompleteSuccessfully()
    {
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock.Object);

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // No exception means success
    }

    [Fact]
    public void MayDispatchAction_ShouldAlwaysReturnTrue()
    {
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock.Object);
        TestAction action = new();

        bool result = middleware.MayDispatchAction(action);

        Assert.True(result);
    }

    [Fact]
    public async Task AfterDispatch_ShouldEmitActionToEffects()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        _middleware = await CreateInitializedMiddleware(testEffect);

        // Trigger lazy initialization
        _middleware.BeforeDispatch(new TestAction());

        TestAction testAction = new();

        // Act
        _middleware.AfterDispatch(testAction);

        // Allow time for async processing
        await Task.Delay(ProcessingDelayMs, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(capturedActions);
        Assert.Same(testAction, capturedActions[0]);
    }

    [Fact]
    public async Task AfterDispatch_ShouldUpdateStateAfterAction()
    {
        // Arrange
        List<IRootState> capturedStates = [];
        TestStateCapturingEffect stateEffect = new(capturedStates);
        _middleware = await CreateInitializedMiddleware(stateEffect);

        // Trigger lazy initialization
        _middleware.BeforeDispatch(new TestAction());

        TestAction testAction = new();

        // Act
        _middleware.AfterDispatch(testAction);

        // Allow time for async processing
        await Task.Delay(ProcessingDelayMs, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEmpty(capturedStates);
        // The middleware emits initial empty state and then the actual state
        // Find the TestRootState instance in the captured states
        TestRootState? testRootState = capturedStates.OfType<TestRootState>().FirstOrDefault();
        Assert.NotNull(testRootState);
        Assert.Same(_rootState, testRootState);
        
        // Verify that the store's CurrentState was called
        _storeMock.Verify(s => s.CurrentState, Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReactiveEffect_DispatchesAction_PublishesDispatchedEvent()
    {
        // Arrange
        TestAction actionToDispatch = new();
        TestDispatchingEffect dispatchingEffect = new(actionToDispatch);
        _middleware = await CreateInitializedMiddleware(dispatchingEffect);

        // Trigger lazy initialization
        _middleware.BeforeDispatch(new TestAction());

        TestAction triggerAction = new();

        // Act
        _middleware.AfterDispatch(triggerAction);

        // Allow time for async processing
        await Task.Delay(ProcessingDelayMs, TestContext.Current.CancellationToken);

        // Assert
        _dispatcherMock.Verify(d => d.Dispatch(actionToDispatch), Times.Once);
        _eventPublisherMock.Verify(p => p.Publish(It.IsAny<ReactiveEffectDispatchedEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task ReactiveEffect_WithError_PublishesErrorEvent()
    {
        // Arrange
        TestErrorThrowingEffect errorEffect = new();
        _middleware = await CreateInitializedMiddleware(errorEffect);

        // Trigger lazy initialization
        _middleware.BeforeDispatch(new TestAction());

        TestAction testAction = new();

        // Act - Call BeforeDispatch to ensure initialization, then AfterDispatch to emit action
        _middleware.BeforeDispatch(testAction);
        _middleware.AfterDispatch(testAction);

        // Allow more time for async processing since error handling might be slower
        await Task.Delay(ProcessingDelayMs * 2, TestContext.Current.CancellationToken);

        // Assert
        _eventPublisherMock.Verify(p => p.Publish(It.IsAny<EffectErrorEventArgs>()), Times.AtLeastOnce);
    }

    [Fact]
    public void BeginInternalMiddlewareChange_ShouldReturnDisposable()
    {
        // Arrange
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock.Object);

        // Act
        IDisposable result = middleware.BeginInternalMiddlewareChange();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IDisposable>(result);
        result.Dispose(); // Verify dispose doesn't throw
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock.Object);

        // Act & Assert - no exception should be thrown
        middleware.Dispose();
        middleware.Dispose();
    }

    [Fact]
    public async Task AfterDispatch_AfterDispose_DoesNotProcessActions()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        _middleware = await CreateInitializedMiddleware(testEffect);

        // Trigger lazy initialization
        _middleware.BeforeDispatch(new TestAction());

        _middleware.Dispose();

        TestAction testAction = new();

        // Act
        _middleware.AfterDispatch(testAction);

        // Allow time for async processing
        await Task.Delay(ProcessingDelayMs, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(capturedActions);
    }

    // Test helper classes
    private sealed class TestRootState : IRootState
    {
        public T GetSlice<T>() where T : IState => default!;
        public IEnumerable<ISlice> GetSlices() => Array.Empty<ISlice>();
        public ImmutableSortedDictionary<string, object> GetStateDictionary() => ImmutableSortedDictionary<string, object>.Empty;
        public ImmutableSortedSet<string> GetKeys() => ImmutableSortedSet<string>.Empty;
        public TState GetSliceState<TState>(string key) where TState : notnull => default!;
        public TState GetSliceState<TState>() where TState : notnull => default!;
        public bool ContainsKey(string key) => false;
    }

    private sealed class TestReactiveEffect : ReactiveEffect
    {
        private readonly List<object> _capturedActions;

        public TestReactiveEffect(List<object> capturedActions)
        {
            _capturedActions = capturedActions;
        }

        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            actions.Subscribe(_capturedActions.Add);
            return Observable.Empty<object>();
        }
    }

    private sealed class TestStateCapturingEffect : ReactiveEffect
    {
        private readonly List<IRootState> _capturedStates;

        public TestStateCapturingEffect(List<IRootState> capturedStates)
        {
            _capturedStates = capturedStates;
        }

        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            rootState.Subscribe(_capturedStates.Add);
            return Observable.Empty<object>();
        }
    }

    private sealed class TestDispatchingEffect : ReactiveEffect
    {
        private readonly object _actionToDispatch;

        public TestDispatchingEffect(object actionToDispatch)
        {
            _actionToDispatch = actionToDispatch;
        }

        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            return actions.Take(1).Select(_ => _actionToDispatch);
        }
    }

    private sealed class TestErrorThrowingEffect : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            return actions.SelectMany<object, object>(_ => 
                Observable.Throw<object>(new InvalidOperationException("Test error")));
        }
    }
}

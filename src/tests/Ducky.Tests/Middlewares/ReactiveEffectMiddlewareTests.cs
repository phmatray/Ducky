using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;
using Ducky.Tests.TestModels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using R3;
using System.Collections.Immutable;

namespace Ducky.Tests.Middlewares;

/// <summary>
/// Tests for the ReactiveEffectMiddleware.
/// </summary>
public class ReactiveEffectMiddlewareTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IDispatcher _dispatcher;
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly Func<IRootState> _getState;
    private readonly TestRootState _rootState;
    private ReactiveEffectMiddleware? _middleware;

    public ReactiveEffectMiddlewareTests()
    {
        ServiceCollection services = [];
        _serviceProvider = services.BuildServiceProvider();
        _dispatcher = new Mock<IDispatcher>().Object;
        _eventPublisher = new Mock<IStoreEventPublisher>().Object;
        _rootState = new TestRootState();
        _getState = () => _rootState;
    }

    public void Dispose()
    {
        _middleware?.Dispose();
    }

    [Fact]
    public void Constructor_WithNullServices_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware(null!, _getState, _dispatcher, _eventPublisher));
    }

    [Fact]
    public void Constructor_WithNullGetState_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware(_serviceProvider, null!, _dispatcher, _eventPublisher));
    }

    [Fact]
    public void Constructor_WithNullDispatcher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware(_serviceProvider, _getState, null!, _eventPublisher));
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ReactiveEffectMiddleware(_serviceProvider, _getState, _dispatcher, null!));
    }

    [Fact]
    public void InvokeBeforeReduce_ReturnsActionsUnchanged()
    {
        // Arrange
        _middleware = new ReactiveEffectMiddleware(_serviceProvider, _getState, _dispatcher, _eventPublisher);
        Subject<ActionContext> actions = new();
        TestAction testAction = new();
        ActionContext context = new(testAction);

        // Act
        Observable<ActionContext> result = _middleware.InvokeBeforeReduce(actions);
        List<ActionContext> received = [];
        using IDisposable subscription = result.Subscribe(received.Add);
        
        actions.OnNext(context);

        // Assert
        Assert.Single(received);
        Assert.Same(context, received[0]);
    }

    [Fact]
    public void InvokeAfterReduce_EmitsActionToEffects()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(testEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);
        
        Subject<ActionContext> actions = new();
        TestAction testAction = new();
        ActionContext context = new(testAction);

        // Act
        Observable<ActionContext> result = _middleware.InvokeAfterReduce(actions);
        using IDisposable subscription = result.Subscribe();
        
        actions.OnNext(context);

        // Assert
        Assert.Single(capturedActions);
        Assert.Same(testAction, capturedActions[0]);
    }

    [Fact]
    public void InvokeAfterReduce_UpdatesStateAfterAction()
    {
        // Arrange
        List<IRootState> capturedStates = [];
        TestStateCapturingEffect stateEffect = new(capturedStates);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(stateEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);
        
        Subject<ActionContext> actions = new();
        TestAction testAction = new();
        ActionContext context = new(testAction);

        // Act
        Observable<ActionContext> result = _middleware.InvokeAfterReduce(actions);
        using IDisposable subscription = result.Subscribe();
        
        actions.OnNext(context);

        // Assert
        Assert.NotEmpty(capturedStates);
        Assert.Same(_rootState, capturedStates[0]);
    }

    [Fact]
    public void InvokeAfterReduce_WithAbortedContext_DoesNotProcessAction()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(testEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);
        
        Subject<ActionContext> actions = new();
        TestAction testAction = new();
        ActionContext context = new(testAction);
        context.Abort();

        // Act
        Observable<ActionContext> result = _middleware.InvokeAfterReduce(actions);
        using IDisposable subscription = result.Subscribe();
        
        actions.OnNext(context);

        // Assert
        Assert.Empty(capturedActions);
    }

    [Fact]
    public void ReactiveEffect_DispatchesAction_PublishesDispatchedEvent()
    {
        // Arrange
        TestAction actionToDispatch = new();
        TestDispatchingEffect dispatchingEffect = new(actionToDispatch);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(dispatchingEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);
        
        Subject<ActionContext> actions = new();
        TestAction triggerAction = new();
        ActionContext context = new(triggerAction);

        // Act
        Observable<ActionContext> result = _middleware.InvokeAfterReduce(actions);
        using IDisposable subscription = result.Subscribe();
        
        actions.OnNext(context);

        // Allow time for async processing
        Thread.Sleep(100);

        // Assert
        Mock<IDispatcher> dispatcherMock = Mock.Get(_dispatcher);
        Mock<IStoreEventPublisher> publisherMock = Mock.Get(_eventPublisher);
        dispatcherMock.Verify(d => d.Dispatch(actionToDispatch), Times.Once);
        publisherMock.Verify(p => p.Publish(It.IsAny<ReactiveEffectDispatchedEventArgs>()), Times.Once);
    }


    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(testEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);

        // Act
        _middleware.Dispose();
        _middleware.Dispose(); // Second call should not throw

        // Assert - just verify no exception is thrown
        Assert.True(true);
    }

    [Fact]
    public void InvokeAfterReduce_AfterDispose_DoesNotProcessActions()
    {
        // Arrange
        List<object> capturedActions = [];
        TestReactiveEffect testEffect = new(capturedActions);
        
        ServiceCollection services = [];
        services.AddSingleton<IReactiveEffect>(testEffect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();
        
        _middleware = new ReactiveEffectMiddleware(serviceProvider, _getState, _dispatcher, _eventPublisher);
        _middleware.Dispose();
        
        Subject<ActionContext> actions = new();
        TestAction testAction = new();
        ActionContext context = new(testAction);

        // Act
        Observable<ActionContext> result = _middleware.InvokeAfterReduce(actions);
        using IDisposable subscription = result.Subscribe();
        
        actions.OnNext(context);

        // Assert
        Assert.Empty(capturedActions);
    }

    // Test helper classes
    private class TestRootState : IRootState
    {
        public T GetSlice<T>() where T : IState => default!;
        public IEnumerable<ISlice> GetSlices() => [];
        public ImmutableSortedDictionary<string, object> GetStateDictionary() => ImmutableSortedDictionary<string, object>.Empty;
        public ImmutableSortedSet<string> GetKeys() => ImmutableSortedSet<string>.Empty;
        public TState GetSliceState<TState>(string key) where TState : notnull => default!;
        public TState GetSliceState<TState>() where TState : notnull => default!;
        public bool ContainsKey(string key) => false;
    }

    private class TestReactiveEffect : ReactiveEffect
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

    private class TestStateCapturingEffect : ReactiveEffect
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

    private class TestDispatchingEffect : ReactiveEffect
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


}

using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.AsyncEffectRetry;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Ducky.Tests.Middlewares;

public class AsyncEffectRetryMiddlewareTests : IDisposable
{
    private readonly Mock<ILogger<AsyncEffectRetryMiddleware>> _loggerMock;
    private readonly Mock<IStoreEventPublisher> _eventPublisherMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly IServiceProvider _serviceProvider;
    private readonly Func<IRootState> _getState;
    private readonly AsyncEffectRetryMiddleware _sut;
    private readonly List<StoreEventArgs> _publishedEvents = [];
    private readonly CompositeDisposable _disposables = [];

    public AsyncEffectRetryMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<AsyncEffectRetryMiddleware>>();
        _eventPublisherMock = new Mock<IStoreEventPublisher>();
        _dispatcherMock = new Mock<IDispatcher>();
        _getState = () => new RootState(ImmutableSortedDictionary<string, object>.Empty);

        // Capture published events
        _eventPublisherMock.Setup(x => x.Publish(It.IsAny<StoreEventArgs>()))
            .Callback<StoreEventArgs>(e => _publishedEvents.Add(e));

        // Setup service provider
        ServiceCollection services = [];
        _serviceProvider = services.BuildServiceProvider();

        _sut = new AsyncEffectRetryMiddleware(
            _loggerMock.Object,
            _eventPublisherMock.Object,
            _serviceProvider,
            _getState,
            _dispatcherMock.Object);
    }

    [Fact]
    public void InvokeBeforeReduce_ReturnsActionsUnchanged()
    {
        // Arrange
        Subject<ActionContext> actions = new();
        List<ActionContext> results = [];

        // Act
        _disposables.Add(_sut.InvokeBeforeReduce(actions).Subscribe(results.Add));

        ActionContext context = new(new TestAction());
        actions.OnNext(context);

        // Assert
        results.ShouldHaveSingleItem();
        results[0].ShouldBe(context);
    }

    [Fact]
    public async Task InvokeAfterReduce_ExecutesAsyncEffects_Async()
    {
        // Arrange
        TaskCompletionSource<bool> effectExecuted = new();
        TestAsyncEffect effect = new(effectExecuted);

        ServiceCollection services = [];
        services.AddSingleton<IAsyncEffect>(effect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        AsyncEffectRetryMiddleware middleware = new(
            _loggerMock.Object,
            _eventPublisherMock.Object,
            serviceProvider,
            _getState,
            _dispatcherMock.Object);

        Subject<ActionContext> actions = new();
        List<ActionContext> results = [];

        // Act
        _disposables.Add(middleware.InvokeAfterReduce(actions).Subscribe(results.Add));

        ActionContext context = new(new TestActionWithParameter("test"));
        actions.OnNext(context);

        // Assert
        await effectExecuted.Task;
        effect.HandledActions.ShouldHaveSingleItem();
        effect.HandledActions[0].ShouldBeOfType<TestActionWithParameter>();
        string name = ((TestActionWithParameter)effect.HandledActions[0]).Name;
        name.ShouldBe("test");
    }

    [Fact]
    public async Task InvokeAfterReduce_RetriesOnFailure_Async()
    {
        // Arrange
        FailingAsyncEffect effect = new(failCount: 1);

        ServiceCollection services = [];
        services.AddSingleton<IAsyncEffect>(effect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        AsyncEffectRetryMiddleware middleware = new(
            _loggerMock.Object,
            _eventPublisherMock.Object,
            serviceProvider,
            _getState,
            _dispatcherMock.Object);

        Subject<ActionContext> actions = new();

        // Act
        _disposables.Add(middleware.InvokeAfterReduce(actions).Subscribe());

        ActionContext context = new(new TestAction());
        actions.OnNext(context);

        // Wait for retries to complete
        // With exponential backoff: 1s + 2s + 4s = 7s, plus some buffer
        await Task.Delay(8000, TestContext.Current.CancellationToken); // Account for exponential backoff

        // Assert
        effect.AttemptCount.ShouldBe(2); // Initial + 1 retry that succeeds

        List<RetryAttemptEventArgs> retryEvents = _publishedEvents
            .OfType<RetryAttemptEventArgs>()
            .ToList();

        retryEvents.Count.ShouldBe(1);
        retryEvents[0].Attempt.ShouldBe(0); // Polly uses 0-based attempt numbers
    }

    [Fact]
    public async Task InvokeAfterReduce_DispatchesServiceUnavailable_WhenAllRetriesFail_Async()
    {
        // Arrange
        FailingAsyncEffect effect = new(failCount: 10); // Always fails

        ServiceCollection services = [];
        services.AddSingleton<IAsyncEffect>(effect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        Mock<IDispatcher> dispatcherMock = new();

        AsyncEffectRetryMiddleware middleware = new(
            _loggerMock.Object,
            _eventPublisherMock.Object,
            serviceProvider,
            _getState,
            dispatcherMock.Object);

        Subject<ActionContext> actions = new();

        // Act
        _disposables.Add(middleware.InvokeAfterReduce(actions).Subscribe());

        TestAction originalAction = new();
        ActionContext context = new(originalAction);
        actions.OnNext(context);

        // Wait for all retries to complete
        await Task.Delay(8000, TestContext.Current.CancellationToken); // Account for exponential backoff

        // Assert
        // The circuit breaker might open before all retries are exhausted
        dispatcherMock.Verify(
            d => d.Dispatch(
                It.Is<ServiceUnavailableAction>(a => ReferenceEquals(a.OriginalAction, originalAction)
                    && (a.Reason.Contains("All retry attempts failed") || a.Reason.Contains("Circuit breaker is open")))),
            Times.Once);

        List<ServiceUnavailableEventArgs> serviceUnavailableEvents = _publishedEvents
            .OfType<ServiceUnavailableEventArgs>()
            .ToList();

        serviceUnavailableEvents.Count.ShouldBe(1);
        string reason = serviceUnavailableEvents[0].Reason;
        bool isValidReason = reason is "All retry attempts failed" or "Circuit breaker is open";
        isValidReason.ShouldBeTrue($"Unexpected reason: '{reason}'");
    }

    [Fact]
    public async Task InvokeAfterReduce_OpensCircuitBreaker_AfterMultipleFailures_Async()
    {
        // Arrange
        AlwaysFailingAsyncEffect effect = new();

        ServiceCollection services = [];
        services.AddSingleton<IAsyncEffect>(effect);
        IServiceProvider serviceProvider = services.BuildServiceProvider();

        AsyncEffectRetryMiddleware middleware = new(
            _loggerMock.Object,
            _eventPublisherMock.Object,
            serviceProvider,
            _getState,
            _dispatcherMock.Object);

        Subject<ActionContext> actions = new();

        // Act
        _disposables.Add(middleware.InvokeAfterReduce(actions).Subscribe());

        // Send multiple actions to trigger circuit breaker
        for (int i = 0; i < 3; i++)
        {
            ActionContext context = new(new TestAction());
            actions.OnNext(context);
            await Task.Delay(8000, TestContext.Current.CancellationToken); // Wait for retries
        }

        // Assert
        List<CircuitBreakerOpenedEventArgs> circuitBreakerEvents = _publishedEvents
            .OfType<CircuitBreakerOpenedEventArgs>()
            .ToList();

        circuitBreakerEvents.ShouldNotBeEmpty();
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private class TestAsyncEffect : AsyncEffect<TestActionWithParameter>
    {
        private readonly TaskCompletionSource<bool> _taskCompletionSource;

        public List<object> HandledActions { get; } = [];

        public TestAsyncEffect(TaskCompletionSource<bool> taskCompletionSource)
        {
            _taskCompletionSource = taskCompletionSource;
        }

        public override Task HandleAsync(TestActionWithParameter action, IRootState rootState)
        {
            HandledActions.Add(action);
            _taskCompletionSource.SetResult(true);
            return Task.CompletedTask;
        }
    }

    private class FailingAsyncEffect : AsyncEffect<TestAction>
    {
        private readonly int _failCount;

        public int AttemptCount { get; private set; }

        public FailingAsyncEffect(int failCount)
        {
            _failCount = failCount;
        }

        public override Task HandleAsync(TestAction action, IRootState rootState)
        {
            AttemptCount++;
            if (AttemptCount <= _failCount)
            {
                throw new InvalidOperationException($"Simulated failure {AttemptCount}");
            }

            return Task.CompletedTask;
        }
    }

    private class AlwaysFailingAsyncEffect : AsyncEffect<TestAction>
    {
        public override Task HandleAsync(TestAction action, IRootState rootState)
        {
            throw new InvalidOperationException("Always fails");
        }
    }
}

using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;
using Ducky.Tests.TestModels;
using Moq;

namespace Ducky.Tests.Middlewares;

/// <summary>
/// Tests for the AsyncEffectMiddleware.
/// </summary>
public class AsyncEffectMiddlewareTests
{
    private readonly Mock<IStoreEventPublisher> _eventPublisherMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IStore> _storeMock;
    private readonly RootState _rootState;

    public AsyncEffectMiddlewareTests()
    {
        _eventPublisherMock = new Mock<IStoreEventPublisher>();
        _dispatcherMock = new Mock<IDispatcher>();
        _storeMock = new Mock<IStore>();
        _rootState = Factories.CreateTestRootState();
        _storeMock.Setup(s => s.CurrentState).Returns(_rootState);
    }

    private async Task<AsyncEffectMiddleware> CreateInitializedMiddleware(params IAsyncEffect[] effects)
    {
        AsyncEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);
        return middleware;
    }

    [Fact]
    public void Constructor_WithNullEffects_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(null!, _eventPublisherMock.Object));
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        IAsyncEffect[] effects = [];

        Assert.Throws<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(effects, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        IAsyncEffect[] effects = [];

        AsyncEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);

        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task InitializeAsync_SetsDispatcherOnAllEffects()
    {
        Mock<IAsyncEffect> effect1Mock = new();
        Mock<IAsyncEffect> effect2Mock = new();
        IAsyncEffect[] effects = [effect1Mock.Object, effect2Mock.Object];

        await CreateInitializedMiddleware(effects);

        effect1Mock.Verify(e => e.SetDispatcher(_dispatcherMock.Object), Times.Once);
        effect2Mock.Verify(e => e.SetDispatcher(_dispatcherMock.Object), Times.Once);
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyEffects_DoesNotThrow()
    {
        IAsyncEffect[] effects = [];

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effects);

        Assert.NotNull(middleware);
    }

    [Fact]
    public async Task AfterInitializeAllMiddlewares_DoesNotThrow()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();

        middleware.AfterInitializeAllMiddlewares();
    }

    [Fact]
    public async Task MayDispatchAction_AlwaysReturnsTrue()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();
        TestAction action = new();

        bool result = middleware.MayDispatchAction(action);

        Assert.True(result);
    }

    [Fact]
    public async Task BeforeDispatch_DoesNothing()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();
        TestAction action = new();

        middleware.BeforeDispatch(action);
    }

    [Fact]
    public async Task AfterDispatch_WithNoEffects_DoesNotThrow()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();
        TestAction action = new();

        middleware.AfterDispatch(action);
    }

    [Fact]
    public async Task AfterDispatch_WithEffectThatCannotHandle_DoesNotCallEffect()
    {
        Mock<IAsyncEffect> effectMock = new();
        effectMock.Setup(e => e.CanHandle(It.IsAny<object>())).Returns(false);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effectMock.Object);
        TestAction action = new();

        middleware.AfterDispatch(action);

        // Wait a short time to ensure async operations complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        effectMock.Verify(e => e.HandleAsync(It.IsAny<object>(), It.IsAny<IRootState>()), Times.Never);
    }

    [Fact]
    public async Task AfterDispatch_WithEffectThatCanHandle_CallsEffect()
    {
        Mock<IAsyncEffect> effectMock = new();
        effectMock.Setup(e => e.CanHandle(It.IsAny<object>())).Returns(true);
        effectMock.Setup(e => e.HandleAsync(It.IsAny<object>(), It.IsAny<IRootState>()))
            .Returns(Task.CompletedTask);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effectMock.Object);
        TestAction action = new();

        middleware.AfterDispatch(action);

        // Wait a short time to ensure async operations complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        effectMock.Verify(e => e.HandleAsync(action, _rootState), Times.Once);
    }

    [Fact]
    public async Task AfterDispatch_WithEffectThatThrows_PublishesErrorEvent()
    {
        Mock<IAsyncEffect> effectMock = new();
        TestException testException = new();
        effectMock.Setup(e => e.CanHandle(It.IsAny<object>())).Returns(true);
        effectMock.Setup(e => e.HandleAsync(It.IsAny<object>(), It.IsAny<IRootState>()))
            .ThrowsAsync(testException);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effectMock.Object);
        TestAction action = new();

        middleware.AfterDispatch(action);

        // Wait a longer time to ensure async operations and error handling complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        _eventPublisherMock.Verify(
            ep => ep.Publish(It.Is<EffectErrorEventArgs>(args =>
                ReferenceEquals(args.Exception, testException)
                    && args.EffectType == effectMock.Object.GetType()
                    && ReferenceEquals(args.Action, action))),
            Times.Once);
    }

    [Fact]
    public async Task BeginInternalMiddlewareChange_ReturnsDisposable()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();

        IDisposable disposable = middleware.BeginInternalMiddlewareChange();

        Assert.NotNull(disposable);
        disposable.Dispose(); // Should not throw
    }

    [Fact]
    public async Task AfterDispatch_FireAndForget_DoesNotBlockExecution()
    {
        Mock<IAsyncEffect> slowEffectMock = new();
        slowEffectMock.Setup(e => e.CanHandle(It.IsAny<object>())).Returns(true);
        slowEffectMock.Setup(e => e.HandleAsync(It.IsAny<object>(), It.IsAny<IRootState>()))
            .Returns(async () => await Task.Delay(200, TestContext.Current.CancellationToken));

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(slowEffectMock.Object);
        TestAction action = new();

        // Measure execution time - should return immediately
        DateTime startTime = DateTime.UtcNow;
        middleware.AfterDispatch(action);
        TimeSpan executionTime = DateTime.UtcNow - startTime;

        // Should complete almost immediately (fire and forget)
        Assert.True(
            executionTime.TotalMilliseconds < 50,
            $"AfterDispatch took {executionTime.TotalMilliseconds}ms, expected < 50ms");
    }
}

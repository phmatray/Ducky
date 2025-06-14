using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;

namespace Ducky.Tests.Middlewares;

/// <summary>
/// Tests for the AsyncEffectMiddleware.
/// </summary>
public class AsyncEffectMiddlewareTests
{
    private readonly IStoreEventPublisher _eventPublisher;
    private readonly IDispatcher _dispatcher;
    private readonly IStore _store;

    public AsyncEffectMiddlewareTests()
    {
        _eventPublisher = A.Fake<IStoreEventPublisher>();
        _dispatcher = A.Fake<IDispatcher>();
        _store = A.Fake<IStore>();
        // Note: We can't mock the CurrentState() extension method since it's static,
        // but the Store itself implements IStateProvider, so we can mock those methods directly
    }

    private async Task<AsyncEffectMiddleware> CreateInitializedMiddleware(params IAsyncEffect[] effects)
    {
        AsyncEffectMiddleware middleware = new(effects, _eventPublisher);
        await middleware.InitializeAsync(_dispatcher, _store);
        return middleware;
    }

    [Fact]
    public void Constructor_WithNullEffects_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(null!, _eventPublisher));
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        IAsyncEffect[] effects = [];

        Should.Throw<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(effects, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        IAsyncEffect[] effects = [];

        AsyncEffectMiddleware middleware = new(effects, _eventPublisher);

        middleware.ShouldNotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_SetsDispatcherOnAllEffects()
    {
        IAsyncEffect effect1 = A.Fake<IAsyncEffect>();
        IAsyncEffect effect2 = A.Fake<IAsyncEffect>();
        IAsyncEffect[] effects = [effect1, effect2];

        await CreateInitializedMiddleware(effects);

        A.CallTo(() => effect1.SetDispatcher(_dispatcher)).MustHaveHappenedOnceExactly();
        A.CallTo(() => effect2.SetDispatcher(_dispatcher)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task InitializeAsync_WithEmptyEffects_DoesNotThrow()
    {
        IAsyncEffect[] effects = [];

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effects);

        middleware.ShouldNotBeNull();
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

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task BeforeReduce_DoesNothing()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();
        TestAction action = new();

        middleware.BeforeReduce(action);
    }

    [Fact]
    public async Task AfterReduce_WithNoEffects_DoesNotThrow()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();
        TestAction action = new();

        middleware.AfterReduce(action);
    }

    [Fact]
    public async Task AfterReduce_WithEffectThatCannotHandle_DoesNotCallEffect()
    {
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(false);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        middleware.AfterReduce(action);

        // Wait a short time to ensure async operations complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored)).MustNotHaveHappened();
    }

    [Fact]
    public async Task AfterReduce_WithEffectThatCanHandle_CallsEffect()
    {
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored))
            .Returns(Task.CompletedTask);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        middleware.AfterReduce(action);

        // Wait a short time to ensure async operations complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        A.CallTo(() => effect.HandleAsync(action, A<IStateProvider>.That.Matches(sp => sp == _store))).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AfterReduce_WithEffectThatThrows_PublishesErrorEvent()
    {
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        TestException testException = new();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored))
            .ThrowsAsync(testException);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        middleware.AfterReduce(action);

        // Wait a longer time to ensure async operations and error handling complete
        await Task.Delay(100, TestContext.Current.CancellationToken);

        A.CallTo(() => _eventPublisher.Publish(A<EffectErrorEventArgs>.That.Matches(args =>
            ReferenceEquals(args.Exception, testException)
                && args.EffectType == effect.GetType()
                && ReferenceEquals(args.Action, action))))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task BeginInternalMiddlewareChange_ReturnsDisposable()
    {
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware();

        IDisposable disposable = middleware.BeginInternalMiddlewareChange();

        disposable.ShouldNotBeNull();
        disposable.Dispose(); // Should not throw
    }

    [Fact]
    public async Task AfterReduce_FireAndForget_DoesNotBlockExecution()
    {
        IAsyncEffect slowEffect = A.Fake<IAsyncEffect>();
        A.CallTo(() => slowEffect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => slowEffect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored))
            .ReturnsLazily(async () => await Task.Delay(200, TestContext.Current.CancellationToken));

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(slowEffect);
        TestAction action = new();

        // Measure execution time - should return immediately
        DateTime startTime = DateTime.UtcNow;
        middleware.AfterReduce(action);
        TimeSpan executionTime = DateTime.UtcNow - startTime;

        // Should complete almost immediately (fire and forget)
        executionTime.TotalMilliseconds
            .ShouldBeLessThan(50, $"AfterReduce took {executionTime.TotalMilliseconds}ms, expected < 50ms");
    }
}

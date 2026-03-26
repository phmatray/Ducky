// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.AsyncEffect;
using Ducky.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;

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
        AsyncEffectMiddleware middleware = new(effects, _eventPublisher, NullLogger<AsyncEffectMiddleware>.Instance);
        await middleware.InitializeAsync(_dispatcher, _store);
        return middleware;
    }

    [Fact]
    public void Constructor_WithNullEffects_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(null!, _eventPublisher, NullLogger<AsyncEffectMiddleware>.Instance));
    }

    [Fact]
    public void Constructor_WithNullEventPublisher_ThrowsArgumentNullException()
    {
        IAsyncEffect[] effects = [];

        Should.Throw<ArgumentNullException>(() =>
            new AsyncEffectMiddleware(effects, null!, NullLogger<AsyncEffectMiddleware>.Instance));
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        IAsyncEffect[] effects = [];

        AsyncEffectMiddleware middleware = new(effects, _eventPublisher, NullLogger<AsyncEffectMiddleware>.Instance);

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

        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task AfterReduce_WithEffectThatCanHandle_CallsEffect()
    {
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);
        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        middleware.AfterReduce(action);

        // Wait a short time to ensure async operations complete
        await Task.Delay(50, TestContext.Current.CancellationToken);

        A.CallTo(() => effect.HandleAsync(action, A<IStateProvider>.That.Matches(sp => sp == _store), A<CancellationToken>.Ignored))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AfterReduce_WithEffectThatThrows_PublishesErrorEvent()
    {
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        TestException testException = new();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
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
        A.CallTo(() => slowEffect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
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

    [Fact]
    public async Task AfterReduce_WithMultipleConcurrentEffects_AttributesExceptionsCorrectly()
    {
        // Arrange: two effects handle the same action, only the second one throws
        IAsyncEffect successEffect = A.Fake<IAsyncEffect>();
        IAsyncEffect failingEffect = A.Fake<IAsyncEffect>();
        TestException testException = new();

        A.CallTo(() => successEffect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => successEffect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.CompletedTask);

        A.CallTo(() => failingEffect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => failingEffect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ThrowsAsync(testException);

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(successEffect, failingEffect);
        TestAction action = new();

        // Act
        middleware.AfterReduce(action);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert: error attributed to the failing effect, not to IAsyncEffect
        A.CallTo(() => _eventPublisher.Publish(A<EffectErrorEventArgs>.That.Matches(args =>
            ReferenceEquals(args.Exception, testException)
                && args.EffectType == failingEffect.GetType())))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task AfterReduce_WithMultipleFailingEffects_PublishesAllErrorsWithCorrectAttribution()
    {
        // Arrange: three effects all throw different exceptions
        IAsyncEffect effect1 = A.Fake<IAsyncEffect>();
        IAsyncEffect effect2 = A.Fake<IAsyncEffect>();
        IAsyncEffect effect3 = A.Fake<IAsyncEffect>();
        TestException ex1 = new("error1");
        TestException ex2 = new("error2");
        TestException ex3 = new("error3");

        A.CallTo(() => effect1.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect1.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ThrowsAsync(ex1);

        A.CallTo(() => effect2.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect2.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ThrowsAsync(ex2);

        A.CallTo(() => effect3.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect3.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ThrowsAsync(ex3);

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect1, effect2, effect3);
        TestAction action = new();

        // Act
        middleware.AfterReduce(action);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Assert: each error attributed to the correct effect
        A.CallTo(() => _eventPublisher.Publish(A<EffectErrorEventArgs>.That.Matches(args =>
            ReferenceEquals(args.Exception, ex1) && args.EffectType == effect1.GetType())))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _eventPublisher.Publish(A<EffectErrorEventArgs>.That.Matches(args =>
            ReferenceEquals(args.Exception, ex2) && args.EffectType == effect2.GetType())))
            .MustHaveHappenedOnceExactly();

        A.CallTo(() => _eventPublisher.Publish(A<EffectErrorEventArgs>.That.Matches(args =>
            ReferenceEquals(args.Exception, ex3) && args.EffectType == effect3.GetType())))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task Dispose_CancelsPendingEffects()
    {
        // Arrange: effect that observes cancellation
        CancellationToken capturedToken = default;
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily(call =>
            {
                capturedToken = call.Arguments.Get<CancellationToken>(2)!;
                return Task.Delay(5000, capturedToken);
            });

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        // Act
        middleware.AfterReduce(action);
        await Task.Delay(50, TestContext.Current.CancellationToken); // Let the effect start
        middleware.Dispose();

        // Assert
        capturedToken.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public async Task AfterReduce_PassesCancellationTokenToEffect()
    {
        // Arrange
        CancellationToken capturedToken = default;
        IAsyncEffect effect = A.Fake<IAsyncEffect>();
        A.CallTo(() => effect.CanHandle(A<object>.Ignored)).Returns(true);
        A.CallTo(() => effect.HandleAsync(A<object>.Ignored, A<IStateProvider>.Ignored, A<CancellationToken>.Ignored))
            .ReturnsLazily(call =>
            {
                capturedToken = call.Arguments.Get<CancellationToken>(2)!;
                return Task.CompletedTask;
            });

        AsyncEffectMiddleware middleware = await CreateInitializedMiddleware(effect);
        TestAction action = new();

        // Act
        middleware.AfterReduce(action);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert: a non-default cancellation token was passed
        capturedToken.ShouldNotBe(CancellationToken.None);
        capturedToken.CanBeCanceled.ShouldBeTrue();
    }
}

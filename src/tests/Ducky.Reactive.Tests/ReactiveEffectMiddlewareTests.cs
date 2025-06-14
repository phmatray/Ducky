// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class ReactiveEffectMiddlewareTests
{
    private readonly IStoreEventPublisher _eventPublisherMock;
    private readonly IDispatcher _dispatcherMock;
    private readonly IStore _storeMock;
    private readonly IRootState _rootStateMock;

    public ReactiveEffectMiddlewareTests()
    {
        _eventPublisherMock = A.Fake<IStoreEventPublisher>();
        _dispatcherMock = A.Fake<IDispatcher>();
        _storeMock = A.Fake<IStore>();
        _rootStateMock = A.Fake<IRootState>();

        A.CallTo(() => _storeMock.CurrentState()).Returns(_rootStateMock);
    }

    [Fact]
    public async Task InitializeAsync_WithNoEffects_ShouldInitializeSuccessfully()
    {
        // Arrange
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock);

        // Act
        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Assert - No exceptions thrown
        true.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithSingleEffect_ShouldSubscribeToEffectActions()
    {
        // Arrange
        TestReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        // Act
        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Assert
        effect.WasHandleCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task AfterReduce_WithAction_ShouldStreamActionToEffects()
    {
        // Arrange
        TestReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);
        object testAction = new { Type = "TEST_ACTION" };

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        middleware.AfterReduce(testAction);

        // Wait for observable to process
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert
        effect.ReceivedActions.ShouldContain(testAction);
    }

    [Fact]
    public async Task AfterReduce_WithMultipleActions_ShouldStreamAllActions()
    {
        // Arrange
        TestReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);
        object[] testActions =
        [
            new { Type = "ACTION_1" },
            new { Type = "ACTION_2" },
            new { Type = "ACTION_3" }
        ];

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        foreach (object action in testActions)
        {
            middleware.AfterReduce(action);
        }

        // Wait for observables to process
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        effect.ReceivedActions.Count.ShouldBe(testActions.Length);
        foreach (object action in testActions)
        {
            effect.ReceivedActions.ShouldContain(action);
        }
    }

    [Fact]
    public async Task EffectDispatchesAction_ShouldCallDispatcher()
    {
        // Arrange
        DispatchingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);
        object triggerAction = new { Type = "TRIGGER" };

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        middleware.AfterReduce(triggerAction);

        // Wait for effect to process and dispatch
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _dispatcherMock.Dispatch(A<object>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task EffectThrowsException_ShouldPublishErrorEvent()
    {
        // Arrange
        ErrorThrowingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);
        object triggerAction = new { Type = "TRIGGER" };

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        middleware.AfterReduce(triggerAction);

        // Wait for error to propagate
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        A.CallTo(() => _eventPublisherMock.Publish(A<EffectErrorEventArgs>.Ignored)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task StateChanges_ShouldStreamToEffects()
    {
        // Arrange
        StateTrackingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        IRootState newStateMock = A.Fake<IRootState>();
        A.CallTo(() => _storeMock.CurrentState()).Returns(newStateMock);

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        middleware.AfterReduce(new { Type = "UPDATE_STATE" });

        // Wait for state to update
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        effect.ReceivedStates.ShouldNotBeEmpty();
    }

    [Fact]
    public void BeginInternalMiddlewareChange_ShouldReturnDisposable()
    {
        // Arrange
        ReactiveEffectMiddleware middleware = new([], _eventPublisherMock);

        // Act
        IDisposable disposable = middleware.BeginInternalMiddlewareChange();

        // Assert
        disposable.ShouldNotBeNull();

        // Should not throw when disposed
        Should.NotThrow(() => disposable.Dispose());
    }

    // Test helper classes
    private class TestReactiveEffect : ReactiveEffect
    {
        public bool WasHandleCalled { get; private set; }
        public List<object> ReceivedActions { get; } = [];

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        {
            WasHandleCalled = true;

            // Track all received actions
            actions.Subscribe(action => ReceivedActions.Add(action));

            return Observable.Empty<object>();
        }
    }

    private class DispatchingReactiveEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        {
            return actions
                .Where(action => action.ToString()!.Contains("TRIGGER"))
                .Select(_ => new { Type = "DISPATCHED_FROM_EFFECT" });
        }
    }

    private class ErrorThrowingReactiveEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        {
            return actions
                .Where(action => action.ToString()!.Contains("TRIGGER"))
                .Select<object, object>(_ => throw new InvalidOperationException("Test exception"));
        }
    }

    private class StateTrackingReactiveEffect : ReactiveEffect
    {
        public List<IStateProvider> ReceivedStates { get; } = [];

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        {
            stateProvider.Subscribe(state => ReceivedStates.Add(state));
            return Observable.Empty<object>();
        }
    }
}
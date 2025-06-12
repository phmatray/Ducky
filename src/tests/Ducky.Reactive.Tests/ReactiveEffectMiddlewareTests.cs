// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Reactive.Middlewares.ReactiveEffects;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Reactive.Tests;

public class ReactiveEffectMiddlewareTests
{
    private readonly Mock<IStoreEventPublisher> _eventPublisherMock;
    private readonly Mock<IDispatcher> _dispatcherMock;
    private readonly Mock<IStore> _storeMock;
    private readonly Mock<IRootState> _rootStateMock;
    private readonly FakeTimeProvider _timeProvider;

    public ReactiveEffectMiddlewareTests()
    {
        _eventPublisherMock = new Mock<IStoreEventPublisher>();
        _dispatcherMock = new Mock<IDispatcher>();
        _storeMock = new Mock<IStore>();
        _rootStateMock = new Mock<IRootState>();
        _timeProvider = new FakeTimeProvider();
        
        _storeMock.Setup(s => s.CurrentState).Returns(_rootStateMock.Object);
    }

    [Fact]
    public async Task InitializeAsync_WithNoEffects_ShouldInitializeSuccessfully()
    {
        // Arrange
        List<ReactiveEffect> effects = [];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);

        // Act
        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // Assert - No exceptions thrown
        true.ShouldBeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithSingleEffect_ShouldSubscribeToEffectActions()
    {
        // Arrange
        TestReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);

        // Act
        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // Assert
        effect.WasHandleCalled.ShouldBeTrue();
    }

    [Fact]
    public async Task AfterReduce_WithAction_ShouldStreamActionToEffects()
    {
        // Arrange
        TestReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        object testAction = new { Type = "TEST_ACTION" };

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

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
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        object[] testActions = [
            new { Type = "ACTION_1" },
            new { Type = "ACTION_2" },
            new { Type = "ACTION_3" }
        ];

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

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
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        object triggerAction = new { Type = "TRIGGER" };

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // Act
        middleware.AfterReduce(triggerAction);

        // Wait for effect to process and dispatch
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        _dispatcherMock.Verify(d => d.Dispatch(It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task EffectThrowsException_ShouldPublishErrorEvent()
    {
        // Arrange
        ErrorThrowingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        object triggerAction = new { Type = "TRIGGER" };

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // Act
        middleware.AfterReduce(triggerAction);

        // Wait for error to propagate
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        _eventPublisherMock.Verify(e => e.Publish(It.IsAny<EffectErrorEventArgs>()), Times.Once);
    }

    [Fact]
    public async Task StateChanges_ShouldStreamToEffects()
    {
        // Arrange
        StateTrackingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);
        
        Mock<IRootState> newStateMock = new();
        _storeMock.Setup(s => s.CurrentState).Returns(newStateMock.Object);

        await middleware.InitializeAsync(_dispatcherMock.Object, _storeMock.Object);

        // Act
        middleware.AfterReduce(new { Type = "UPDATE_STATE" });

        // Wait for state to update
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        effect.ReceivedStates.ShouldContain(newStateMock.Object);
    }

    [Fact]
    public void BeginInternalMiddlewareChange_ShouldReturnDisposable()
    {
        // Arrange
        List<ReactiveEffect> effects = [];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock.Object);

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

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            WasHandleCalled = true;
            
            // Track all received actions
            actions.Subscribe(action => ReceivedActions.Add(action));
            
            return Observable.Empty<object>();
        }
    }

    private class DispatchingReactiveEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .Where(action => action.ToString()!.Contains("TRIGGER"))
                .Select(_ => new { Type = "DISPATCHED_FROM_EFFECT" });
        }
    }

    private class ErrorThrowingReactiveEffect : ReactiveEffect
    {
        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            return actions
                .Where(action => action.ToString()!.Contains("TRIGGER"))
                .Select<object, object>(_ => throw new InvalidOperationException("Test exception"));
        }
    }

    private class StateTrackingReactiveEffect : ReactiveEffect
    {
        public List<IRootState> ReceivedStates { get; } = [];

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IRootState> rootState)
        {
            rootState.Subscribe(state => ReceivedStates.Add(state));
            return Observable.Empty<object>();
        }
    }
}
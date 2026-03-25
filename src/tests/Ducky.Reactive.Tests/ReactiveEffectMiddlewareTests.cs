// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Tests;

public class ReactiveEffectMiddlewareTests
{
    private readonly IStoreEventPublisher _eventPublisherMock;
    private readonly IDispatcher _dispatcherMock;
    private readonly IStore _storeMock;
    private readonly IStateProvider _stateProviderMock;

    public ReactiveEffectMiddlewareTests()
    {
        _eventPublisherMock = A.Fake<IStoreEventPublisher>();
        _dispatcherMock = A.Fake<IDispatcher>();
        _storeMock = A.Fake<IStore>();
        _stateProviderMock = A.Fake<IStateProvider>();

        // Set up store to implement IStateProvider directly
        A.CallTo(() => _storeMock.GetSlice<object>()).Returns(_stateProviderMock.GetSlice<object>());
        A.CallTo(() => _storeMock.GetSliceByKey<object>(A<string>.Ignored))
            .ReturnsLazily(call => _stateProviderMock.GetSliceByKey<object>(call.Arguments[0] as string ?? string.Empty));

        // Default: return empty state dictionary for snapshot creation
        A.CallTo(() => _storeMock.GetStateDictionary())
            .Returns(ImmutableSortedDictionary<string, object>.Empty);
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

        IStateProvider newStateProviderMock = A.Fake<IStateProvider>();
        A.CallTo(() => _storeMock.GetSlice<object>()).Returns(newStateProviderMock.GetSlice<object>());
        A.CallTo(() => _storeMock.GetSliceByKey<object>(A<string>.Ignored))
            .ReturnsLazily(call => newStateProviderMock.GetSliceByKey<object>(call.Arguments[0] as string ?? string.Empty));

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

    private class StateActionPairingEffect : ReactiveEffect
    {
        public List<(object Action, IStateProvider State)> Pairs { get; } = [];

        public override IObservable<object> Handle(IObservable<object> actions, IObservable<IStateProvider> stateProvider)
        {
            actions.WithLatestFrom(stateProvider, (a, s) => (a, s))
                .Subscribe(pair => Pairs.Add(pair));
            return Observable.Empty<object>();
        }
    }

    [Fact]
    public async Task InitializeAsync_BehaviorSubjectInitialValue_ShouldNotBeNull()
    {
        // Arrange
        StateTrackingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        // Act
        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Wait for observable to process
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert - the first state received (from BehaviorSubject replay) should not be null
        effect.ReceivedStates.ShouldNotBeEmpty();
        effect.ReceivedStates.All(s => s is not null).ShouldBeTrue();
    }

    [Fact]
    public async Task AfterReduce_RapidDispatch_ShouldSnapshotStatePerAction()
    {
        // Arrange
        StateActionPairingEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        ImmutableSortedDictionary<string, object> state1 = ImmutableSortedDictionary<string, object>.Empty.Add("counter", 1);
        ImmutableSortedDictionary<string, object> state2 = ImmutableSortedDictionary<string, object>.Empty.Add("counter", 2);
        ImmutableSortedDictionary<string, object> state3 = ImmutableSortedDictionary<string, object>.Empty.Add("counter", 3);

        Queue<ImmutableSortedDictionary<string, object>> stateQueue = new();
        stateQueue.Enqueue(state1);
        stateQueue.Enqueue(state2);
        stateQueue.Enqueue(state3);

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Configure store to return different state dictionaries on each call
        A.CallTo(() => _storeMock.GetStateDictionary())
            .ReturnsLazily(() => stateQueue.Dequeue());

        object action1 = new { Type = "ACTION_1" };
        object action2 = new { Type = "ACTION_2" };
        object action3 = new { Type = "ACTION_3" };

        // Act - rapid dispatch of 3 actions
        middleware.AfterReduce(action1);
        middleware.AfterReduce(action2);
        middleware.AfterReduce(action3);

        // Wait for observables to process
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert - each action should be paired with its corresponding state snapshot
        effect.Pairs.Count.ShouldBe(3);

        effect.Pairs[0].Action.ShouldBe(action1);
        effect.Pairs[0].State.GetStateDictionary()["counter"].ShouldBe(1);

        effect.Pairs[1].Action.ShouldBe(action2);
        effect.Pairs[1].State.GetStateDictionary()["counter"].ShouldBe(2);

        effect.Pairs[2].Action.ShouldBe(action3);
        effect.Pairs[2].State.GetStateDictionary()["counter"].ShouldBe(3);
    }

    [Fact]
    public async Task AfterReduce_SnapshotImmutability_ShouldPreserveOldState()
    {
        // Arrange
        StateTrackingReactiveEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        ImmutableSortedDictionary<string, object> initialState =
            ImmutableSortedDictionary<string, object>.Empty.Add("value", "first");

        A.CallTo(() => _storeMock.GetStateDictionary()).Returns(initialState);
        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act - dispatch first action, capture the snapshot
        middleware.AfterReduce(new { Type = "ACTION_1" });
        await Task.Delay(10, TestContext.Current.CancellationToken);

        IStateProvider firstSnapshot = effect.ReceivedStates.Last();

        // Now change what the store returns and dispatch again
        ImmutableSortedDictionary<string, object> updatedState =
            ImmutableSortedDictionary<string, object>.Empty.Add("value", "second");
        A.CallTo(() => _storeMock.GetStateDictionary()).Returns(updatedState);

        middleware.AfterReduce(new { Type = "ACTION_2" });
        await Task.Delay(10, TestContext.Current.CancellationToken);

        // Assert - first snapshot should still have the old value
        firstSnapshot.GetStateDictionary()["value"].ShouldBe("first");

        // Latest snapshot should have the new value
        IStateProvider secondSnapshot = effect.ReceivedStates.Last();
        secondSnapshot.GetStateDictionary()["value"].ShouldBe("second");
    }

    [Fact]
    public async Task AfterReduce_EffectSeesCorrectStatePairedWithAction()
    {
        // Arrange
        StateActionPairingEffect effect = new();
        List<ReactiveEffect> effects = [effect];
        ReactiveEffectMiddleware middleware = new(effects, _eventPublisherMock);

        ImmutableSortedDictionary<string, object> stateA =
            ImmutableSortedDictionary<string, object>.Empty.Add("phase", "A");
        ImmutableSortedDictionary<string, object> stateB =
            ImmutableSortedDictionary<string, object>.Empty.Add("phase", "B");

        await middleware.InitializeAsync(_dispatcherMock, _storeMock);

        // Act
        A.CallTo(() => _storeMock.GetStateDictionary()).Returns(stateA);
        object actionA = new { Type = "DO_A" };
        middleware.AfterReduce(actionA);

        A.CallTo(() => _storeMock.GetStateDictionary()).Returns(stateB);
        object actionB = new { Type = "DO_B" };
        middleware.AfterReduce(actionB);

        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        effect.Pairs.Count.ShouldBe(2);
        effect.Pairs[0].Action.ShouldBe(actionA);
        effect.Pairs[0].State.GetStateDictionary()["phase"].ShouldBe("A");
        effect.Pairs[1].Action.ShouldBe(actionB);
        effect.Pairs[1].State.GetStateDictionary()["phase"].ShouldBe("B");
    }
}
using Ducky.Middlewares.ReactiveEffect;
using Moq;
using R3;

namespace Ducky.Tests.Middlewares;

/// <summary>
/// Tests for the ReactiveEffect base class.
/// </summary>
public class ReactiveEffectTests
{
    [Fact]
    public void GetKey_ReturnsTypeName()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        string key = effect.GetKey();

        // Assert
        Assert.Equal("TestEffect", key);
    }

    [Fact]
    public void GetAssemblyName_ReturnsAssemblyName()
    {
        // Arrange
        TestEffect effect = new();

        // Act
        string assemblyName = effect.GetAssemblyName();

        // Assert
        Assert.Contains("Ducky.Tests", assemblyName);
    }

    [Fact]
    public void Handle_DefaultImplementation_ReturnsEmptyObservable()
    {
        // Arrange
        TestDefaultEffect effect = new();
        Subject<object> actions = new();
        Subject<IRootState> states = new();
        List<object> results = [];

        // Act
        Observable<object> result = effect.Handle(actions, states);
        using IDisposable subscription = result.Subscribe(results.Add);

        actions.OnNext(new TestAction());
        states.OnNext(new Mock<IRootState>().Object);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Handle_CustomImplementation_ProcessesActions()
    {
        // Arrange
        TestCustomEffect effect = new();
        Subject<object> actions = new();
        Subject<IRootState> states = new();
        List<object> results = [];

        // Act
        Observable<object> result = effect.Handle(actions, states);
        using IDisposable subscription = result.Subscribe(results.Add);

        TestAction inputAction = new();
        actions.OnNext(inputAction);

        // Assert
        Assert.Single(results);
        Assert.IsType<TestResponseAction>(results[0]);
        var response = (TestResponseAction)results[0];
        Assert.Equal("Processed", response.Message);
    }

    [Fact]
    public void Handle_WithStateChanges_ReactsToState()
    {
        // Arrange
        TestStateReactiveEffect effect = new();
        Subject<object> actions = new();
        Subject<IRootState> states = new();
        List<object> results = [];

        // Act
        Observable<object> result = effect.Handle(actions, states);
        using IDisposable subscription = result.Subscribe(results.Add);

        // Emit state changes
        IRootState state1 = CreateMockState("State1");
        IRootState state2 = CreateMockState("State2");

        states.OnNext(state1);
        states.OnNext(state2);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.IsType<StateChangedAction>(r));
    }

    [Fact]
    public void Handle_CombinesActionsAndState_ProducesCorrectOutput()
    {
        // Arrange
        TestCombinedEffect effect = new();
        Subject<object> actions = new();
        Subject<IRootState> states = new();
        List<object> results = [];

        // Act
        Observable<object> result = effect.Handle(actions, states);
        using IDisposable subscription = result.Subscribe(results.Add);

        // Set initial state
        IRootState mockState = CreateMockState("TestState");
        states.OnNext(mockState);

        // Dispatch action
        TestAction action = new();
        actions.OnNext(action);

        // Allow some time for processing
        Thread.Sleep(100);

        // Assert
        Assert.Single(results);
        Assert.IsType<CombinedResultAction>(results[0]);
    }

    // Helper methods and test classes
    private static IRootState CreateMockState(string id)
    {
        Mock<IRootState> stateMock = new();
        stateMock.Setup(s => s.ToString()).Returns(id);
        return stateMock.Object;
    }

    private class TestEffect : ReactiveEffect;

    private class TestDefaultEffect : ReactiveEffect
    {
        // Uses default implementation
    }

    private class TestCustomEffect : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            return actions
                .Where(a => a is TestAction)
                .Select(_ => (object)new TestResponseAction("Processed"));
        }
    }

    private class TestStateReactiveEffect : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            return rootState
                .Select(state => (object)new StateChangedAction(state.ToString() ?? "Unknown"));
        }
    }

    private class TestCombinedEffect : ReactiveEffect
    {
        public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
        {
            return actions
                .Where(a => a is TestAction)
                .WithLatestFrom(
                    rootState,
                    (action, state) => (object)new CombinedResultAction(action.GetType().Name, state.ToString() ?? "Unknown"));
        }
    }

    private record TestAction;
    private record TestResponseAction(string Message);
    private record StateChangedAction(string StateId);
    private record CombinedResultAction(string ActionType, string StateId);
}

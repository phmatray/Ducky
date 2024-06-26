namespace BlazorStore.Tests;

public class ActionReducerTests
{
    private class TestState
    {
        public int Value { get; set; }
    }

    private class IncrementAction : IAction
    {
        public int Amount { get; set; }
    }

    private class DecrementAction : IAction
    {
        public int Amount { get; set; }
    }

    private class ResetAction : IAction;

    private class TestActionReducer : ActionReducer<TestState>
    {
        public TestActionReducer()
        {
            Register<IncrementAction>((state, action) => new TestState { Value = state.Value + action.Amount });
            Register<DecrementAction>((state, action) => new TestState { Value = state.Value - action.Amount });
            Register<ResetAction>(() => new TestState { Value = 0 });
        }
    }

    [Fact]
    public void Invoke_IncrementAction_ShouldIncrementStateValue()
    {
        var reducer = new TestActionReducer();
        var initialState = new TestState { Value = 10 };
        var action = new IncrementAction { Amount = 5 };

        var newState = reducer.Invoke(initialState, action);

        newState.Value.Should().Be(15);
    }

    [Fact]
    public void Invoke_DecrementAction_ShouldDecrementStateValue()
    {
        var reducer = new TestActionReducer();
        var initialState = new TestState { Value = 10 };
        var action = new DecrementAction { Amount = 3 };

        var newState = reducer.Invoke(initialState, action);

        newState.Value.Should().Be(7);
    }

    [Fact]
    public void Invoke_ResetAction_ShouldResetStateValue()
    {
        var reducer = new TestActionReducer();
        var initialState = new TestState { Value = 10 };
        var action = new ResetAction();

        var newState = reducer.Invoke(initialState, action);

        newState.Value.Should().Be(0);
    }

    [Fact]
    public void Invoke_UnknownAction_ShouldNotChangeState()
    {
        var reducer = new TestActionReducer();
        var initialState = new TestState { Value = 10 };
        var action = new UnknownAction();

        var newState = reducer.Invoke(initialState, action);

        newState.Should().BeSameAs(initialState);
    }

    private class UnknownAction : IAction;
}
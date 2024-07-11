namespace R3dux.Tests.TestModels;

// Actions
public record TestIncrementAction : IAction;
public record TestDecrementAction : IAction;
public record TestResetAction : IAction;
public record TestSetValueAction(int Value) : IAction;

// Reducers
public class TestCounterReducers : ReducerCollection<int>
{
    public TestCounterReducers()
    {
        Map<TestIncrementAction>((state, _) => state + 1);
        Map<TestDecrementAction>((state, _) => state - 1);
        Map<TestResetAction>((_, _) => GetInitialState());
        Map<TestSetValueAction>((_, action) => action.Value);
    }

    public override int GetInitialState()
    {
        return 10;
    }
}

// Effects
public class TestIncrementEffect : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<RootState> rootState)
    {
        // if the Value is greater than 15, then reset the counter
        return actions
            .FilterActions<TestIncrementAction>()    
            .WithSliceState<int, TestIncrementAction>(rootState)
            .Where(pair => pair.State > 15)
            .Delay(TimeSpan.FromSeconds(3))
            .SelectAction(_ => new TestResetAction());
    }
}

// Slice
public record TestCounterSlice : Slice<int>
{
    public override ReducerCollection<int> Reducers { get; } = new TestCounterReducers();

    public override string GetKey() => "test-counter";
}

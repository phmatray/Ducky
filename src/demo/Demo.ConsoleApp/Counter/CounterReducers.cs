namespace Demo.ConsoleApp.Counter;

public sealed record CounterReducers : SliceReducers<CounterState>
{
    public CounterReducers()
    {
        On<Increment>(Reduce);
        On<Decrement>(Reduce);
        On<Reset>(GetInitialState);
        On<SetValue>(Reduce);
        On<IncrementAsync>(Reduce);
    }

    public override CounterState GetInitialState()
        => new(0);

    private static CounterState Reduce(CounterState state, Increment action)
        => state with { Value = state.Value + action.Amount };

    private static CounterState Reduce(CounterState state, Decrement action)
        => state with { Value = Math.Max(0, state.Value - action.Amount) };

    private static CounterState Reduce(CounterState _, SetValue action)
        => new(action.Value);

    private static CounterState Reduce(CounterState state, IncrementAsync action)
        => state with { Value = state.Value + action.Amount };
}

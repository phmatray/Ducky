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
        => new();

    private static CounterState Reduce(CounterState state, Increment action)
        => new(state.Value + action.Amount);

    private static CounterState Reduce(CounterState state, Decrement action)
        => new(Math.Max(0, state.Value - action.Amount));

    private static CounterState Reduce(CounterState _, SetValue action)
        => new(action.Value);

    private static CounterState Reduce(CounterState state, IncrementAsync action)
        => new(state.Value + action.Amount);
}

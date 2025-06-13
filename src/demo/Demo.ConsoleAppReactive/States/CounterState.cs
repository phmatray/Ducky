namespace Demo.ConsoleAppReactive.States;

// Counter state and actions
public record CounterState : IState
{
    public int Value { get; init; }
    public int IncrementCount { get; init; }
    public int DecrementCount { get; init; }
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;
}

[DuckyAction]
public record IncrementCounter(int Amount = 1);

[DuckyAction]
public record DecrementCounter(int Amount = 1);

[DuckyAction]
public record ResetCounter;

[DuckyAction]
public record CounterThresholdReached(int Value);

// Counter slice reducers
public record CounterSliceReducers : SliceReducers<CounterState>
{
    public override CounterState GetInitialState() => new()
    {
        Value = 0,
        IncrementCount = 0,
        DecrementCount = 0,
        LastUpdated = DateTime.UtcNow
    };

    public CounterSliceReducers()
    {
        On<IncrementCounter>((state, action) => state with
            {
                Value = state.Value + action.Amount,
                IncrementCount = state.IncrementCount + 1,
                LastUpdated = DateTime.UtcNow
            });

        On<DecrementCounter>((state, action) => state with
            {
                Value = state.Value - action.Amount,
                DecrementCount = state.DecrementCount + 1,
                LastUpdated = DateTime.UtcNow
            });

        On<ResetCounter>(state => GetInitialState() with
            {
                IncrementCount = state.IncrementCount,
                DecrementCount = state.DecrementCount
            });
    }
}

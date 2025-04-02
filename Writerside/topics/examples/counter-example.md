# Counter Example

```c#
namespace MyDuckyApp;

#region State

public record CounterState(int Value);

#endregion

#region Actions

[DuckyAction]
public record Increment(int Amount = 1);

[DuckyAction]
public record Decrement(int Amount = 1);

[DuckyAction]
public record Reset;

#endregion

#region Reducers

public record CounterReducers : SliceReducers<CounterState>
{
    public CounterReducers()
    {
        On<Increment>(Reduce);
        On<Decrement>(Reduce);
        On<Reset>(GetInitialState);
    }

    public override CounterState GetInitialState()
        => new(10);

    private static CounterState Reduce(CounterState state, Increment action)
        => new(state.Value + action.Amount);

    private static CounterState Reduce(CounterState state, Decrement action)
        => new(state.Value - action.Amount);
}

#endregion

#region Effects

public class ResetCounterAfter3Sec : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        CounterState counterState = rootState.GetSliceState<CounterState>();

        // if the Value is greater than 15, then reset the counter
        if (counterState.Value > 15)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ObservableSystem.DefaultTimeProvider);
            Dispatcher.Reset();
        }

        await Task.CompletedTask;
    }
}

#endregion

```
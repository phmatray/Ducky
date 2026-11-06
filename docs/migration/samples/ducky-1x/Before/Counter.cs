using Ducky;
using Ducky.Generators;

namespace Ducky1x.Before;

#region before-counter
// Ducky 1.x: every state implements the IState marker; a record deriving from SliceReducers<TState> reduces it.
public sealed record CounterState(int Value) : IState;

[DuckyAction]
public sealed record Increment(int Amount = 1);

[DuckyAction]
public sealed record Reset;

public sealed record CounterReducers : SliceReducers<CounterState>
{
    public CounterReducers()
    {
        On<Increment>((state, action) => new CounterState(state.Value + action.Amount));
        On<Reset>(GetInitialState);
    }

    public override CounterState GetInitialState() => new(0);
}
#endregion

#region before-effect
// Ducky 1.x: an AsyncEffect<TAction> reads the root state and dispatches through its own Dispatch.
public sealed class ResetAboveTen : AsyncEffect<Increment>
{
    public override Task HandleAsync(Increment action, IRootState rootState)
    {
        if (rootState.GetSliceState<CounterState>().Value > 10)
        {
            Dispatch(new Reset());
        }

        return Task.CompletedTask;
    }
}
#endregion

#region before-selector
// Ducky 1.x: a memoized selector over the slice state.
public static class CounterSelectors
{
    public static readonly Func<CounterState, bool> IsPositive = MemoizedSelector.Create<CounterState, bool>(state => state.Value > 0);
}
#endregion

using Ducky;

namespace Ducky1x.After;

#region after-counter
// Ducky 2.0: any record is a state (no IState); a sealed Slice<TState> declares the initial state and its reducers.
public sealed record CounterState(int Value);

[DuckyAction]
public sealed record Increment(int Amount = 1);

[DuckyAction]
public sealed record Reset;

public sealed class CounterSlice : Slice<CounterState>
{
    public CounterSlice()
    {
        On<Increment>((state, action) => new CounterState(state.Value + action.Amount));
        On<Reset>(_ => Initial);
    }

    protected override CounterState Initial => new(0);
}
#endregion

#region after-effect
// Ducky 2.0: Effect<TAction> reads the snapshot and dispatches through its context.
public sealed class ResetAboveTen : Effect<Increment>
{
    public override Task Handle(Increment action, EffectContext context, CancellationToken cancellationToken)
    {
        if (context.State.Get<CounterState>().Value > 10)
        {
            context.Reset(); // generated from [DuckyAction]: context.Dispatch(new Reset())
        }

        return Task.CompletedTask;
    }
}
#endregion

#region after-selector
// Ducky 2.0: Selector.Define over the snapshot; Create() gives each consumer its own memoized function.
public static class CounterSelectors
{
    public static readonly SelectorDef<bool> IsPositive = Selector.Define((StateSnapshot s) => s.Get<CounterState>().Value, value => value > 0);
}
#endregion

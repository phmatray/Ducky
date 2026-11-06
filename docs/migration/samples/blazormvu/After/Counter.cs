using Ducky;

namespace BlazorMvu.After;

public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

#region after-model
// Ducky 2.0: the model is a slice state and each message a plain action record, shared by the whole app.
public sealed record CounterState(int Count, bool IsLoading, string? Error, int Seconds);

public sealed record Increment;

public sealed record Decrement;

public sealed record LoadCount;

public sealed record CountLoading;

public sealed record CountLoaded(int Count);

public sealed record CountFailed(string Error);

public sealed record Tick;
#endregion

#region after-update
// Ducky 2.0: Init becomes Initial, and each case of the Update switch one On<Msg>.
public sealed class CounterSlice : Slice<CounterState>
{
    public CounterSlice()
    {
        On<Increment>(state => state with { Count = state.Count + 1 });
        On<Decrement>(state => state with { Count = state.Count - 1 });
        On<CountLoading>(state => state with { IsLoading = true, Error = null });
        On<CountLoaded>((state, loaded) => state with { Count = loaded.Count, IsLoading = false });
        On<CountFailed>((state, failed) => state with { IsLoading = false, Error = failed.Error });
        On<Tick>(state => state with { Seconds = state.Seconds + 1 });
    }

    protected override CounterState Initial => new(Count: 0, IsLoading: false, Error: null, Seconds: 0);
}
#endregion

#region after-cmd
// Ducky 2.0: Cmd.OfTask becomes an effect; ctx.Run dispatches started, then succeeded or failed.
public sealed class LoadCountEffect(ICounterApi api) : Effect<LoadCount>
{
    public override Concurrency Policy => Concurrency.Switch; // a new LoadCount cancels the running one

    public override Task Handle(LoadCount action, EffectContext context, CancellationToken cancellationToken) =>
        context.Run(
            started: new CountLoading(),
            work: api.GetCountAsync,
            succeeded: count => new CountLoaded(count),
            failed: exception => new CountFailed(exception.Message),
            cancellationToken);
}
#endregion

#region after-sub
// Ducky 2.0: Sub.Every becomes a LongRunning effect started at init, on the store's TimeProvider (fake in tests).
public sealed class ClockEffect : Effect<StoreInitialized>
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    public override bool LongRunning => true; // it never finishes: WhenIdleAsync doesn't wait for it

    public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval, context.Time);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            context.Dispatch(new Tick());
        }
    }
}
#endregion

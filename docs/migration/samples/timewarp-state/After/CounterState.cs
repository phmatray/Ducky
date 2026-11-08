using Ducky;

namespace TimeWarpSample.After;

#region after-state
// Ducky 2.0: an immutable record replaces the mutable State<T>; the slice's Initial replaces Initialize().
public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

public sealed record CounterState(int Count, bool IsLoading, string? Error);
#endregion

#region after-actions
// Ducky 2.0: the actions are top-level records. [DuckyAction] generates Store.IncrementCount(1) and
// await Store.IncrementCountAsync(1), the counterparts of TimeWarp.State's generated CounterState.IncrementCount(...).
[DuckyAction]
public sealed record IncrementCount(int Amount);

[DuckyAction]
public sealed record LoadCount;

public sealed record CountLoaded(int Count);

public sealed record CountLoadFailed(string Error);
#endregion

#region after-slice
// Ducky 2.0: each mutation of a handler becomes a pure reducer that returns the next state. The async handler splits in
// two: LoadCount's reducer sets IsLoading at once, and the effect below reports the result as CountLoaded or CountLoadFailed.
public sealed class CounterSlice : Slice<CounterState>
{
    public CounterSlice()
    {
        On<IncrementCount>((state, action) => state with { Count = state.Count + action.Amount });
        On<LoadCount>(state => state with { IsLoading = true, Error = null });
        On<CountLoaded>((state, action) => state with { Count = action.Count, IsLoading = false });
        On<CountLoadFailed>((state, action) => state with { IsLoading = false, Error = action.Error });
    }

    public override string Key => "counter";

    protected override CounterState Initial => new(Count: 0, IsLoading: false, Error: null);
}
#endregion

#region after-effect
// Ducky 2.0: the awaiting half of the handler, with the run's token. Switch: a newer LoadCount cancels the running one.
public sealed class LoadCountEffect(ICounterApi api) : Effect<LoadCount>
{
    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(LoadCount action, EffectContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Dispatch(new CountLoaded(await api.GetCountAsync(cancellationToken)));
        }
        catch (HttpRequestException exception)
        {
            context.Dispatch(new CountLoadFailed(exception.Message));
        }
    }
}
#endregion

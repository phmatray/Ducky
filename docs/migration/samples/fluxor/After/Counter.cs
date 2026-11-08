using Ducky;

namespace FluxorSample.After;

#region after-state
// Ducky 2.0: the state and the actions stay plain records; no attribute, no parameterless constructor.
public sealed record CounterState(int Count, bool IsLoading, string? Error);

public sealed record IncrementCounterAction(int Amount);

public sealed record LoadCounterAction;

public sealed record LoadCounterResultAction(int Count);

public sealed record LoadCounterFailedAction(string Error);

public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}
#endregion

#region after-reducers
// Ducky 2.0: one slice holds the initial state and one On<TAction> per action; On<T>(state => ...) when the payload is unused.
public sealed class CounterSlice : Slice<CounterState>
{
    public CounterSlice()
    {
        On<IncrementCounterAction>((state, action) => state with { Count = state.Count + action.Amount });
        On<LoadCounterAction>(state => state with { IsLoading = true, Error = null });
        On<LoadCounterResultAction>((state, action) => state with { Count = action.Count, IsLoading = false });
        On<LoadCounterFailedAction>((state, action) => state with { IsLoading = false, Error = action.Error });
    }

    // The slice key, pinned because the prerender seed and DevTools name the slice by it.
    public override string Key => "counter";

    protected override CounterState Initial => new(Count: 0, IsLoading: false, Error: null);
}
#endregion

#region after-effect
// Ducky 2.0: an Effect<TAction> gets a cancellation token and a concurrency policy. Switch: a newer LoadCounterAction
// cancels the running load, and the cancelled run can no longer dispatch.
public sealed class LoadCounterEffect(ICounterApi api) : Effect<LoadCounterAction>
{
    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(LoadCounterAction action, EffectContext context, CancellationToken cancellationToken)
    {
        try
        {
            context.Dispatch(new LoadCounterResultAction(await api.GetCountAsync(cancellationToken)));
        }
        catch (HttpRequestException exception)
        {
            context.Dispatch(new LoadCounterFailedAction(exception.Message));
        }
    }
}
#endregion

#region after-load-rule
// Ducky 2.0: the first load runs once the store is initialized, unless the prerender seed already restored the slice.
public sealed class LoadCounterOnStart : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<CounterState>())
        {
            context.Dispatch(new LoadCounterAction());
        }

        return Task.CompletedTask;
    }
}
#endregion

#region after-middleware
// Ducky 2.0: a Middleware sees every reduced action in AfterReduce, with its context (the action, origin, states).
public sealed class ActionLog
{
    public List<string> Entries { get; } = [];
}

public sealed class ActionLogMiddleware(ActionLog log) : Middleware
{
    public override void AfterReduce(ActionContext context) => log.Entries.Add(context.Action.GetType().Name);
}
#endregion

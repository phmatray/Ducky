using Fluxor;

namespace FluxorSample.Before;

#region before-state
// Fluxor: [FeatureState] marks the state; its parameterless constructor gives the initial state.
[FeatureState]
public sealed record CounterState(int Count, bool IsLoading, string? Error)
{
    public CounterState() : this(Count: 0, IsLoading: false, Error: null)
    {
    }
}

public sealed record IncrementCounterAction(int Amount);

public sealed record LoadCounterAction;

public sealed record LoadCounterResultAction(int Count);

public sealed record LoadCounterFailedAction(string Error);

public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}
#endregion

#region before-reducers
// Fluxor: static [ReducerMethod]s, found by the assembly scan; [ReducerMethod(typeof(T))] when the payload is unused.
public static class CounterReducers
{
    [ReducerMethod]
    public static CounterState OnIncrement(CounterState state, IncrementCounterAction action) =>
        state with { Count = state.Count + action.Amount };

    [ReducerMethod(typeof(LoadCounterAction))]
    public static CounterState OnLoad(CounterState state) => state with { IsLoading = true, Error = null };

    [ReducerMethod]
    public static CounterState OnLoaded(CounterState state, LoadCounterResultAction action) =>
        state with { Count = action.Count, IsLoading = false };

    [ReducerMethod]
    public static CounterState OnLoadFailed(CounterState state, LoadCounterFailedAction action) =>
        state with { IsLoading = false, Error = action.Error };
}
#endregion

#region before-effect
// Fluxor: an [EffectMethod] starts for every LoadCounterAction, alongside any earlier run, and gets no cancellation token.
public sealed class CounterEffects(ICounterApi api)
{
    [EffectMethod(typeof(LoadCounterAction))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        try
        {
            dispatcher.Dispatch(new LoadCounterResultAction(await api.GetCountAsync(CancellationToken.None)));
        }
        catch (HttpRequestException exception)
        {
            dispatcher.Dispatch(new LoadCounterFailedAction(exception.Message));
        }
    }
}
#endregion

#region before-middleware
// Fluxor: a Middleware sees every action through BeforeDispatch and AfterDispatch.
public sealed class ActionLog
{
    public List<string> Entries { get; } = [];
}

public sealed class ActionLogMiddleware(ActionLog log) : Middleware
{
    public override void AfterDispatch(object action) => log.Entries.Add(action.GetType().Name);
}
#endregion

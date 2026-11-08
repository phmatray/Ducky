using Ducky;

namespace StateContainerSample.After;

#region after-store
// Ducky 2.0: the container becomes a SliceStore. Its state is one immutable record, its methods keep their names, and
// each change is a Set whose mutator runs when the store reduces it: no event to raise, no lost update.
public interface ICounterApi
{
    Task<int> GetCountAsync(CancellationToken cancellationToken);
}

public sealed record CounterState(int Count, bool IsLoading, string? Error);

public sealed record LoadCount; // what a component dispatches to (re)load

public sealed class CounterStore : SliceStore<CounterState>
{
    public CounterStore() => On<LoadCount>(state => state with { IsLoading = true, Error = null });

    public override string Key => "counter";

    protected override CounterState Initial => new(Count: 0, IsLoading: false, Error: null);

    public void Increment(int amount) => Set(state => state with { Count = state.Count + amount });

    public void Loaded(int count) => Set(state => state with { Count = count, IsLoading = false });

    public void LoadFailed(string error) => Set(state => state with { IsLoading = false, Error = error });
}
#endregion

#region after-effect
// Ducky 2.0: a SliceStore never awaits. The async method becomes an effect that injects the service and the SliceStore;
// Switch cancels a running load when a newer LoadCount arrives, and the superseded run's Set calls are dropped.
public sealed class LoadCountEffect(ICounterApi api, CounterStore counter) : Effect<LoadCount>
{
    public override Concurrency Policy => Concurrency.Switch;

    public override async Task Handle(LoadCount action, EffectContext context, CancellationToken cancellationToken)
    {
        try
        {
            counter.Loaded(await api.GetCountAsync(cancellationToken));
        }
        catch (HttpRequestException exception)
        {
            counter.LoadFailed(exception.Message);
        }
    }
}

// The first load runs once, when the store is initialized, unless the prerender seed already restored the slice.
public sealed class LoadCountOnStart : Effect<StoreInitialized>
{
    public override Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<CounterState>())
        {
            context.Dispatch(new LoadCount());
        }

        return Task.CompletedTask;
    }
}
#endregion

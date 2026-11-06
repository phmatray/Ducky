using Ducky;
using Ducky.Blazor;
using Microsoft.AspNetCore.Components.Rendering;

namespace TheBlazorState.After;

public sealed record DashboardData(int OpenTasks);

public interface IStatsApi
{
    Task<DashboardData> GetDashboardAsync(string userId, CancellationToken cancellationToken);
}

public interface ICurrentUser
{
    string Id { get; }
}

#region after-persist
// Ducky 2.0: the persisted property is a field of a slice state; Persist<DashboardSlice> in the registration sets the TTL.
public sealed record DashboardState(DashboardData? Stats);

public sealed record StatsLoaded(DashboardData Stats);

public sealed class DashboardSlice : Slice<DashboardState>
{
    public DashboardSlice() => On<StatsLoaded>((state, action) => state with { Stats = action.Stats });

    protected override DashboardState Initial => new(Stats: null);
}
#endregion

#region after-load
// Ducky 2.0: LoadFrom becomes an Effect<StoreInitialized> that loads only when storage restored nothing.
public sealed class LoadStats(IStatsApi api, ICurrentUser user) : Effect<StoreInitialized>
{
    public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
    {
        if (!context.State.WasRestored<DashboardState>())
        {
            context.Dispatch(new StatsLoaded(await api.GetDashboardAsync(user.Id, cancellationToken)));
        }
    }
}
#endregion

#region after-component
// Ducky 2.0: StatsMeta.WasRestored becomes State.WasRestored<T>(), on the store's snapshot.
public sealed class Dashboard : DuckyComponent<DashboardState>
{
    public bool FromCache => Store.State.WasRestored<DashboardState>();

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, State.Stats?.OpenTasks);
}
#endregion

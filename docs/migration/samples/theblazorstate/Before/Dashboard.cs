using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using TheBlazorState.Attributes;

namespace TheBlazorState.Before;

public sealed record DashboardData(int OpenTasks);

public interface IStatsApi
{
    Task<DashboardData> GetDashboardAsync(string userId);
}

public interface ICurrentUser
{
    string Id { get; }
}

#region before-persist
// TheBlazorState: a [Persist] partial property of the component, configured in ConfigureState. LoadFrom runs when
// nothing was restored; KeySuffix scopes the key to the user, read once when the component initializes.
public sealed partial class Dashboard : ComponentBase
{
    [Inject]
    public required IStatsApi Api { get; set; }

    [Inject]
    public required ICurrentUser User { get; set; }

    [Persist(TimeToLive = "00:05:00")]
    public partial DashboardData? Stats { get; set; }

    partial void ConfigureState(__StateContext ctx) =>
        ctx.Stats
            .KeySuffix(User.Id)
            .LoadFrom(async () => (DashboardData?)await Api.GetDashboardAsync(User.Id));

    public bool FromCache => StatsMeta.WasRestored;

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Stats?.OpenTasks);
}
#endregion

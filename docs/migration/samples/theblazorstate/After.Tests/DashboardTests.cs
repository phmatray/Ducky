using System.Globalization;
using System.Text.Json.Nodes;
using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace TheBlazorState.After.Tests;

internal sealed class FakeStatsApi : IStatsApi
{
    public List<string> Requested { get; } = [];

    public Task<DashboardData> GetDashboardAsync(string userId, CancellationToken cancellationToken)
    {
        Requested.Add(userId);
        return Task.FromResult(new DashboardData(OpenTasks: 3));
    }
}

internal sealed class FakeUser(string id) : ICurrentUser
{
    public string Id => id;
}

// The migrated app keeps the TheBlazorState app's behaviour (SPEC §22.3).
public sealed class DashboardTests
{
    [Fact]
    public async Task LoadStats_NothingRestored_LoadsForUser()
    {
        var api = new FakeStatsApi();

        var run = await EffectTest.Run<LoadStats, StoreInitialized>(
            new StoreInitialized(),
            ducky => ducky.AddSlice<DashboardSlice>(),
            services: s => s.AddSingleton<IStatsApi>(api).AddSingleton<ICurrentUser>(new FakeUser("ada")));

        run.Dispatched.ShouldBe([new StatsLoaded(new DashboardData(3))]);
        api.Requested.ShouldBe(["ada"]);
    }

    [Fact]
    public async Task LoadStats_Restored_DoesNotLoad()
    {
        var api = new FakeStatsApi();

        var run = await EffectTest.Run<LoadStats, StoreInitialized>(
            new StoreInitialized(),
            ducky => ducky.AddSlice<DashboardSlice>(),
            store => store.Restore(new Dictionary<string, object> { ["dashboard"] = new DashboardState(new DashboardData(7)) }, Origin.Hydration),
            services: s => s.AddSingleton<IStatsApi>(api).AddSingleton<ICurrentUser>(new FakeUser("ada")));

        run.Dispatched.ShouldBeEmpty();
        api.Requested.ShouldBeEmpty();
    }

    [Fact]
    public async Task AppStore_SetUserName_SeenByEverySelection()
    {
        await using var services = new ServiceCollection().AddDucky(ducky => ducky.AddSlice<AppStore>()).BuildServiceProvider();
        var store = services.GetRequiredService<IStore>();
        using var shown = store.Select((AppState state) => state.UserName);

        services.GetRequiredService<AppStore>().SetUserName("Ada");

        services.GetRequiredService<AppStore>().ShouldBeSameAs(store.Slices.OfType<AppStore>().Single()); // one shared instance
        shown.Value.ShouldBe("Ada");
    }
}

public sealed class DashboardViewTests : BunitContext
{
    private static readonly DateTimeOffset _now = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private readonly FakeStatsApi _api = new();
    private readonly BunitJSModuleInterop _ducky;

    public DashboardViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ducky = JSInterop.SetupModule("./_content/Ducky.Blazor/ducky.js");
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(_now));
        Services.AddSingleton<IStatsApi>(_api).AddSingleton<ICurrentUser>(new FakeUser("ada"));
        Registration.AddApp(Services);
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    // Scope puts the user in every persisted key, and MaxAge (the TimeToLive) is inclusive: 5 minutes old still restores.
    // The stored value is Ducky's envelope (version, write time, state).
    [Theory]
    [InlineData("ducky:ada:dashboard", 0, true)]
    [InlineData("ducky:ada:dashboard", 5 * TimeSpan.TicksPerMinute, true)]
    [InlineData("ducky:ada:dashboard", (5 * TimeSpan.TicksPerMinute) + 1, false)]
    [InlineData("ducky:bob:dashboard", 0, false)]
    public void StoredDashboard_RestoresOnlyForTheUserWithinMaxAge(string key, long ageTicks, bool restored)
    {
        var writtenAt = (_now - TimeSpan.FromTicks(ageTicks)).UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
        _ducky.Setup<string?>("storageGet", invocation => (string?)invocation.Arguments[1] == key)
            .SetResult(new JsonObject { ["v"] = 1, ["at"] = writtenAt, ["s"] = JsonNode.Parse("""{"Stats":{"OpenTasks":7}}""") }.ToJsonString());

        var view = Render<Dashboard>();

        view.WaitForAssertion(() => view.Markup.ShouldBe(restored ? "7" : "3"));
        view.Instance.FromCache.ShouldBe(restored);
        _api.Requested.ShouldBe(restored ? [] : ["ada"]);
    }

    [Fact]
    public void EmptyStorage_LoadsAndRenders()
    {
        var view = Render<Dashboard>();

        view.WaitForAssertion(() => view.Markup.ShouldBe("3"));
        view.Instance.FromCache.ShouldBeFalse();
        _api.Requested.ShouldBe(["ada"]);
    }
}

using Ducky.Blazor;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Ducky.Docs.Tests.Blazor;

// The snippets of docs/guides/blazor/cross-tab.md.
public sealed class CrossTabGuideTests
{
    #region cross-tab-register
    public static DuckyBuilder AddSyncedPreferences(DuckyBuilder ducky) => ducky
        .UseJson(DocsJson.Default)
        .AddSlice<PreferencesSlice>()
        .Persist<PreferencesSlice>(options => options.SyncAcrossTabs = true); // localStorage only
    #endregion

    // Non-normative (M15-03): a value another tab wrote is read again after 50 ms and restored into a synced slice; an
    // unsynced slice ignores the other tabs' storage events, and ducky.js attaches no window storage listener for it
    // (crossTab false, §11.7 Registration).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SyncAcrossTabs_OtherTabsChangeRestored(bool synced)
    {
        var browser = new FakeBrowser();
        await using var store = TestStore.Create(
            d => _ = synced ? AddSyncedPreferences(d) : d.UseJson(DocsJson.Default).AddSlice<PreferencesSlice>().Persist<PreferencesSlice>(),
            services => services.AddSingleton<IJSRuntime>(browser));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // [0] is the store id and [1] the .NET reference; then prefix, crossTab, pruneAfterMs (CrossTabSync.DeliveryPruneAfter,
        // 10 min, passed as a long) and maxInlineBytes (the BlazorOptions.InlinePayloadBytes default).
        browser.CallsTo("watchStorage").Single()[2..].ShouldBe(["ducky", synced, 600000L, 16 * 1024]);

        const string written = """{"v":1,"at":"2026-11-07T12:00:00Z","s":{"Theme":"dark"}}""";
        browser.Storage[("local", "ducky:preferences")] = written; // the other tab's write
        var receipt = browser.NotifyStorageChangedAsync("tab", "local", "ducky:preferences", written);
        store.Time.Advance(TimeSpan.FromMilliseconds(49));
        await store.Settled();
        store.State.Get<Preferences>().Theme.ShouldBe("light");

        store.Time.Advance(TimeSpan.FromMilliseconds(1));
        await receipt;
        await store.Settled();
        store.State.Get<Preferences>().Theme.ShouldBe(synced ? "dark" : "light");
        browser.CallsTo("storageSet").ShouldBeEmpty(); // a restore from another tab is never written back
    }

    // Non-normative (M15-03): sessionStorage is per tab, so syncing a Session slice is a configuration error.
    [Fact]
    public void SyncAcrossTabs_SessionStorage_IsConfigurationError()
    {
        var services = new ServiceCollection().AddDucky(d => d
            .UseJson(DocsJson.Default)
            .AddSlice<PreferencesSlice>()
            .Persist<PreferencesSlice>(options => (options.Storage, options.SyncAcrossTabs) = (PersistStorage.Session, true)));
        using var provider = services.BuildServiceProvider();

        Should.Throw<DuckyConfigurationException>(() => provider.CreateScope().ServiceProvider.GetRequiredService<IStore>())
            .Message.ShouldContain("DUCKY312");
    }
}

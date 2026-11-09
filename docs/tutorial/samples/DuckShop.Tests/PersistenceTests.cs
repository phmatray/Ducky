using Ducky;
using Ducky.Blazor;
using Ducky.Generated;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace DuckShop.Tests;

// The persisted basket (tutorial step 8), with the app's own registration and a fake browser.
public sealed class PersistenceTests
{
    [Fact]
    public async Task Basket_PageReloaded_Restored()
    {
        var browser = new FakeBrowserStorage();
        await using (var page = Shop(browser))
        {
            await page.InitializeAsync(TestContext.Current.CancellationToken);
            page.AddToBasket("pirate");
            page.AddToBasket("pirate");
            await page.Settled();
        } // closing the page writes what is still pending

        await using var reloaded = Shop(browser);
        await reloaded.InitializeAsync(TestContext.Current.CancellationToken);

        browser.Entries.Keys.ShouldContain(("local", "ducky:basket"));
        reloaded.State.Get<BasketState>().Lines.ShouldBe([new BasketLine("pirate", 2)]);
        reloaded.State.WasRestored<BasketState>().ShouldBeTrue();
        reloaded.State.WasRestored<CatalogueState>().ShouldBeFalse(); // not persisted: loaded again
    }

    // A basket saved by version 1 of the shop is migrated when version 2 reads it.
    [Fact]
    public async Task Basket_SavedByVersion1_Migrated()
    {
        var browser = new FakeBrowserStorage();
        browser.Entries[("local", "ducky:basket")] =
            """{"v":1,"at":"2026-11-01T09:00:00Z","s":{"Items":{"classic":2,"disco":1}}}""";

        await using var store = Shop(browser);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.State.Get<BasketState>().Lines.ShouldBe([new BasketLine("classic", 2), new BasketLine("disco", 1)]);
        store.State.WasRestored<BasketState>().ShouldBeTrue();
    }

    // The load rule: an effect that loads a slice storage can restore checks WasRestored.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task LoadRule_RestoredCatalogue_NotLoadedAgain(bool restored, int loads)
    {
        var api = new CountingCatalogueApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CatalogueSlice>().AddEffect<LoadCatalogueOnceEffect>(),
            services => services.AddSingleton<ICatalogueApi>(api));
        if (restored)
        {
            store.Seed(new CatalogueState(FakeCatalogueApi.Ducks, Loading: false, Error: null)); // as storage would
        }

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.Settled();

        api.Calls.ShouldBe(loads);
        store.State.Get<CatalogueState>().Ducks.ShouldBe(FakeCatalogueApi.Ducks);
    }

    #region load-rule
    // If the catalogue were persisted too, its effect would skip the load after a reload.
    public sealed class LoadCatalogueOnceEffect(ICatalogueApi api) : Effect<StoreInitialized>
    {
        public override async Task Handle(
            StoreInitialized action,
            EffectContext context,
            CancellationToken cancellationToken)
        {
            // Storage already restored the catalogue: don't load it a second time.
            if (context.State.WasRestored<CatalogueState>())
            {
                return;
            }

            // Only a finished load changes the slice, so storage never holds a half-done one.
            context.Dispatch(new CatalogueLoaded(await api.GetDucksAsync(cancellationToken)));
        }
    }
    #endregion

    private static TestStore Shop(FakeBrowserStorage browser) => TestStore.Create(
        ducky => ducky
            .AddDuckyGenerated_DuckShop()
            .AddBlazor()
            .UseJson(ShopJson.Default)
            .PersistBasket(),
        services => services
            .AddSingleton<IJSRuntime>(browser)
            .AddSingleton<ICatalogueApi, InstantCatalogueApi>()
            .AddSingleton<ISearchApi, FakeSearchApi>());

    private sealed class CountingCatalogueApi : ICatalogueApi
    {
        public int Calls { get; private set; }

        public async Task<System.Collections.Immutable.ImmutableList<Duck>> GetDucksAsync(CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Yield();
            return FakeCatalogueApi.Ducks;
        }
    }
}

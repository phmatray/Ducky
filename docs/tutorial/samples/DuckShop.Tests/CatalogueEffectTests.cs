using Ducky;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckShop.Tests;

// The catalogue effect on its own (tutorial steps 5 and 9).
public sealed class CatalogueEffectTests
{
    #region test-effect
    [Fact]
    public async Task LoadCatalogue_ServerAnswers_LoadingThenLoaded()
    {
        var run = await EffectTest.Run<LoadCatalogueEffect, StoreInitialized>(
            new StoreInitialized(),
            services: services => services.AddSingleton<ICatalogueApi, InstantCatalogueApi>());

        run.Dispatched.ShouldBe([
            new CatalogueLoading(),
            new CatalogueLoaded(FakeCatalogueApi.Ducks),
        ]);
    }

    [Fact]
    public async Task LoadCatalogue_ServerFails_LoadingThenFailed()
    {
        var run = await EffectTest.Run<LoadCatalogueEffect, StoreInitialized>(
            new StoreInitialized(),
            services: services => services.AddSingleton<ICatalogueApi, OfflineCatalogueApi>());

        run.Dispatched.ShouldBe([
            new CatalogueLoading(),
            new CatalogueFailed("The warehouse is offline."),
        ]);
    }
    #endregion

    // Step 5: the fake server answers after its latency, on the store's clock.
    [Fact]
    public async Task LoadCatalogue_FakeServer_AnswersAfterItsLatency()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CatalogueSlice>().AddEffect<LoadCatalogueEffect>(),
            services => services.AddSingleton<ICatalogueApi, FakeCatalogueApi>());

        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.State.Get<CatalogueState>().Loading.ShouldBeTrue();
        store.Time.Advance(FakeCatalogueApi.Latency - TimeSpan.FromMilliseconds(1));
        store.State.Get<CatalogueState>().Loading.ShouldBeTrue();
        store.Time.Advance(TimeSpan.FromMilliseconds(1));
        await store.Settled();

        store.State.Get<CatalogueState>().ShouldBe(new CatalogueState(FakeCatalogueApi.Ducks, Loading: false, Error: null));
    }
}

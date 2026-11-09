using Ducky.Generated;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckShop.Tests;

// The whole shop on a test store (tutorial step 9): the app's slices and effects, its fake
// servers, and the test store's fake clock.
public sealed class ShopTests
{
    #region test-store
    [Fact]
    public async Task Shop_SearchThenAdd_MatchesShownAndBasketFilled()
    {
        await using var store = TestStore.Create(
            ducky => ducky.AddDuckyGenerated_DuckShop(),
            services => services
                .AddSingleton<ICatalogueApi, FakeCatalogueApi>()
                .AddSingleton<ISearchApi, FakeSearchApi>());

        // Starting the store starts the catalogue load; the fake server answers 600 ms later.
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Time.Advance(FakeCatalogueApi.Latency);
        await store.Settled();

        store.TypeSearch("yel");
        store.Time.Advance(FakeSearchApi.Latency("yel"));
        await store.Settled();
        store.AddToBasket("mellow");

        ShelfSelectors.VisibleDucks.Evaluate(store.State).Select(duck => duck.Name)
            .ShouldBe(["Classic Yellow", "Mellow Yellow"]);
        store.State.Get<BasketState>().Count.ShouldBe(1);
        store.Failures.ShouldBeEmpty();
    }
    #endregion
}

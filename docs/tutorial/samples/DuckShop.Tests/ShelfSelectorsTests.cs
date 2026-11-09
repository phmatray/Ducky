using Ducky.Testing;

namespace DuckShop.Tests;

// The shelf's memoized selector (tutorial step 7).
public sealed class ShelfSelectorsTests
{
    [Fact]
    public async Task VisibleDucks_NoSearch_EveryDuckCheapestFirst()
    {
        await using var store = Shop()
            .Seed(new CatalogueState(FakeCatalogueApi.Ducks, Loading: false, Error: null));

        ShelfSelectors.VisibleDucks.Evaluate(store.State).Select(duck => duck.Id)
            .ShouldBe(["rubber", "classic", "mellow", "pirate", "ninja", "disco"]);
    }

    [Fact]
    public async Task VisibleDucks_SearchHits_OnlyTheMatches()
    {
        await using var store = Shop()
            .Seed(new CatalogueState(FakeCatalogueApi.Ducks, Loading: false, Error: null))
            .Seed(new SearchState("yel", Searching: false, Hits: ["mellow", "classic"]));

        ShelfSelectors.VisibleDucks.Evaluate(store.State).Select(duck => duck.Id).ShouldBe(["classic", "mellow"]);
    }

    // Memoized: a change elsewhere (the basket, the search box's text) leaves the ducks and
    // the hits as they were, so the selector returns the very same list and the shelf
    // doesn't re-render.
    [Fact]
    public async Task VisibleDucks_UnrelatedChange_SameListInstance()
    {
        await using var store = Shop()
            .Seed(new CatalogueState(FakeCatalogueApi.Ducks, Loading: false, Error: null));
        var visibleDucks = ShelfSelectors.VisibleDucks.Create();
        var before = visibleDucks(store.State);

        store.AddToBasket("classic");
        await store.Settled();

        visibleDucks(store.State).ShouldBeSameAs(before);
    }

    private static TestStore Shop() => TestStore.Create(ducky => ducky
        .AddSlice<CatalogueSlice>()
        .AddSlice<SearchSlice>()
        .AddSlice<BasketSlice>());
}

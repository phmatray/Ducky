using Bunit;
using DuckShop.Components;
using Microsoft.AspNetCore.Components.Web;

namespace DuckShop.Tests;

// What the reader sees in the browser at steps 4 to 7, rendered by bUnit. A store change
// re-renders a component on the renderer, a moment after the change: the tests await it
// with WaitForAssertionAsync.
public sealed class Step4ComponentTests : ShopContext
{
    [Fact]
    public async Task Step4_AddAndRemove_BadgeAndCardFollow()
    {
        var badge = Render<BasketBadge>();
        var shelf = Render<Steps.Step4.Shelf>();
        badge.Markup.ShouldContain("Basket: empty");

        await shelf.FindAll("button")[0].ClickAsync(new MouseEventArgs()); // Add to basket, on the Classic Yellow
        await badge.WaitForAssertionAsync(() => badge.Find("p").TextContent.ShouldBe("Basket: 1 duck"));
        await shelf.WaitForAssertionAsync(() => shelf.Find(".in-basket").TextContent.ShouldContain("1 in your basket"));

        await shelf.FindAll("button")[0].ClickAsync(new MouseEventArgs()); // and a second one
        await badge.WaitForAssertionAsync(() => badge.Find("p").TextContent.ShouldBe("Basket: 2 ducks"));
        await shelf.WaitForAssertionAsync(() => shelf.Find(".in-basket").TextContent.ShouldContain("2 in your basket"));

        await shelf.Find("button.link").ClickAsync(new MouseEventArgs()); // Remove one
        await badge.WaitForAssertionAsync(() => badge.Find("p").TextContent.ShouldBe("Basket: 1 duck"));
        await shelf.WaitForAssertionAsync(() => shelf.Find(".in-basket").TextContent.ShouldContain("1 in your basket"));
    }

    // A component re-renders only when a value it selected changed: adding a Classic Yellow
    // re-renders its card and the badge, never the pirate's card.
    [Fact]
    public async Task Step4_AddOneDuck_OtherCardNotRerendered()
    {
        var badge = Render<BasketBadge>();
        var shelf = Render<Steps.Step4.Shelf>();
        var cards = shelf.FindComponents<DuckCard>();
        var (classic, pirate) = (cards[0], cards[1]);
        var pirateRenders = pirate.RenderCount;

        await classic.Find("button").ClickAsync(new MouseEventArgs());

        await classic.WaitForAssertionAsync(() => classic.Markup.ShouldContain("1 in your basket"));
        await badge.WaitForAssertionAsync(() => badge.Markup.ShouldContain("Basket: 1 duck"));
        pirate.RenderCount.ShouldBe(pirateRenders);
    }
}

public sealed class Step5ComponentTests : ShopContext
{
    [Fact]
    public async Task Step5_ShelfLoading_ThenSixDucks()
    {
        await Store.InitializeAsync(Cancellation); // StoreInitialized starts the load

        var shelf = Render<Steps.Step5.Shelf>();
        shelf.Find(".status").TextContent.ShouldBe("Loading ducks…");
        Time.Advance(FakeCatalogueApi.Latency);

        await shelf.WaitForAssertionAsync(() => shelf.FindComponents<DuckCard>().Count.ShouldBe(6));
    }
}

public sealed class Step5OfflineComponentTests() : ShopContext(new OfflineCatalogueApi())
{
    [Fact]
    public async Task Step5_ServerOffline_ErrorShown()
    {
        var shelf = Render<Steps.Step5.Shelf>();

        await shelf.WaitForAssertionAsync(() => shelf.Find(".status.error").TextContent
            .ShouldBe("Couldn't load the ducks: The warehouse is offline."));
    }
}

public sealed class FinalShelfTests : ShopContext
{
    // Steps 6 and 7: the search box says what is happening, then the shelf keeps the matches,
    // cheapest first.
    [Fact]
    public async Task Search_Yel_StatusThenOnlyTheMatches()
    {
        var shelf = await LoadedShelf();

        await shelf.Find("input").InputAsync("yel");
        await shelf.WaitForAssertionAsync(() => Status(shelf).ShouldBe("Searching for “yel”…"));
        shelf.FindComponents<DuckCard>().Count.ShouldBe(6); // the shelf waits for the answer
        Time.Advance(FakeSearchApi.Latency("yel"));

        await shelf.WaitForAssertionAsync(() => Status(shelf).ShouldBe("2 ducks match."));
        await shelf.WaitForAssertionAsync(() => shelf.FindAll(".duck h2").Select(name => name.TextContent)
            .ShouldBe(["Classic Yellow", "Mellow Yellow"]));
    }

    [Fact]
    public async Task Search_NoMatch_SaysSo()
    {
        var shelf = await LoadedShelf();

        await shelf.Find("input").InputAsync("swan");
        Time.Advance(FakeSearchApi.Latency("swan"));

        await shelf.WaitForAssertionAsync(() => Status(shelf).ShouldBe("No duck matches."));
        await shelf.WaitForAssertionAsync(() => shelf.FindAll("p.status").Select(p => p.TextContent)
            .ShouldContain("No duck matches your search."));
    }

    private static string Status(IRenderedComponent<Shelf> shelf) => shelf.Find(".search .status").TextContent;

    private async Task<IRenderedComponent<Shelf>> LoadedShelf()
    {
        await Store.InitializeAsync(Cancellation);
        Time.Advance(FakeCatalogueApi.Latency);
        await Store.WhenIdleAsync(Cancellation); // the catalogue is in: no other work runs
        var shelf = Render<Shelf>();
        shelf.FindComponents<DuckCard>().Count.ShouldBe(6);
        return shelf;
    }
}

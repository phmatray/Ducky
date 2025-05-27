namespace Demo.BlazorWasm.E2E.Tests;

[TestFixture]
public class MoviesTests : TestBase
{
    [SetUp]
    public async Task SetUp()
    {
        await TestSetup();
        await NavigateAndWaitForBlazor("/movies");
    }

    [Test]
    public async Task Movies_ShouldLoadAndDisplayMovies()
    {
        // Wait for movies to load
        await Page.WaitForSelectorAsync("table tbody tr", new() { Timeout = 10000 });

        // Check that movies are displayed
        ILocator movieRows = Page.Locator("table tbody tr");
        int movieCount = await movieRows.CountAsync();
        Assert.That(movieCount, Is.GreaterThan(0));

        // Check table headers
        await Expect(Page.Locator("th:has-text('Title')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Director')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Year')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Duration')")).ToBeVisibleAsync();
        await Expect(Page.Locator("th:has-text('Score')")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Movies_SearchShouldFilterResults()
    {
        // Wait for movies to load
        await Page.WaitForSelectorAsync("table tbody tr", new() { Timeout = 10000 });

        // Get initial count
        ILocator movieRows = Page.Locator("table tbody tr");
        int initialCount = await movieRows.CountAsync();

        // Search for a specific movie - using the SearchBox component
        ILocator searchBox = Page.Locator(".search-box input, input[type='text']")
            .Filter(new() { Has = Page.Locator("label:has-text('Search movies')") });
        await searchBox.FillAsync("Matrix");

        // Wait for search to complete (debounced)
        await Task.Delay(600);

        // Check filtered results
        int filteredCount = await movieRows.CountAsync();
        Assert.That(filteredCount, Is.LessThan(initialCount));

        // Verify search results contain the search term
        IReadOnlyList<string> titles = await Page.Locator("table tbody tr td:first-child").AllTextContentsAsync();
        foreach (string title in titles)
        {
            Assert.That(title.ToLower(), Does.Contain("matrix").IgnoreCase);
        }

        // Clear search
        await searchBox.FillAsync(string.Empty);
        await Task.Delay(600);

        // Check that all movies are shown again
        await Expect(movieRows).ToHaveCountAsync(initialCount);
    }

    [Test]
    public async Task Movies_ShouldNavigateToDetailsPage()
    {
        // Wait for movies to load
        await Page.WaitForSelectorAsync("table tbody tr", new() { Timeout = 10000 });

        // Click on the first movie's info button
        ILocator infoButton = Page.Locator("table tbody tr:first-child button[aria-label*='Info']").First;
        await infoButton.ClickAsync();

        // Check navigation to details page
        await Page.WaitForURLAsync(new Regex(".*movies/\\d+$"));

        // Check details page loaded
        await Expect(Page.Locator("h1")).ToHaveTextAsync("Movie Details");
        await Expect(Page.Locator("text=Director:")).ToBeVisibleAsync();
        await Expect(Page.Locator("text=Year:")).ToBeVisibleAsync();
    }

    [Test]
    public async Task Movies_LoadingStateShouldBeShown()
    {
        // Navigate to trigger loading state
        await Page.ReloadAsync();

        // Check for loading skeleton or spinner
        ILocator loadingSkeleton = Page.Locator(".mud-skeleton");

        // Should show loading state initially
        await Expect(loadingSkeleton.First).ToBeVisibleAsync();

        // Wait for content to load
        await Page.WaitForSelectorAsync("table tbody tr", new() { Timeout = 10000 });

        // Loading state should be gone
        await Expect(loadingSkeleton).ToHaveCountAsync(0);
    }

    [Test]
    public async Task Movies_ShouldShowRatings()
    {
        // Wait for movies to load
        await Page.WaitForSelectorAsync("table tbody tr", new() { Timeout = 10000 });

        // Check that ratings are displayed
        ILocator ratings = Page.Locator(".mud-rating");
        await Expect(ratings.First).ToBeVisibleAsync();

        // Check rating stars are visible
        ILocator stars = Page.Locator(".mud-rating .mud-icon-root");
        int starCount = await stars.CountAsync();
        Assert.That(starCount, Is.GreaterThan(0));
    }
}

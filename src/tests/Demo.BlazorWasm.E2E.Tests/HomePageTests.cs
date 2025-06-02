namespace Demo.BlazorWasm.E2E.Tests;

public class HomePageTests : TestBase
{
    [Fact]
    public async Task HomePage_ShouldLoadSuccessfully()
    {
        // Arrange & Act - Already navigated in setup

        // Assert
        await Expect(Page).ToHaveTitleAsync("Ducky Blazor Demo - State Management for Blazor");

        // Check main heading
        ILocator heading = Page.Locator("h3:has-text('Serialized State')");
        await Expect(heading).ToBeVisibleAsync();

        // Check app bar title
        ILocator appBarTitle = Page.Locator(".mud-appbar h5");
        await Expect(appBarTitle).ToBeVisibleAsync();
        await Expect(appBarTitle).ToHaveTextAsync("Ducky Store");
    }

    [Fact]
    public async Task Navigation_ShouldWorkCorrectly()
    {
        // Open navigation drawer
        ILocator menuButton = Page.Locator("button[aria-label*='Menu'], .mud-appbar button:has(.mud-icon-root)").First;
        await menuButton.ClickAsync();

        // Check navigation menu is visible
        ILocator navDrawer = Page.Locator(".mud-drawer");
        await Expect(navDrawer).ToBeVisibleAsync();

        // Navigate to Counter page
        await Page.ClickAsync("text=Counter");
        await Page.WaitForURLAsync(new Regex(".*counter$"));
        await Expect(Page.Locator("h3")).ToContainTextAsync("Counter");

        // Navigate to Todo page - open menu again
        await menuButton.ClickAsync();
        await Page.ClickAsync("text=Todo");
        await Page.WaitForURLAsync(new Regex(".*todo$"));
        await Expect(Page.Locator("h3")).ToContainTextAsync("Todo List");

        // Navigate to Movies page - open menu again
        await menuButton.ClickAsync();
        await Page.ClickAsync("text=Movies");
        await Page.WaitForURLAsync(new Regex(".*movies$"));
        await Expect(Page.Locator("h3")).ToContainTextAsync("Movies");

        // Navigate back to Home - open menu again
        await menuButton.ClickAsync();
        await Page.ClickAsync("text=Home");
        await Page.WaitForURLAsync(new Regex(".*/$"));
        await Expect(Page.Locator("h3")).ToContainTextAsync("Serialized State");
    }

    [Fact]
    public async Task NotificationsDrawer_ShouldOpenAndClose()
    {
        // Check notifications badge
        ILocator notificationsBadge = Page.Locator("[aria-label='notifications']");
        await Expect(notificationsBadge).ToBeVisibleAsync();

        // Open notifications drawer
        await notificationsBadge.ClickAsync();
        ILocator drawer = Page.Locator(".mud-drawer-content");
        await Expect(drawer).ToBeVisibleAsync();

        // Check drawer content
        await Expect(drawer.Locator("h6")).ToHaveTextAsync("Notifications");

        // Close drawer
        await Page.ClickAsync(".mud-overlay");
        await Expect(drawer).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task AboutDialog_ShouldOpenFromAppBar()
    {
        // Click on Info button in app bar
        ILocator infoButton = Page.Locator(".mud-appbar button").Filter(new() { HasText = Icons.Material.Filled.Info });
        if (!await infoButton.IsVisibleAsync())
        {
            // Try alternate selector
            infoButton = Page.Locator(".mud-appbar button:has(.mud-icon-root)").Nth(1);
        }

        await infoButton.ClickAsync();

        // Check dialog appears
        ILocator dialog = Page.Locator(".mud-dialog-content");
        await Expect(dialog).ToBeVisibleAsync();
        await Expect(dialog.Locator("text=Ducky State Management")).ToBeVisibleAsync();

        // Close dialog
        await Page.ClickAsync("button:has-text('Close')");
        await Expect(dialog).Not.ToBeVisibleAsync();
    }
}

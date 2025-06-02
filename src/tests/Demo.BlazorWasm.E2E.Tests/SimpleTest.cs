namespace Demo.BlazorWasm.E2E.Tests;

public class SimpleTest : MinimalTestBase
{
    [Fact]
    public async Task CanLoadHomePage()
    {
        // Navigate using the base class method
        await NavigateAndWait();
        
        // Take screenshot for debugging
        await Page.ScreenshotAsync(new() { Path = "test-screenshot.png", FullPage = true });
        
        // Check if MudBlazor loaded
        var mudLayoutExists = await Page.Locator(".mud-layout").CountAsync() > 0;
        var mudAppBarExists = await Page.Locator(".mud-appbar").CountAsync() > 0;
        
        Console.WriteLine($"MudLayout exists: {mudLayoutExists}");
        Console.WriteLine($"MudAppBar exists: {mudAppBarExists}");
        
        // Simple assertion
        Assert.Equal("Ducky Blazor Demo - State Management for Blazor", await Page.TitleAsync());
    }
}
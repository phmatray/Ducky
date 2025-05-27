namespace Demo.BlazorWasm.E2E.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SimpleTest : PageTest
{
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }
    
    [Test]
    public async Task CanLoadHomePage()
    {
        var baseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";
        
        // Navigate
        await Page.GotoAsync(baseUrl, new PageGotoOptions 
        { 
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000 
        });
        
        // Take screenshot for debugging
        await Page.ScreenshotAsync(new() { Path = "test-screenshot.png", FullPage = true });
        
        // Wait a bit more
        await Task.Delay(5000);
        
        // Check if MudBlazor loaded
        var mudLayoutExists = await Page.Locator(".mud-layout").CountAsync() > 0;
        var mudAppBarExists = await Page.Locator(".mud-appbar").CountAsync() > 0;
        
        Console.WriteLine($"MudLayout exists: {mudLayoutExists}");
        Console.WriteLine($"MudAppBar exists: {mudAppBarExists}");
        
        // Simple assertion
        Assert.That(await Page.TitleAsync(), Is.EqualTo("Ducky"));
    }
}
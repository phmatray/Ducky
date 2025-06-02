namespace Demo.BlazorWasm.E2E.Tests;

public abstract class TestBase : PageTest
{
    protected string BaseUrl { get; private set; } = null!;

    public override BrowserNewContextOptions ContextOptions()
    {
        return new()
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }

    protected override async Task SetUp()
    {
        BaseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

        Console.WriteLine($"Navigating to {BaseUrl}...");
        
        try
        {
            // Navigate and wait for initial load with a shorter timeout
            await Page.GotoAsync(
                BaseUrl,
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded, // Changed from NetworkIdle to DOMContentLoaded
                    Timeout = 30000 // Reduced from 60 seconds to 30 seconds
                });
            
            Console.WriteLine("Navigation completed, waiting for app to initialize...");
            
            // Wait a bit for Blazor/JS to fully initialize (reduced time)
            await Task.Delay(2000);
            
            Console.WriteLine("Setup complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during navigation: {ex.Message}");
            throw;
        }
    }

    protected async Task NavigateAndWaitForBlazor(string path)
    {
        await Page.GotoAsync(
            $"{BaseUrl}{path}",
            new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 60000
            });
        
        // Wait a bit for page to stabilize
        await Task.Delay(2000);
    }
}
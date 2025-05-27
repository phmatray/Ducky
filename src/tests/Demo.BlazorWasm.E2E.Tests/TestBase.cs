namespace Demo.BlazorWasm.E2E.Tests;

public abstract class TestBase : PageTest
{
    protected string BaseUrl { get; private set; } = null!;

    [SetUp]
    public async Task TestSetup()
    {
        BaseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

        // Wait for Blazor to be ready
        await Page.GotoAsync(BaseUrl);
        await WaitForBlazorReady();
    }

    protected async Task WaitForBlazorReady()
    {
        try
        {
            // First, wait for the page to be loaded
            await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            
            // Wait for Blazor to be available
            await Page.WaitForFunctionAsync(
                "() => window.Blazor !== undefined",
                new PageWaitForFunctionOptions { Timeout = 30000 });
            
            // Wait for app content to be loaded (not loading indicator)
            await Page.WaitForFunctionAsync(
                @"() => {
                    const app = document.querySelector('#app');
                    if (!app) return false;
                    
                    // Check if loading indicator is gone
                    const loadingProgress = document.querySelector('.loading-progress');
                    if (loadingProgress && loadingProgress.offsetParent !== null) return false;
                    
                    // Check if there's actual content
                    const hasContent = app.children.length > 0 && 
                                     !app.querySelector('.loading-progress');
                    return hasContent;
                }",
                new PageWaitForFunctionOptions { Timeout = 60000 });
            
            // Additional wait for UI to stabilize
            await Task.Delay(1000);
        }
        catch (TimeoutException)
        {
            // Take a screenshot for debugging
            await Page.ScreenshotAsync(new() { Path = "blazor-timeout-debug.png", FullPage = true });
            throw;
        }
    }

    protected async Task NavigateAndWaitForBlazor(string path)
    {
        await Page.GotoAsync($"{BaseUrl}{path}");
        await WaitForBlazorReady();
    }
}

namespace Demo.BlazorWasm.E2E.Tests;

public abstract class TestBase : PageTest
{
    protected string BaseUrl { get; private set; } = null!;

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }

    [SetUp]
    public async Task TestSetup()
    {
        BaseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";

        // Navigate and wait for initial load
        await Page.GotoAsync(BaseUrl, new PageGotoOptions 
        { 
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000 
        });
        
        // Wait a bit for Blazor/JS to fully initialize
        await Task.Delay(5000);
    }

    protected async Task NavigateAndWaitForBlazor(string path)
    {
        await Page.GotoAsync($"{BaseUrl}{path}", new PageGotoOptions 
        { 
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000 
        });
        
        // Wait a bit for page to stabilize
        await Task.Delay(2000);
    }
}
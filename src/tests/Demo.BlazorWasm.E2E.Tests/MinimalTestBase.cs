namespace Demo.BlazorWasm.E2E.Tests;

public abstract class MinimalTestBase : PageTest
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
    public Task BaseSetup()
    {
        BaseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:8080";
        
        // Enable console logging
        Page.Console += (_, msg) => Console.WriteLine($"Browser console: {msg.Text}");
        Page.PageError += (_, ex) => Console.WriteLine($"Browser error: {ex}");
        
        return Task.CompletedTask;
    }
    
    protected async Task NavigateAndWait(string path = "")
    {
        var response = await Page.GotoAsync($"{BaseUrl}{path}", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 60000
        });
        
        // Give the app extra time to initialize
        await Task.Delay(3000);
    }
}
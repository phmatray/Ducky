using System.Text;

namespace Demo.BlazorWasm.E2E.Tests;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class LoadingDiagnosticsTest : PageTest
{
    private readonly List<string> _consoleLogs = new();
    private readonly List<string> _consoleErrors = new();
    
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
        };
    }
    
    [SetUp]
    public Task SetupConsoleLogging()
    {
        // Capture all console messages
        Page.Console += (_, msg) =>
        {
            var text = msg.Text;
            var type = msg.Type;
            
            if (type == "error")
            {
                _consoleErrors.Add($"[ERROR] {text}");
                
                // Also try to get the stack trace for errors
                Task.Run(async () =>
                {
                    try
                    {
                        var args = msg.Args;
                        foreach (var arg in args)
                        {
                            var json = await arg.JsonValueAsync<object>();
                            _consoleErrors.Add($"[ERROR DETAILS] {json}");
                        }
                    }
                    catch
                    {
                        // Ignore errors getting error details
                    }
                });
            }
            else
            {
                _consoleLogs.Add($"[{type.ToUpper()}] {text}");
            }
        };
        
        // Also capture page errors
        Page.PageError += (_, exception) =>
        {
            _consoleErrors.Add($"[PAGE ERROR] {exception}");
        };
        
        return Task.CompletedTask;
    }
    
    [Test]
    public async Task DiagnoseLoadingIssue()
    {
        var baseUrl = Environment.GetEnvironmentVariable("BASE_URL") ?? "http://localhost:5000";
        
        Console.WriteLine($"Navigating to {baseUrl}...");
        
        try
        {
            // Navigate with a shorter timeout to see what happens
            await Page.GotoAsync(baseUrl, new PageGotoOptions 
            { 
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30000 
            });
            
            Console.WriteLine("Initial page load completed (DOMContentLoaded)");
            
            // Wait a bit to collect logs
            await Task.Delay(5000);
            
            // Check if the app is stuck at loading
            var loadingProgress = false;
            var loadingText = "";
            try
            {
                loadingProgress = await Page.Locator("#blazor-loading-progress").IsVisibleAsync();
                loadingText = await Page.Locator("#blazor-loading-text").TextContentAsync().ConfigureAwait(false) ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking loading elements: {ex.Message}");
            }
            
            Console.WriteLine($"Loading progress visible: {loadingProgress}");
            Console.WriteLine($"Loading text: {loadingText}");
            
            // Take screenshot
            await Page.ScreenshotAsync(new() { Path = "loading-diagnostics.png", FullPage = true });
            
            // Print all console logs
            Console.WriteLine("\n=== CONSOLE LOGS ===");
            foreach (var log in _consoleLogs)
            {
                Console.WriteLine(log);
            }
            
            // Print all console errors
            Console.WriteLine("\n=== CONSOLE ERRORS ===");
            foreach (var error in _consoleErrors)
            {
                Console.WriteLine(error);
            }
            
            // Try to evaluate JavaScript to get more info
            var blazorStarted = await Page.EvaluateAsync<bool>(@"() => {
                return typeof window.Blazor !== 'undefined' && window.Blazor._internal !== undefined;
            }");
            
            Console.WriteLine($"\nBlazor started: {blazorStarted}");
            
            // Check for any DI container errors
            var diErrors = await Page.EvaluateAsync<string>(@"() => {
                try {
                    // Check if DotNet object exists
                    if (typeof DotNet === 'undefined') return 'DotNet object not found';
                    
                    // Check console for any errors
                    const errors = [];
                    
                    // Try to get any error messages from the page
                    const errorElements = document.querySelectorAll('.blazor-error-ui');
                    errorElements.forEach(el => {
                        if (el.style.display !== 'none') {
                            errors.push('Blazor error UI visible: ' + el.textContent);
                        }
                    });
                    
                    return errors.length > 0 ? errors.join('; ') : 'No DI errors found in UI';
                } catch (e) {
                    return 'Error checking DI: ' + e.toString();
                }
            }");
            
            Console.WriteLine($"DI check result: {diErrors}");
            
            // Check for Ducky store
            var duckyInfo = await Page.EvaluateAsync<string>(@"() => {
                try {
                    // Check if any Ducky-related objects exist
                    const info = [];
                    if (typeof window.DuckyStore !== 'undefined') info.push('DuckyStore exists');
                    if (typeof window.Ducky !== 'undefined') info.push('Ducky exists');
                    
                    // Check for dependency injection errors in window
                    const keys = Object.keys(window);
                    const errorKeys = keys.filter(k => k.toLowerCase().includes('error') || k.toLowerCase().includes('exception'));
                    if (errorKeys.length > 0) info.push('Error keys in window: ' + errorKeys.join(', '));
                    
                    return info.join('; ') || 'No Ducky objects found';
                } catch (e) {
                    return 'Error checking: ' + e.toString();
                }
            }");
            
            Console.WriteLine($"Ducky info: {duckyInfo}");
            
            // Check if the app actually loaded
            var appLoaded = await Page.Locator("#app").IsVisibleAsync();
            var mudLayoutExists = await Page.Locator(".mud-layout").CountAsync() > 0;
            
            Console.WriteLine($"\nApp element visible: {appLoaded}");
            Console.WriteLine($"MudLayout exists: {mudLayoutExists}");
            
            // Get the HTML content of the app div
            var appHtml = await Page.Locator("#app").InnerHTMLAsync();
            Console.WriteLine($"\nApp HTML content length: {appHtml.Length}");
            if (appHtml.Length < 500)
            {
                Console.WriteLine($"App HTML content: {appHtml}");
            }
            
            // Create a summary
            var summary = new StringBuilder();
            summary.AppendLine("\n=== DIAGNOSIS SUMMARY ===");
            summary.AppendLine($"Total console logs: {_consoleLogs.Count}");
            summary.AppendLine($"Total console errors: {_consoleErrors.Count}");
            summary.AppendLine($"Loading still visible: {loadingProgress}");
            summary.AppendLine($"Blazor started: {blazorStarted}");
            summary.AppendLine($"App loaded: {appLoaded}");
            
            Console.WriteLine(summary.ToString());
            
            // Assert that there are no console errors
            if (_consoleErrors.Count > 0)
            {
                Assert.Fail($"Found {_consoleErrors.Count} console errors. First error: {_consoleErrors.First()}");
            }
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Timeout exception: {ex.Message}");
            
            // Still print what we collected
            Console.WriteLine("\n=== CONSOLE LOGS (before timeout) ===");
            foreach (var log in _consoleLogs)
            {
                Console.WriteLine(log);
            }
            
            Console.WriteLine("\n=== CONSOLE ERRORS (before timeout) ===");
            foreach (var error in _consoleErrors)
            {
                Console.WriteLine(error);
            }
            
            throw;
        }
    }
}
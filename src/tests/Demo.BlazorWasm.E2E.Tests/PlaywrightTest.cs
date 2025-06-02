namespace Demo.BlazorWasm.E2E.Tests;

/// <summary>
/// Base class for Playwright tests with xUnit v3 support
/// </summary>
public abstract class PageTest : IAsyncLifetime, IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public IPage Page { get; private set; } = null!;
    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Browser is not initialized");
    public IBrowserContext Context => _context ?? throw new InvalidOperationException("Context is not initialized");

    /// <summary>
    /// Browser type to use for tests. Override to change browser.
    /// </summary>
    protected virtual string BrowserName => "chromium";

    /// <summary>
    /// Browser launch options. Override to customize.
    /// </summary>
    protected virtual BrowserTypeLaunchOptions BrowserTypeLaunchOptions => new() { Headless = true };

    /// <summary>
    /// Browser context options. Override to customize.
    /// </summary>
    public virtual BrowserNewContextOptions ContextOptions() => new();

    /// <summary>
    /// Called after the page is created. Override to perform setup.
    /// </summary>
    protected virtual Task SetUp() => Task.CompletedTask;

    /// <summary>
    /// Called before the browser is disposed. Override to perform cleanup.
    /// </summary>
    protected virtual Task TearDown() => Task.CompletedTask;

    async ValueTask IAsyncLifetime.InitializeAsync()
    {
        try
        {
            Console.WriteLine("Initializing Playwright...");
            _playwright = await Playwright.CreateAsync();
            
            Console.WriteLine($"Launching {BrowserName} browser...");
            _browser = BrowserName.ToLowerInvariant() switch
            {
                "chromium" => await _playwright.Chromium.LaunchAsync(BrowserTypeLaunchOptions),
                "firefox" => await _playwright.Firefox.LaunchAsync(BrowserTypeLaunchOptions),
                "webkit" => await _playwright.Webkit.LaunchAsync(BrowserTypeLaunchOptions),
                _ => throw new NotSupportedException($"Browser '{BrowserName}' is not supported")
            };

            Console.WriteLine("Creating browser context...");
            _context = await _browser.NewContextAsync(ContextOptions());
            Page = await _context.NewPageAsync();

            Console.WriteLine("Running test setup...");
            await SetUp();
            Console.WriteLine("Test initialization complete.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during test initialization: {ex}");
            throw;
        }
    }

    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        try
        {
            await TearDown();
        }
        finally
        {
            if (_context != null)
            {
                await _context.DisposeAsync();
            }

            if (_browser != null)
            {
                await _browser.DisposeAsync();
            }

            _playwright?.Dispose();
        }
    }
}

/// <summary>
/// Assertion helpers for Playwright
/// </summary>
public static class Assertions
{
    public static ILocatorAssertions Expect(ILocator locator) 
        => Microsoft.Playwright.Assertions.Expect(locator);

    public static IPageAssertions Expect(IPage page) 
        => Microsoft.Playwright.Assertions.Expect(page);

    public static IAPIResponseAssertions Expect(IAPIResponse response) 
        => Microsoft.Playwright.Assertions.Expect(response);
}
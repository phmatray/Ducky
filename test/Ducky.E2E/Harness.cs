using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ducky.E2E;

// SPEC §17.2: one browser (DUCKY_BROWSER: chromium, the default, firefox or webkit) and a Kestrel static-file host over
// harness/ (ducky.js is linked in by the csproj). Every page comes from NewPageAsync: Playwright's clock belongs to the
// browser context, so it is installed once per context, before its first page loads anything, and, in Chromium, CDP
// precise coverage is started for each page. Pages may be opened concurrently. On dispose the ducky.js coverage of every
// open page goes to DUCKY_JS_COVERAGE_DIR, which the E2E target merges and gates once (§19).
internal sealed class Harness : IAsyncDisposable
{
    private readonly List<(IPage Page, ICDPSession? Cdp)> _pages = [];
    private readonly List<JsonElement> _coverage = [];
    private readonly Lock _gate = new();
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly WebApplication _host;

    private Harness(IPlaywright playwright, IBrowser browser, WebApplication host)
    {
        _playwright = playwright;
        _browser = browser;
        _host = host;
        BaseUrl = new Uri(host.Urls.Single());
    }

    public static string BrowserName =>
        Environment.GetEnvironmentVariable("DUCKY_BROWSER") is { Length: > 0 } name ? name : "chromium";

    public static bool MeasuresCoverage => BrowserName == "chromium";

    public Uri BaseUrl { get; }

    public static async Task<Harness> StartAsync()
    {
        var host = await StartStaticHostAsync(Path.Combine(AppContext.BaseDirectory, "harness"));
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright[BrowserName].LaunchAsync();
        return new Harness(playwright, browser, host);
    }

    // Also serves the trimmed WASM publish (SPEC §17.2) once WasmTrimmedSmoke lands.
    public static async Task<WebApplication> StartStaticHostAsync(string root)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(root) });
        await app.StartAsync();
        return app;
    }

    // A new browser context unless one is given (two pages of one context share storage, as two tabs do). A given context
    // is always the Context of an earlier harness page, so it already has the clock: installing it again would reset the
    // fake time of every page of the context to wall-clock now.
    public async Task<IPage> NewPageAsync(IBrowserContext? context = null)
    {
        var created = context is null;
        context ??= await _browser.NewContextAsync(new() { BaseURL = BaseUrl.ToString() });
        var page = await context.NewPageAsync();
        if (created)
        {
            await page.Clock.InstallAsync();
        }
        ICDPSession? cdp = null;
        if (MeasuresCoverage)
        {
            cdp = await context.NewCDPSessionAsync(page);
            await cdp.SendAsync("Profiler.enable");
            await cdp.SendAsync("Profiler.startPreciseCoverage", new() { ["callCount"] = true, ["detailed"] = true });
        }
        lock (_gate)
        {
            _pages.Add((page, cdp));
        }
        return page;
    }

    // The V8 ScriptCoverage of every script whose URL passes the filter, from every page. Profiler.takePreciseCoverage
    // resets the counts, so each call adds a delta per open page to what earlier calls took; the merge sums the copies.
    // ponytail: a page closed before the last call loses its last delta, which can only fail the gate, never pass it;
    // collect in a close helper if a spec ever has to close a page early.
    public async Task<List<JsonElement>> TakeCoverageAsync(Func<string, bool> url)
    {
        List<(IPage Page, ICDPSession? Cdp)> pages;
        lock (_gate)
        {
            pages = _pages.Where(p => p.Cdp is not null && !p.Page.IsClosed).ToList();
        }
        foreach (var (_, cdp) in pages)
        {
            var taken = await cdp!.SendAsync("Profiler.takePreciseCoverage");
            lock (_gate)
            {
                _coverage.AddRange(taken!.Value.GetProperty("result").EnumerateArray());
            }
        }
        lock (_gate)
        {
            return _coverage.Where(s => url(s.GetProperty("url").GetString()!)).ToList();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Environment.GetEnvironmentVariable("DUCKY_JS_COVERAGE_DIR") is { Length: > 0 } directory)
        {
            var scripts = await TakeCoverageAsync(JsBlockCoverage.IsDuckyJs);
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"{Guid.NewGuid():N}.json"), JsonSerializer.Serialize(scripts));
        }
        await _browser.DisposeAsync();
        _playwright.Dispose();
        await _host.DisposeAsync();
    }
}

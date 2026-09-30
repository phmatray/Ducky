using Microsoft.Playwright;

namespace Ducky.E2E;

// Stage 1 (SPEC §24): the harness spec over the stub ducky.js; replaced by the real ducky.js specs (M6-01), which
// delete it and its manifest entry.
public sealed class SkeletonTests
{
    private static readonly string _harness = Path.Combine(AppContext.BaseDirectory, "harness");

    [Fact]
    public async Task Skeleton_DuckyE2E_Smoke()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();
        await page.RouteAsync("http://ducky.test/**", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Path = Path.Combine(_harness, new Uri(route.Request.Url).AbsolutePath.TrimStart('/')),
        }));

        await page.GotoAsync("http://ducky.test/index.html");
        var ready = await page.EvaluateAsync<bool>("async () => (await import('/ducky.js')).ready()");

        ready.ShouldBeTrue();
    }
}

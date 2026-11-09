using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Ducky.E2E.Samples;

// M14-07: the Ducky Playground (samples/Ducky.Playground), published by the E2E target like every sample of Ducky.slnx and
// served from its wwwroot by the static-file host, as GitHub Pages serves it. A smoke of the shop's main path, plus the
// X-ray drawer reading the source files the build copies beside the app (non-normative).
public sealed class PlaygroundTests
{
    private const float BootTimeoutMs = 60_000;

    [Fact]
    public async Task Playground_Boots_SearchAddToCartCheckout()
    {
        await using var harness = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory("Ducky.Playground"), "wwwroot"));
        var page = await harness.NewPageAsync();
        var log = new ConcurrentQueue<string>();
        page.Console += (_, message) => log.Enqueue($"{message.Type}: {message.Text}");
        page.PageError += (_, error) => log.Enqueue($"pageerror: {error}");
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                log.Enqueue($"HTTP {response.Status} {response.Url}");
            }
        };

        await page.GotoAsync("/");
        try
        {
            await page.Locator(".duck").First.WaitForAsync(new() { Timeout = BootTimeoutMs });
        }
        catch (TimeoutException)
        {
            Assert.Fail($"no duck on the shelf within {BootTimeoutMs} ms; the page's log:{Environment.NewLine}{string.Join(Environment.NewLine, log)}");
        }

        // Search: the Switch effect answers the last term only.
        await page.Locator(".search-field input").PressSequentiallyAsync("the classic");
        await Assertions.Expect(page.Locator(".duck")).ToHaveCountAsync(1);
        await Assertions.Expect(page.Locator(".duck-name")).ToHaveTextAsync("The Classic");

        // Basket, then checkout: the Exhaust effect pays once and the order appears.
        await page.Locator(".duck .add").ClickAsync();
        await Assertions.Expect(page.Locator("#basket-title")).ToContainTextAsync("1 duck");
        await page.Locator(".pay").ClickAsync();
        await Assertions.Expect(page.Locator(".steps li")).ToHaveCountAsync(5, new() { Timeout = 10_000 });
        await Assertions.Expect(page.Locator("#basket-title")).Not.ToContainTextAsync("duck");

        // X-ray: a tag opens the drawer on the app's own source, with the feature's line marked.
        await page.Locator(".xswitch").ClickAsync();
        await page.Locator(".xtag", new() { HasText = "Exhaust" }).ClickAsync();
        await Assertions.Expect(page.Locator(".slab .ln.mark")).ToContainTextAsync("Concurrency.Exhaust");

        log.Where(line => line.StartsWith("error", StringComparison.Ordinal) || line.StartsWith("pageerror", StringComparison.Ordinal) || line.StartsWith("HTTP", StringComparison.Ordinal))
            .ShouldBeEmpty($"the page logged:{Environment.NewLine}{string.Join(Environment.NewLine, log)}");
    }
}

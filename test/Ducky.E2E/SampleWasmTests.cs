using System.Collections.Concurrent;

namespace Ducky.E2E;

// The trimmed standalone sample publish in a real browser (SPEC §17.6). bUnit runs untrimmed assemblies, so a member the
// trimmer removes (the private setter of DuckyComponent's or DuckyLayout's inherited [Inject] Store, without its
// DynamicDependency) passes every bUnit test and leaves this page on DuckyInitializer's "Loading…" (SPEC §11.2 Trimming).
public sealed class SampleWasmTests
{
    private const float RenderTimeoutMs = 60_000;

    [Fact]
    public async Task Wasm_SampleIndex_RendersWithoutRendererError()
    {
        await using var harness = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory("Ducky.Samples.Wasm"), "wwwroot"));
        var page = await harness.NewPageAsync();
        var log = new ConcurrentQueue<string>();
        var critical = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Console += (_, message) =>
        {
            log.Enqueue($"{message.Type}: {message.Text}");
            if (message.Text.Contains("crit:", StringComparison.Ordinal))
            {
                critical.TrySetResult(message.Text);
            }
        };
        page.Response += (_, response) =>
        {
            if (response.Status >= 400)
            {
                log.Enqueue($"HTTP {response.Status} {response.Url}");
            }
        };

        await page.GotoAsync("/");
        var heading = page.Locator("h1").WaitForAsync(new() { Timeout = RenderTimeoutMs });

        // A renderer error fails at once with its log line instead of waiting out the render timeout.
        if (await Task.WhenAny(heading, critical.Task) == critical.Task)
        {
            Assert.Fail($"the page logged a renderer error:{Environment.NewLine}{string.Join(Environment.NewLine, log)}");
        }
        if (!heading.IsCompletedSuccessfully)
        {
            Assert.Fail($"no h1 within {RenderTimeoutMs} ms; the page's log:{Environment.NewLine}{string.Join(Environment.NewLine, log)}");
        }
        (await page.Locator("h1").TextContentAsync()).ShouldBe("Todos");
        (await page.Locator("[data-testid=remaining]").TextContentAsync()).ShouldBe("0 left");
        (await page.Locator("[data-testid=layout-remaining]").TextContentAsync()).ShouldBe("0 open");
        critical.Task.IsCompleted.ShouldBeFalse($"a renderer error was logged after the page rendered:{Environment.NewLine}{string.Join(Environment.NewLine, log)}");
    }
}

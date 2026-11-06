using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace Ducky.E2E.Persistence;

// The prerender seed end to end against the Interactive Auto sample (SPEC §11.4, INV-15): loaded once, persisted with an
// explicit Interactive Auto mode into both the server and the WebAssembly payloads, kept within the start-circuit budget,
// and the interactivity gate's probe of a store touched first by plain code (spike S-7, answer 3).
public sealed class PrerenderTests
{
    // Above every seed budget the search can need: 20 KB of envelope is far more than 20 KB of estimated wire (×2.4).
    private const int SearchCeiling = 20 * 1024;

    // /products loads its Prerender slice in an effect on StoreInitialized. The prerender pass loads it once and seeds it;
    // the circuit takes the seed (WasRestored), so its effect skips the load: one catalogue call for the page, and no
    // request to /api/products.
    [Fact]
    public async Task Prerender_NoDoubleLoad_NetworkCalledOnce()
    {
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        var apiCalls = CollectProductRequests(page);
        await ServerPages.HoldWebAssemblyAsync(page);

        await page.GotoAsync(host, "/products");

        await Assertions.Expect(page.GetByTestId("idle")).ToHaveTextAsync("Server", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.Locator("[data-testid=products] li")).ToHaveTextAsync(["Rubber duck", "Duck pond", "Bread crumbs"]);
        (await ServerPages.LoadsAsync(host)).ShouldBe("1");
        apiCalls.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    // Both render modes configured: the seed is in the server and the WebAssembly state of the page, and each side applies
    // it, the circuit on a first visit and the WebAssembly store once its resources are cached (spike S-7, answer 5).
    [Fact]
    public async Task Prerender_ServerAndWasmModesConfigured_SeedPersistedAndApplied()
    {
        await using var host = await ServerPages.StartAsync();
        var html = await ServerPages.GetAsync(host, "/products");
        html.ShouldContain("<!--Blazor-Server-Component-State:");
        ServerPages.Seed(html).Carries("products").ShouldBeTrue();

        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        var apiCalls = CollectProductRequests(page);
        var releaseWebAssembly = await ServerPages.HoldWebAssemblyAsync(page);
        await page.GotoAsync(host, "/products");
        await Assertions.Expect(page.GetByTestId("idle")).ToHaveTextAsync("Server", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        // The circuit took the seed from the (data-protected) server copy: the loads are the two prerender passes, the GET
        // above and this visit, and the circuit's load effect skipped its own.
        (await ServerPages.LoadsAsync(host)).ShouldBe("2");
        apiCalls.ShouldBeEmpty();

        // A WebAssembly-only page boots the runtime, so the next Interactive Auto page renders in WebAssembly. Its document
        // request runs a server pass too, which loads and seeds the products before the page arrives (the third load);
        // its WebAssembly store, interactive once the not-prerendered count shows, takes that seed and requests nothing.
        releaseWebAssembly();
        await ServerPages.WaitForWebAssemblyAsync(page);
        await page.GotoAsync(host, "/handler");
        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("0", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        (await ServerPages.LoadsAsync(host)).ShouldBe("3");
        apiCalls.ShouldBeEmpty();

        await page.GotoAsync(host, "/products");

        await Assertions.Expect(page.GetByTestId("idle")).ToHaveTextAsync("WebAssembly", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.Locator("[data-testid=products] li")).ToHaveTextAsync(["Rubber duck", "Duck pond", "Bread crumbs"]);
        (await ServerPages.LoadsAsync(host)).ShouldBe("4"); // the prerender pass only
        apiCalls.ShouldBeEmpty();
        errors.ShouldBeEmpty();
    }

    // A 100 KB slice filled during prerender is left out of the seed (it would blow the 32 KB start-circuit message): the
    // circuit still starts and loads the slice again itself (not restored).
    [Fact]
    public async Task Prerender_LargeSlice_InteractiveServer_CircuitStarts()
    {
        const int Chars = 100 * 1024;
        await using var host = await ServerPages.StartAsync();
        var seed = ServerPages.Seed(await ServerPages.GetAsync(host, $"/large?chars={Chars}"));
        seed.Carries("products").ShouldBeTrue();
        seed.Carries("large").ShouldBeFalse();

        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, $"/large?chars={Chars}");

        await Assertions.Expect(page.GetByTestId("fill-1k")).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync($"{Chars}");
        await Assertions.Expect(page.GetByTestId("restored")).ToHaveTextAsync("False");
        await page.GetByTestId("fill-1k").ClickAsync();
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync("1024");
        errors.ShouldBeEmpty();
    }

    // The largest quote-dense and CJK slices the budget admits (found by bisection on the prerendered seed, so N is carried
    // and N + 1 is not) still start the circuit: the seed travels as byte[] (×4/3 whatever the content), never as a string
    // whose escaping grows quotes and CJK up to about 3×, so the estimate holds for any content (SPEC §11.4).
    [Theory]
    [InlineData("quote")]
    [InlineData("cjk")]
    public async Task Prerender_SeedJustUnderBudget_QuoteDenseAndCjk_InteractiveServer_CircuitStarts(string kind)
    {
        await using var host = await ServerPages.StartAsync();
        async Task<bool> Carried(int chars) => ServerPages.Seed(await ServerPages.GetAsync(host, $"/large/{kind}?chars={chars}")).Carries("large");
        (await Carried(1)).ShouldBeTrue();
        (await Carried(SearchCeiling)).ShouldBeFalse();
        var (carried, omitted) = (1, SearchCeiling);
        while (omitted - carried > 1)
        {
            var middle = (carried + omitted) / 2;
            (carried, omitted) = await Carried(middle) ? (middle, omitted) : (carried, middle);
        }

        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, $"/large/{kind}?chars={carried}");

        await Assertions.Expect(page.GetByTestId("fill-1k")).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.GetByTestId("kind")).ToHaveTextAsync(kind);
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync($"{carried}");
        await Assertions.Expect(page.GetByTestId("restored")).ToHaveTextAsync("True"); // from the seed, not filled again
        await page.GetByTestId("fill-1k").ClickAsync();
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync("1024");
        errors.ShouldBeEmpty();
    }

    // /probe is a plain component under a layout without DuckyInitializer that reads the store first, so the gate is
    // Unknown when persistence asks and probes the runtime. Prerender (RemoteJSRuntime: an already-faulted import task) is
    // non-interactive: browser storage is not read and hydration does not fail. The circuit is interactive: the Local
    // counter written on / comes back.
    [Fact]
    public async Task Gate_Unknown_ProbeDetectsPrerender()
    {
        await using var host = await ServerPages.StartAsync();
        var html = await ServerPages.GetAsync(host, "/probe");
        html.ShouldContain("""<span data-testid="renderer">Static</span>""");
        html.ShouldContain("""<span data-testid="status">Hydrated</span>""");
        html.ShouldContain("""<span data-testid="count">0</span>""");

        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, "/");
        var increment = page.GetByTestId("increment");
        await Assertions.Expect(increment).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await increment.ClickAsync();
        await increment.ClickAsync();
        await page.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":2')",
            null, new() { PollingInterval = 50 });

        await ServerPages.WaitForWebAssemblyAsync(page);
        await page.GotoAsync(host, "/probe");

        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("2", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.GetByTestId("renderer")).ToHaveTextAsync("Server");
        await Assertions.Expect(page.GetByTestId("status")).ToHaveTextAsync("Hydrated");
        errors.ShouldBeEmpty();
    }

    private static ConcurrentQueue<string> CollectProductRequests(IPage page)
    {
        var requests = new ConcurrentQueue<string>();
        page.Request += (_, request) =>
        {
            if (new Uri(request.Url).AbsolutePath == "/api/products")
            {
                requests.Enqueue(request.Url);
            }
        };
        return requests;
    }
}

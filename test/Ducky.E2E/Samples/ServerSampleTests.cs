using System.Net;
using Microsoft.Playwright;

namespace Ducky.E2E.Samples;

// M14-02: samples/Ducky.Samples.Server (Interactive Auto, stage 18) builds, is published by the E2E target and starts with
// the PublishedHost fixture (SPEC §17.2). The prerender and interactive checks prove the shared registration passes every
// configuration check (DUCKY310-317 surface only at the first store resolution) on the server and in the WASM client.
public sealed class ServerSampleTests
{
    private const string Sample = "Ducky.Samples.Server";

    [Fact]
    public async Task ServerSample_StartsWithHostFixture_HealthEndpointAnswers()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        using var http = new HttpClient { BaseAddress = host.BaseUrl };

        using var response = await http.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }

    // The products slice is Prerender<T> and loaded by an effect on StoreInitialized: the prerendered HTML already lists
    // them, and the server catalogue counted exactly one load for the request.
    [Fact]
    public async Task ServerSample_Prerender_HtmlShowsEffectLoadedSlice()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        using var http = new HttpClient { BaseAddress = host.BaseUrl };
        var ct = TestContext.Current.CancellationToken;

        var html = await http.GetStringAsync(new Uri("/products", UriKind.Relative), ct);

        html.ShouldContain("<li>Rubber duck</li>");
        html.ShouldContain("<li>Duck pond</li>");
        (await http.GetStringAsync(new Uri("/api/products/loads", UriKind.Relative), ct)).ShouldBe("1");
    }

    // Interactive Auto: the first visit (a fresh browser context, no cached WebAssembly resources) runs on a circuit; the
    // counter reduces, persists to localStorage and re-renders. Interactivity waits cover the circuit start, store init
    // and hydration on a freshly started host, on a 2-vCPU runner.
    [Fact]
    public async Task ServerSample_Counter_InteractiveAndPersistedLocally()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = CollectErrors(page);
        await page.GotoAsync(new Uri(host.BaseUrl, "/").ToString());
        // The button is rendered disabled until the component is interactive.
        var increment = page.GetByTestId("increment");
        await Assertions.Expect(increment).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("renderer")).ToHaveTextAsync("Server");

        await increment.ClickAsync();
        await increment.ClickAsync();

        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("2");
        await page.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":2')",
            null, new() { PollingInterval = 50 });
        errors.ShouldBeEmpty();
    }

    // The WASM side of the shared registration: a WebAssembly-only page injects the app's one store, renders and dispatches
    // (DuckyComponent's injected properties must be settable through Mono's reflection).
    [Fact]
    public async Task ServerSample_WasmRenderMode_ComponentInjectsRendersAndDispatches()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = CollectErrors(page);
        await page.GotoAsync(new Uri(host.BaseUrl, "/handler").ToString());
        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("0", new() { Timeout = 30_000 });

        await page.GetByTestId("ping").ClickAsync();

        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("1");
        errors.ShouldBeEmpty();
    }

    // A signed-in user's NameIdentifier-scoped Server-storage slice survives a reload (a new circuit reads the cache), and
    // neither another user nor an anonymous visitor (a null scope reads nothing, INV-17) sees it.
    [Fact]
    public async Task ServerSample_ServerStorage_ScopedToSignedInUser()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        await SignInAsync(page, host, "alice");
        var add = page.GetByTestId("add-note");
        await Assertions.Expect(add).ToBeEnabledAsync(new() { Timeout = 30_000 });

        await add.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("1");
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("1");

        await SignInAsync(page, host, "bob");
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("0");
        await page.GotoAsync(new Uri(host.BaseUrl, "/logout").ToString());
        await page.GotoAsync(new Uri(host.BaseUrl, "/circuit").ToString());
        await Assertions.Expect(page.GetByTestId("user")).ToHaveTextAsync("anonymous");
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("0");
        await SignInAsync(page, host, "alice");
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("1");
    }

    // /auto puts a circuit store and a WebAssembly store in one document (the §17.6 Interactive Auto specs run against it):
    // both panels become interactive, each on its own renderer.
    [Fact]
    public async Task ServerSample_AutoPage_CircuitAndWasmPanelsInteractive()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = CollectErrors(page);
        await page.GotoAsync(new Uri(host.BaseUrl, "/auto").ToString());

        await Assertions.Expect(page.GetByTestId("increment-server")).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId("increment-wasm")).ToBeEnabledAsync(new() { Timeout = 30_000 });

        await Assertions.Expect(page.GetByTestId("renderer-server")).ToHaveTextAsync("Server");
        await Assertions.Expect(page.GetByTestId("renderer-wasm")).ToHaveTextAsync("WebAssembly");
        errors.ShouldBeEmpty();
    }

    private static async Task SignInAsync(IPage page, PublishedHost host, string user)
    {
        await page.GotoAsync(new Uri(host.BaseUrl, $"/login?user={user}&returnUrl=/circuit").ToString());
        await Assertions.Expect(page.GetByTestId("user")).ToHaveTextAsync(user);
    }

    private static List<string> CollectErrors(IPage page)
    {
        var errors = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
            {
                errors.Add(message.Text);
            }
        };
        page.PageError += (_, error) => errors.Add(error);
        return errors;
    }
}

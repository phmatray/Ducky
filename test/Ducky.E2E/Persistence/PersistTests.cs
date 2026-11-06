using Microsoft.Playwright;

namespace Ducky.E2E.Persistence;

// Browser storage end to end (SPEC §11.5, §17.6): a reload restores what the store wrote, in the standalone WASM sample and
// on a circuit of the Server sample, and a large slice crosses JS→.NET by stream without closing the circuit (INV-23, S-5).
public sealed class PersistTests
{
    [Fact]
    public async Task Persist_ReloadRestores_WasmAndServer()
    {
        // Standalone WebAssembly: the todos slice (Local, synced) survives a reload.
        await using (var wasm = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory("Ducky.Samples.Wasm"), "wwwroot")))
        {
            var page = await wasm.NewPageAsync();
            var errors = ServerPages.CollectErrors(page);
            await page.GotoAsync("/");
            await page.GetByTestId("new-todo").FillAsync("Buy bread", new() { Timeout = ServerPages.InteractiveTimeoutMs });
            await page.GetByTestId("add").ClickAsync();
            await Assertions.Expect(page.GetByTestId("remaining")).ToHaveTextAsync("1 left");
            await page.WaitForFunctionAsync("() => Object.values(localStorage).some(v => v.includes('Buy bread'))", null, new() { PollingInterval = 50 });

            await page.ReloadAsync();

            await Assertions.Expect(page.GetByTestId("todo")).ToContainTextAsync("Buy bread", new() { Timeout = ServerPages.InteractiveTimeoutMs });
            await Assertions.Expect(page.GetByTestId("remaining")).ToHaveTextAsync("1 left");
            errors.ShouldBeEmpty();
        }

        // A circuit of the Server sample: the Local large slice survives a reload. /large is Interactive Server, so a circuit
        // store reads it back whatever the browser cached (an Interactive Auto page may come back in WebAssembly).
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var serverPage = await harness.NewPageAsync();
        var serverErrors = ServerPages.CollectErrors(serverPage);
        await serverPage.GotoAsync(host, "/large");
        var fill = serverPage.GetByTestId("fill-1k");
        await Assertions.Expect(fill).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await fill.ClickAsync();
        await Assertions.Expect(serverPage.GetByTestId("length")).ToHaveTextAsync("1024");
        await serverPage.WaitForFunctionAsync($"() => (localStorage.getItem('{ServerPages.LargeKey}') ?? '').length > 1024",
            null, new() { PollingInterval = 50, Timeout = ServerPages.InteractiveTimeoutMs });

        await serverPage.ReloadAsync();

        // Prerender can't read localStorage (it shows the empty slice); the circuit store restores it.
        await Assertions.Expect(serverPage.GetByTestId("length")).ToHaveTextAsync("1024", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        serverErrors.ShouldBeEmpty();
    }

    // S-5 / D11: 100 KB and about 2 MB written to localStorage by a circuit store and read back through IJSStreamReference
    // by the next circuit, which stays open (a later click still re-renders, no error is logged).
    [Fact]
    public async Task Persist_LargeSlice_OnServer_RoundTrips()
    {
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, "/large");

        foreach (var (button, chars) in new[] { ("fill-100k", 100 * 1024), ("fill-2m", 2 * 1024 * 1024) })
        {
            var fill = page.GetByTestId(button);
            await Assertions.Expect(fill).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
            await fill.ClickAsync();
            await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync($"{chars}", new() { Timeout = ServerPages.InteractiveTimeoutMs });
            await page.WaitForFunctionAsync($"() => (localStorage.getItem('{ServerPages.LargeKey}') ?? '').length > {chars}",
                null, new() { PollingInterval = 100, Timeout = ServerPages.InteractiveTimeoutMs });

            await page.ReloadAsync();

            // The prerendered page shows the empty initial slice; only the stream read brings the value back.
            await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync($"{chars}", new() { Timeout = ServerPages.InteractiveTimeoutMs });
            await Assertions.Expect(page.GetByTestId("restored")).ToHaveTextAsync("True");
        }

        await page.GetByTestId("fill-1k").ClickAsync();
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync("1024", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        errors.ShouldBeEmpty();
    }
}

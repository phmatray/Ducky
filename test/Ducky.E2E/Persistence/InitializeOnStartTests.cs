using Microsoft.Playwright;

namespace Ducky.E2E.Persistence;

// SPEC §11.1 InitializeOnStart in a real browser: the WASM sample has no <DuckyInitializer> and no Program.cs preload, so
// only the hosted service AddDucky registers (run by WebAssemblyHost.RunAsync after the page's state was restored) starts
// the store's initialization. A reload must hydrate the stored todo and never overwrite it with the initial state.
public sealed class InitializeOnStartTests
{
    [Fact]
    public async Task WasmSample_WithoutInitializer_HydratesFromStorageOnReload()
    {
        await using var wasm = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory("Ducky.Samples.Wasm"), "wwwroot"));
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
        (await page.EvaluateAsync<bool>("() => Object.values(localStorage).some(v => v.includes('Buy bread'))")).ShouldBeTrue();
        errors.ShouldBeEmpty();
    }
}

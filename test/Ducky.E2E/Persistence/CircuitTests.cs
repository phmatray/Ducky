using Microsoft.Playwright;

namespace Ducky.E2E.Persistence;

// A circuit paused and resumed, or whose connection drops and comes back, against the Interactive Auto sample (SPEC §11.4,
// §11.6, INV-15, INV-17; spike S-7, answers 4 and 6). Pause and resume go through Blazor.pauseCircuit/resumeCircuit on a
// page whose connection was never forced closed: after Blazor._internal.forceCloseConnection() the .NET 10 client no
// longer sends PauseCircuit (S-7).
public sealed class CircuitTests
{
    // The resumed circuit is a new store; it takes the pause seed, so the Prerender products slice is restored and its load
    // effect skips the load: the catalogue still counted one load, the prerender's. The runtime download stays held: a
    // resumed circuit resolves its Interactive Auto components again, to WebAssembly once the runtime has loaded.
    [Fact]
    public async Task Circuit_Resume_SeedRestoresPrerenderSlices()
    {
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await ServerPages.HoldWebAssemblyAsync(page);
        await page.GotoAsync(host, "/products");
        await Assertions.Expect(page.GetByTestId("idle")).ToHaveTextAsync("Server", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        (await ServerPages.LoadsAsync(host)).ShouldBe("1");

        await PauseAsync(page);
        // The resumed circuit renders its components afresh; this marker comes back only from the new store, once idle.
        await page.EvaluateAsync("() => document.querySelector('[data-testid=idle]').remove()");
        await ResumeAsync(page);

        await Assertions.Expect(page.GetByTestId("idle")).ToHaveTextAsync("Server", new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await Assertions.Expect(page.Locator("[data-testid=products] li")).ToHaveTextAsync(["Rubber duck", "Duck pond", "Bread crumbs"]);
        (await ServerPages.LoadsAsync(host)).ShouldBe("1");
        errors.ShouldBeEmpty();
    }

    // The large slice (Local, Prerender) holds 1 KB in the circuit while localStorage still holds an older 100 KB value: the
    // write failed (here localStorage.setItem throws for the key, standing in for writes lost while the connection was
    // down), so the key is dirty when the pause is taken. The resumed store keeps the seed value over the stale storage and
    // rewrites storage with it (SPEC §11.4, the dirty-key case of ADR-0011).
    [Fact]
    public async Task Circuit_ResumeAfterDisconnect_PauseSeedWinsOverStaleLocalStorage()
    {
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, "/large");
        var fill = page.GetByTestId("fill-100k");
        await Assertions.Expect(fill).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await fill.ClickAsync();
        await page.WaitForFunctionAsync($"() => (localStorage.getItem('{ServerPages.LargeKey}') ?? '').length > {100 * 1024}",
            null, new() { PollingInterval = 50, Timeout = ServerPages.InteractiveTimeoutMs });

        await page.EvaluateAsync($$"""
            () => {
                const setItem = Storage.prototype.setItem;
                window.failedWrites = 0;
                window.restoreSetItem = () => { Storage.prototype.setItem = setItem; };
                Storage.prototype.setItem = function (key, value) {
                    if (key === '{{ServerPages.LargeKey}}') {
                        window.failedWrites++;
                        throw new DOMException('the write is lost', 'QuotaExceededError');
                    }
                    return setItem.call(this, key, value);
                };
            }
            """);
        await page.GetByTestId("fill-1k").ClickAsync();
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync("1024");
        await page.WaitForFunctionAsync("() => window.failedWrites > 0", null, new() { PollingInterval = 50 });

        await PauseAsync(page);
        // Every write of the paused store failed: storage is stale, and only the resumed store can rewrite it. While writes
        // still fail, the resumed store tries: the dirty key's writer was signalled from the seed.
        var failedBeforeResume = await page.EvaluateAsync<int>("() => window.failedWrites");
        await ResumeAsync(page);
        await page.WaitForFunctionAsync($"() => window.failedWrites > {failedBeforeResume}",
            null, new() { PollingInterval = 50, Timeout = ServerPages.InteractiveTimeoutMs });
        await page.EvaluateAsync("() => window.restoreSetItem()");

        await page.WaitForFunctionAsync($"() => (localStorage.getItem('{ServerPages.LargeKey}') ?? '').length < 2048",
            null, new() { PollingInterval = 50, Timeout = ServerPages.InteractiveTimeoutMs });
        (await page.EvaluateAsync<string>($"() => localStorage.getItem('{ServerPages.LargeKey}')")).ShouldContain(new string('x', 1024));
        await Assertions.Expect(page.GetByTestId("length")).ToHaveTextAsync("1024");
        await Assertions.Expect(page.GetByTestId("restored")).ToHaveTextAsync("True");
        errors.ShouldBeEmpty();
    }

    // INV-17 on reconnect: a signed-in user's NameIdentifier-scoped Server-storage notes, changed just before the connection
    // drops. The reconnected circuit keeps its store and re-asserts the same user, which is absorbed (no reset, no re-read,
    // §11.6): the count never shows the initial 0 nor rolls back, and the change is in the cache.
    [Fact]
    public async Task Circuit_ReconnectWithScopedSlices_NoResetFlicker()
    {
        await using var host = await ServerPages.StartAsync();
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerPages.CollectErrors(page);
        await page.GotoAsync(host, "/login?user=alice&returnUrl=/circuit");
        var add = page.GetByTestId("add-note");
        await Assertions.Expect(add).ToBeEnabledAsync(new() { Timeout = ServerPages.InteractiveTimeoutMs });
        await add.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("1");

        // Every value the notes count and the reconnect element take from now on.
        await page.EvaluateAsync("""
            () => {
                const notes = document.querySelector('[data-testid=notes]');
                const modal = document.getElementById('components-reconnect-modal');
                window.seenNotes = [notes.textContent];
                window.seenStates = [modal.className];
                new MutationObserver(() => window.seenNotes.push(notes.textContent))
                    .observe(notes, { childList: true, characterData: true, subtree: true });
                new MutationObserver(() => window.seenStates.push(modal.className))
                    .observe(modal, { attributes: true, attributeFilter: ['class'] });
            }
            """);
        await add.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("2");
        await page.GetByTestId("drop").ClickAsync();
        await page.WaitForFunctionAsync("() => window.seenStates.some(s => s.includes('components-reconnect-show'))", null, new() { PollingInterval = 10 });
        await page.WaitForFunctionAsync("() => document.getElementById('components-reconnect-modal').className.includes('components-reconnect-hide')",
            null, new() { PollingInterval = 50, Timeout = ServerPages.InteractiveTimeoutMs });

        await add.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("3");
        // Consecutive repeats are dropped: a re-render may replace the text node with the same value.
        (await page.EvaluateAsync<string[]>("() => window.seenNotes.filter((v, i, a) => i === 0 || v !== a[i - 1])")).ShouldBe(["1", "2", "3"]);
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("3");
        errors.ShouldBeEmpty();
    }

    private static async Task PauseAsync(IPage page) =>
        (await page.EvaluateAsync<bool>("() => Blazor.pauseCircuit()")).ShouldBeTrue();

    private static async Task ResumeAsync(IPage page) =>
        (await page.EvaluateAsync<bool>("() => Blazor.resumeCircuit()")).ShouldBeTrue();
}

using System.Text;
using System.Text.RegularExpressions;
using Ducky.E2E.Samples;
using Microsoft.Playwright;

namespace Ducky.E2E.CrossTab;

// SPEC §11.7, §17.6 (INV-18, INV-23): cross-tab sync between two pages of one browser context, which share localStorage
// as two tabs do, against the published Samples.Wasm and Samples.Server. "Another script" is a plain same-origin page
// (/health on the server sample) that touches localStorage itself.
public sealed class CrossTabTests
{
    private const string Sample = "Ducky.Samples.Server";
    private const string Wasm = "Ducky.Samples.Wasm";

    // The default hub limit (HubOptions.MaximumReceiveMessageSize): a value sent inline above it would end the circuit.
    private const int HubMessageLimit = 32 * 1024;
    private static readonly LocatorAssertionsToBeEnabledOptions _interactive = new() { Timeout = 30_000 };
    private static readonly PageWaitForFunctionOptions _poll = new() { PollingInterval = 50, Timeout = 30_000 };

    // Counts the storage listeners the page attaches and removes.
    private const string CountStorageListeners = """
        window.storageListeners = 0;
        const add = window.addEventListener, remove = window.removeEventListener;
        window.addEventListener = function (type, ...rest) { if (type === 'storage') storageListeners++; return add.call(this, type, ...rest); };
        window.removeEventListener = function (type, ...rest) { if (type === 'storage') storageListeners--; return remove.call(this, type, ...rest); };
        """;

    // Records each OnStorageChanged delivery the page sends (blazor.web.js's DotNetObject, patched once it loaded): the
    // registration id ducky.js names, when it was sent on the page clock, and how it settled. ducky.js counts a delivery as
    // failed when it rejects or is still unsettled 1 s after it was sent (§11.7); 1.1 s leaves its timer room to run.
    private const string RecordDeliveries = """
        () => {
            window.deliveries = [];
            window.failedOrOk = d => d.outcome !== null || Date.now() - d.at > 1100;
            const invoke = DotNet.DotNetObject.prototype.invokeMethodAsync;
            DotNet.DotNetObject.prototype.invokeMethodAsync = function (method, ...args) {
                const call = invoke.call(this, method, ...args);
                if (method === 'OnStorageChanged') {
                    const delivery = { id: args[5], at: Date.now(), outcome: null };
                    deliveries.push(delivery);
                    call.then(() => delivery.outcome = 'ok', () => delivery.outcome = 'rejected');
                }
                return call;
            };
        }
        """;

    // Records every localStorage/sessionStorage write the page makes, once writes is set to an array.
    private const string RecordWrites = """
        window.writes = null;
        const setItem = Storage.prototype.setItem;
        Storage.prototype.setItem = function (key, value) {
            if (writes) writes.push(`${key}=${value}`);
            return setItem.call(this, key, value);
        };
        """;

    // INV-18: a change made in tab A appears in tab B and back, on the standalone WebAssembly sample.
    [Fact]
    public async Task CrossTab_TwoPages_ChangeInAAppearsInB()
    {
        await using var harness = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory(Wasm), "wwwroot"));
        var a = await harness.NewPageAsync();
        var b = await harness.NewPageAsync(a.Context);
        var errors = ServerSampleTests.CollectErrors(a);
        var errorsB = ServerSampleTests.CollectErrors(b);
        await OpenTodosAsync(a);
        await OpenTodosAsync(b);

        await AddTodoAsync(a, "Buy milk");

        await Assertions.Expect(b.GetByTestId("todo")).ToContainTextAsync(["Buy milk"]);
        await Assertions.Expect(b.GetByTestId("remaining")).ToHaveTextAsync("1 left");
        await b.GetByTestId("todo").GetByRole(AriaRole.Checkbox).CheckAsync();
        await Assertions.Expect(a.GetByTestId("remaining")).ToHaveTextAsync("0 left");
        await Assertions.Expect(a.GetByTestId("todo").GetByRole(AriaRole.Checkbox)).ToBeCheckedAsync();
        errors.ShouldBeEmpty();
        errorsB.ShouldBeEmpty();
    }

    // §11.7: a subscriber that re-dispatches on every change of the synced counter (CounterResynced, an equal but new
    // state) can't start a ping-pong: a receipt never writes and sets the baseline, so the re-dispatched state dedupes. One
    // increment per tab, each awaited in the other, is one counter write per tab, its own increment, within SPEC's "at
    // most 2". A receipt that wrote, or left the baseline behind, would write the re-dispatched value too: a second write
    // in each tab (the other tab drops that echo against its own baseline, so the ping-pong would stop right there).
    [Fact]
    public async Task CrossTab_SubscriberThatRedispatches_ConvergesWithinNWrites()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var b = await harness.NewPageAsync(a.Context);
        var errors = ServerSampleTests.CollectErrors(a);
        var errorsB = ServerSampleTests.CollectErrors(b);
        foreach (var page in (IPage[])[a, b])
        {
            await page.AddInitScriptAsync(RecordWrites + "writes = [];");
            await page.GotoAsync(new Uri(host.BaseUrl, "/redispatch").ToString());
            await Assertions.Expect(page.GetByTestId("increment")).ToBeEnabledAsync(_interactive);
        }

        await a.GetByTestId("increment").ClickAsync();
        await Assertions.Expect(b.GetByTestId("count")).ToHaveTextAsync("1");
        await b.GetByTestId("increment").ClickAsync();
        await Assertions.Expect(a.GetByTestId("count")).ToHaveTextAsync("2");
        await Assertions.Expect(b.GetByTestId("count")).ToHaveTextAsync("2");
        await a.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":2')", null, _poll);

        foreach (var (page, value) in ((IPage, int)[])[(a, 1), (b, 2)])
        {
            var writes = await page.EvaluateAsync<string[]>("() => writes.filter(w => w.startsWith('ducky-samples:counter='))");
            writes.ShouldHaveSingleItem().ShouldContain($"\"value\":{value}");
        }
        errors.ShouldBeEmpty();
        errorsB.ShouldBeEmpty();
    }

    // §11.7: each store registers once, however often its components are created again: after enhanced navigations
    // between two InteractiveServer pages the page holds one storage listener, and another tab's change still arrives.
    [Fact]
    public async Task CrossTab_ListenerRegisteredOnceAcrossReinit()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(a);
        await a.AddInitScriptAsync(CountStorageListeners);
        await OpenLargeAsync(a, host);
        await a.WaitForFunctionAsync("() => storageListeners === 1", null, _poll);
        // Every load resets the count, so the navigations must stay enhanced ones in this document.
        await a.EvaluateAsync("() => { window.sameDocument = true; }");

        for (var round = 0; round < 2; round++)
        {
            await a.GetByRole(AriaRole.Link, new() { Name = "Circuit", Exact = true }).ClickAsync();
            await Assertions.Expect(a.GetByTestId("add-note")).ToBeEnabledAsync(_interactive);
            await a.GetByRole(AriaRole.Link, new() { Name = "Large", Exact = true }).ClickAsync();
            await Assertions.Expect(a.GetByTestId("fill-cjk")).ToBeEnabledAsync(_interactive);
        }
        (await a.EvaluateAsync<bool>("() => window.sameDocument === true")).ShouldBeTrue();
        var b = await harness.NewPageAsync(a.Context);
        await OpenLargeAsync(b, host);
        await b.GetByTestId("fill-cjk").ClickAsync();

        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("cjk");
        await Assertions.Expect(a.GetByTestId("length")).ToHaveTextAsync("12288");
        (await a.EvaluateAsync<int>("() => storageListeners")).ShouldBe(1);
        errors.ShouldBeEmpty();
    }

    // §11.7: localStorage.clear() from another script (a storage event with a null key) resets the synced counter in both
    // stores of /auto, while the unsynced Local preferences, which no tab event may touch, keep their in-memory value. The
    // WASM store writes nothing back: the barrier increment's write is the only one recorded after the clear. (A write-back
    // by the circuit store would sit in its 250 ms debounce and coalesce into the barrier's value; bUnit
    // CrossTab_RemovalReceipt_NoWriteBack covers that.)
    [Fact]
    public async Task CrossTab_ClearFromOtherScript_ResetsSyncedSlices()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(a);
        await a.AddInitScriptAsync(RecordWrites);
        await OpenAutoAsync(a, host);
        await a.GetByTestId("increment-server").ClickAsync();
        await a.GetByTestId("toggle-theme-server").ClickAsync();
        await Assertions.Expect(a.GetByTestId("count-wasm")).ToHaveTextAsync("1");
        await Assertions.Expect(a.GetByTestId("dark-wasm")).ToHaveTextAsync("True");
        await a.WaitForFunctionAsync("""
            () => (localStorage.getItem('ducky-samples:counter') ?? '').includes('"value":1')
                && (localStorage.getItem('ducky-samples:preferences') ?? '').includes('"dark":true')
            """, null, _poll);
        await a.EvaluateAsync("() => { writes = []; }");
        var script = await harness.NewPageAsync(a.Context);
        await script.GotoAsync(new Uri(host.BaseUrl, "/health").ToString());

        await script.EvaluateAsync("() => localStorage.clear()");

        await Assertions.Expect(a.GetByTestId("count-server")).ToHaveTextAsync("0");
        await Assertions.Expect(a.GetByTestId("count-wasm")).ToHaveTextAsync("0");
        await Assertions.Expect(a.GetByTestId("dark-server")).ToHaveTextAsync("True");
        await Assertions.Expect(a.GetByTestId("dark-wasm")).ToHaveTextAsync("True");
        await a.GetByTestId("increment-wasm").ClickAsync();
        await Assertions.Expect(a.GetByTestId("count-server")).ToHaveTextAsync("1");
        await a.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":1')", null, _poll);
        // The clear's re-read pass is over by now (the barrier's relay came after it), so a reset preference would show.
        await Assertions.Expect(a.GetByTestId("dark-server")).ToHaveTextAsync("True");
        await Assertions.Expect(a.GetByTestId("dark-wasm")).ToHaveTextAsync("True");
        var writes = await a.EvaluateAsync<string[]>("() => writes");
        writes.ShouldHaveSingleItem().ShouldStartWith("ducky-samples:counter=");
        writes[0].ShouldContain("\"value\":1");
        (await a.EvaluateAsync<string?>("() => localStorage.getItem('ducky-samples:preferences')")).ShouldBeNull();
        errors.ShouldBeEmpty();
    }

    // INV-18, §11.5: ClearPersistedState in tab B removes the key and resets tab A, which writes nothing back, while B
    // keeps its in-memory todos. A's next change is the only write A makes after the clear.
    [Fact]
    public async Task CrossTab_ClearPersistedStateInOtherTab_ResetsSlice()
    {
        await using var harness = await Harness.StartAsync(Path.Combine(PublishedHost.SampleDirectory(Wasm), "wwwroot"));
        var a = await harness.NewPageAsync();
        var b = await harness.NewPageAsync(a.Context);
        var errors = ServerSampleTests.CollectErrors(a);
        var errorsB = ServerSampleTests.CollectErrors(b);
        await a.AddInitScriptAsync(RecordWrites);
        await OpenTodosAsync(a);
        await OpenTodosAsync(b);
        await AddTodoAsync(a, "Buy milk");
        await Assertions.Expect(b.GetByTestId("todo")).ToContainTextAsync(["Buy milk"]);
        await a.EvaluateAsync("() => { writes = []; }");

        await b.GetByTestId("clear-persisted").ClickAsync();

        await Assertions.Expect(a.GetByTestId("todo")).ToHaveCountAsync(0);
        await Assertions.Expect(a.GetByTestId("remaining")).ToHaveTextAsync("0 left");
        await Assertions.Expect(b.GetByTestId("todo")).ToContainTextAsync(["Buy milk"]);
        (await a.EvaluateAsync<int>("() => localStorage.length")).ShouldBe(0);
        await AddTodoAsync(a, "Eggs");
        await Assertions.Expect(b.GetByTestId("todo")).ToContainTextAsync(["Eggs"]);
        await Assertions.Expect(b.GetByTestId("todo")).ToHaveCountAsync(1);
        var writes = await a.EvaluateAsync<string[]>("() => writes");
        writes.ShouldHaveSingleItem().ShouldContain("Eggs");
        writes[0].ShouldNotContain("Buy milk");
        errors.ShouldBeEmpty();
        errorsB.ShouldBeEmpty();
    }

    // §11.7, INV-23: a 100 KB value from another tab crosses as tooLarge and the circuit store pulls it by stream: it
    // arrives whole, no message the page sent reached the hub limit an inline value would have exceeded, and the circuit
    // still answers a click.
    [Fact]
    public async Task CrossTab_LargeValue_ReadViaStream()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(a);
        var sent = RecordSentFrames(a);
        await OpenLargeAsync(a, host);
        var b = await harness.NewPageAsync(a.Context);
        var errorsB = ServerSampleTests.CollectErrors(b);
        await OpenLargeAsync(b, host);

        await b.GetByTestId("fill-100k").ClickAsync();

        await Assertions.Expect(a.GetByTestId("length")).ToHaveTextAsync("102400");
        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("ascii");
        await a.GetByTestId("fill-cjk").ClickAsync();
        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("cjk");
        AssertBelowHubLimit(sent);
        errors.ShouldBeEmpty();
        errorsB.ShouldBeEmpty();
    }

    // §11.9, INV-23: an envelope of 12 Ki raw CJK characters (another script writes it: the sample's serializer escapes
    // non-ASCII as \uXXXX) is under the 16 KiB budget in UTF-16 code units but about 36 KB of UTF-8, so the listener
    // measures bytes and sends it by stream; inline it would exceed the hub limit and end the circuit, which still answers
    // a click afterwards.
    [Fact]
    public async Task CrossTab_NonAsciiValueUnderCharLimitOverByteLimit_UsesStream()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(a);
        var sent = RecordSentFrames(a);
        await OpenLargeAsync(a, host);
        var script = await harness.NewPageAsync(a.Context);
        await script.GotoAsync(new Uri(host.BaseUrl, "/health").ToString());

        var sizes = await script.EvaluateAsync<int[]>("""
            () => {
                const value = JSON.stringify({ v: 1, at: new Date().toISOString(), s: { kind: 'cjk', text: '語'.repeat(12 * 1024) } });
                localStorage.setItem('ducky-samples:large', value);
                return [value.length, new TextEncoder().encode(JSON.stringify(value)).length];
            }
            """);

        sizes[0].ShouldBeLessThan(16 * 1024);
        sizes[1].ShouldBeGreaterThan(HubMessageLimit);
        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("cjk");
        await Assertions.Expect(a.GetByTestId("length")).ToHaveTextAsync("12288");
        await a.GetByTestId("fill-100k").ClickAsync();
        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("ascii");
        AssertBelowHubLimit(sent);
        errors.ShouldBeEmpty();
    }

    // §11.7: while the circuit of /auto is disconnected (its hub negotiation refused, so it can't reconnect), the WASM
    // store changes a Local and a Session slice; both relays to the circuit store fail. Once the circuit reconnects, the
    // module re-reads each failed (source, area) pair and the circuit store converges on both.
    [Fact]
    public async Task CrossTab_EventDuringCircuitDisconnect_ConvergesAfterReconnect()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var refuse = false;
        await a.RouteAsync(new Regex("/_blazor/negotiate"), route => refuse ? route.AbortAsync() : route.ContinueAsync());
        await OpenAutoAsync(a, host);

        refuse = true;
        await a.EvaluateAsync("() => Blazor._internal.forceCloseConnection()");
        await a.WaitForFunctionAsync("() => document.getElementById('components-reconnect-modal').classList.contains('components-reconnect-show')", null, _poll);
        await a.GetByTestId("increment-wasm").ClickAsync();
        var draft = a.GetByTestId("draft-wasm");
        await draft.FillAsync("offline");
        await draft.BlurAsync();
        await Assertions.Expect(a.GetByTestId("count-wasm")).ToHaveTextAsync("1");
        await a.WaitForFunctionAsync("() => (sessionStorage.getItem('ducky-samples:draft') ?? '').includes('\"text\":\"offline\"')", null, _poll);
        // The relays to the circuit store count as failed once unsettled for the first backoff step (1 s).
        await a.Clock.RunForAsync(1_500);
        (await a.GetByTestId("count-server").TextContentAsync()).ShouldBe("0");

        refuse = false;
        await a.EvaluateAsync("() => Blazor.reconnect()");

        await Assertions.Expect(a.GetByTestId("count-server")).ToHaveTextAsync("1", new() { Timeout = 30_000 });
        await Assertions.Expect(a.GetByTestId("draft-value-server")).ToHaveTextAsync("offline");
    }

    // §11.7, §17.6: two ids whose .NET object is gone, both pruned at the pruneAfterMs cap (10 minutes, on Playwright's
    // clock) and not before it. On page A, a paused and resumed circuit leaves its old store's registration behind (its
    // unwatchStorage failed with the circuit) beside the new store's. blazor.web.js sends every JS-to-.NET call over the
    // circuit's current connection, so the old registration's deliveries reach the resumed circuit, never a connection
    // error. Its DotNetObject id either names no object there (Blazor's generic invocation error: no DetailedErrors, so
    // never the untracked-object fast path) or the one the new circuit gave the new store's reference (ids restart per
    // circuit); the store then rejects the delivery, which names the old registration's id. Either relation ends in a
    // rejection, so the test holds whichever one Blazor's id numbering produces. On page C, a circuit that stays
    // disconnected (its hub negotiation refused) leaves deliveries that never settle, which count as failed after 1 s:
    // the connection-error case. (C opens the InteractiveServer /large: a second tab's / would run in WebAssembly.)
    [Fact]
    public async Task CrossTab_CircuitResumed_OldListenerRemoved()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        await a.AddInitScriptAsync(CountStorageListeners);
        await a.GotoAsync(new Uri(host.BaseUrl, "/").ToString());
        await Assertions.Expect(a.GetByTestId("increment")).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(a.GetByTestId("renderer")).ToHaveTextAsync("Server");
        await a.WaitForFunctionAsync("() => storageListeners === 1", null, _poll);
        var c = await harness.NewPageAsync(a.Context);
        var refuse = false;
        await c.RouteAsync(new Regex("/_blazor/negotiate"), route => refuse ? route.AbortAsync() : route.ContinueAsync());
        await c.AddInitScriptAsync(CountStorageListeners);
        await OpenLargeAsync(c, host);
        await c.WaitForFunctionAsync("() => storageListeners === 1", null, _poll);

        (await a.EvaluateAsync<bool>("() => Blazor.pauseCircuit()")).ShouldBeTrue();
        // Under load the server can answer the first ResumeCircuit before the paused state is available (false); the
        // circuit stays paused on the client, so the resume is retried.
        (await a.EvaluateAsync<bool>("""
            async () => {
                for (let attempt = 0; attempt < 20; attempt++) {
                    if (await Blazor.resumeCircuit()) return true;
                    await new Promise(resolve => setTimeout(resolve, 250));
                }
                return false;
            }
            """)).ShouldBeTrue();
        await a.WaitForFunctionAsync("() => storageListeners === 2", null, _poll);
        refuse = true;
        await c.EvaluateAsync("() => Blazor._internal.forceCloseConnection()");
        await c.WaitForFunctionAsync("() => document.getElementById('components-reconnect-modal').classList.contains('components-reconnect-show')", null, _poll);
        await a.EvaluateAsync(RecordDeliveries);
        await c.EvaluateAsync(RecordDeliveries);
        var script = await harness.NewPageAsync(a.Context);
        await script.GotoAsync(new Uri(host.BaseUrl, "/health").ToString());

        await WriteCounterAsync(script, 5);

        // The failure streaks start: on A the new store's delivery succeeds and the old registration's is rejected; on C the
        // delivery stays unsettled.
        await Assertions.Expect(a.GetByTestId("count")).ToHaveTextAsync("5");
        await a.WaitForFunctionAsync("() => deliveries.length >= 2 && deliveries.slice(0, 2).every(failedOrOk)", null, _poll);
        (await a.EvaluateAsync<string[]>("() => deliveries.slice(0, 2).map(d => d.outcome).sort()")).ShouldBe(["ok", "rejected"]);
        await c.WaitForFunctionAsync("() => deliveries.length >= 1 && failedOrOk(deliveries[0])", null, _poll);
        (await c.EvaluateAsync<string?>("() => deliveries[0].outcome")).ShouldBeNull();

        // Short of the cap, the retries each jump fires fail again and prune nothing. The jump also times out A's hub
        // connection, which reconnects to the same circuit.
        await JumpAndSettleAsync(9 * 60_000, a, c);
        (await a.EvaluateAsync<int>("() => storageListeners")).ShouldBe(2);
        (await c.EvaluateAsync<int>("() => storageListeners")).ShouldBe(1);

        // Past it, the next failed retry prunes each id.
        await a.Clock.FastForwardAsync("01:05");
        await a.WaitForFunctionAsync("() => storageListeners === 1", null, _poll);
        await c.WaitForFunctionAsync("() => storageListeners === 0", null, _poll);
        await WriteCounterAsync(script, 7);
        await Assertions.Expect(a.GetByTestId("count")).ToHaveTextAsync("7", new() { Timeout = 30_000 });
        (await a.EvaluateAsync<int>("() => storageListeners")).ShouldBe(1);
    }

    // §11.9 (spike S-5): a too-large key removed between the notification and the pull. The real Blazor runtime turns the
    // one-byte Uint8Array storageGetStream returns into a stream reference of Length 1, which the circuit store reads as a
    // removal: it resets the slice. Tab A's own storage listener, attached before ducky.js's, removes the key as the event
    // arrives; removeItem fires no event in A, so A sees only the tooLarge notification. (That a removal receipt writes
    // nothing back is bUnit CrossTab_RemovalReceipt_NoWriteBack: a circuit store's 250 ms write debounce makes it
    // unobservable here without a wall-clock wait.)
    [Fact]
    public async Task CrossTab_TooLargeKeyRemovedBeforePull_RealRuntime_ResetsSlice()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var a = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(a);
        await a.AddInitScriptAsync("""
            window.removeOnArrival = false;
            addEventListener('storage', e => {
                if (removeOnArrival && e.key === 'ducky-samples:large' && e.newValue !== null) {
                    removeOnArrival = false;
                    localStorage.removeItem(e.key);
                }
            });
            """);
        await OpenLargeAsync(a, host);
        var b = await harness.NewPageAsync(a.Context);
        var errorsB = ServerSampleTests.CollectErrors(b);
        await OpenLargeAsync(b, host);
        await b.GetByTestId("fill-cjk").ClickAsync();
        await Assertions.Expect(a.GetByTestId("length")).ToHaveTextAsync("12288");
        await a.EvaluateAsync("() => { removeOnArrival = true; }");

        await b.GetByTestId("fill-100k").ClickAsync();

        await a.WaitForFunctionAsync("() => !removeOnArrival", null, _poll);
        await Assertions.Expect(a.GetByTestId("kind")).ToHaveTextAsync("ascii");
        await Assertions.Expect(a.GetByTestId("length")).ToHaveTextAsync("0");
        // B hears A's removal and resets too.
        await Assertions.Expect(b.GetByTestId("length")).ToHaveTextAsync("0");
        (await a.EvaluateAsync<string?>("() => localStorage.getItem('ducky-samples:large')")).ShouldBeNull();
        errors.ShouldBeEmpty();
        errorsB.ShouldBeEmpty();
    }

    private static async Task OpenTodosAsync(IPage page)
    {
        await page.GotoAsync("/");
        await Assertions.Expect(page.GetByTestId("remaining")).ToHaveTextAsync("0 left", new() { Timeout = 60_000 });
    }

    private static async Task AddTodoAsync(IPage page, string title)
    {
        await page.GetByTestId("new-todo").FillAsync(title);
        await page.GetByTestId("add").ClickAsync();
        await Assertions.Expect(page.GetByTestId("todo").Last).ToContainTextAsync(title);
    }

    private static async Task OpenLargeAsync(IPage page, PublishedHost host)
    {
        await page.GotoAsync(new Uri(host.BaseUrl, "/large").ToString());
        await Assertions.Expect(page.GetByTestId("fill-cjk")).ToBeEnabledAsync(_interactive);
    }

    // Jumps the context's clock by ms, then waits on each page until a registration was pruned (its storage listener
    // count dropped), or it sent a delivery past the jump and every such delivery failed or succeeded, so a prune that
    // failure would cause has happened. The retry the jump leads to can wait out one backoff step (up to 30 s).
    private static async Task JumpAndSettleAsync(long ms, params IPage[] pages)
    {
        var listeners = new int[pages.Length];
        for (var i = 0; i < pages.Length; i++)
        {
            listeners[i] = await pages[i].EvaluateAsync<int>("() => storageListeners");
        }

        var landed = await pages[0].EvaluateAsync<long>("ms => Date.now() + ms", ms);
        await pages[0].Clock.FastForwardAsync(ms);
        for (var i = 0; i < pages.Length; i++)
        {
            await pages[i].WaitForFunctionAsync("""
                ([landed, listeners]) => {
                    const since = deliveries.filter(d => d.at >= landed);
                    return storageListeners < listeners || (since.length > 0 && since.every(failedOrOk));
                }
                """, new object[] { landed, listeners[i] }, new() { PollingInterval = 50, Timeout = 60_000 });
        }
    }

    private static async Task OpenAutoAsync(IPage page, PublishedHost host)
    {
        await page.GotoAsync(new Uri(host.BaseUrl, "/auto").ToString());
        await Assertions.Expect(page.GetByTestId("increment-server")).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(page.GetByTestId("increment-wasm")).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(page.GetByTestId("renderer-wasm")).ToHaveTextAsync("WebAssembly");
    }

    // Another script writes a counter envelope (§11.5: version 1, written-at, state) the way a store would.
    private static async Task WriteCounterAsync(IPage script, int value) =>
        await script.EvaluateAsync(
            "value => localStorage.setItem('ducky-samples:counter', JSON.stringify({ v: 1, at: new Date().toISOString(), s: { value } }))",
            value);

    // The size of every WebSocket message the page sends (the circuit's hub connection).
    private static List<int> RecordSentFrames(IPage page)
    {
        var sizes = new List<int>();
        page.WebSocket += (_, socket) => socket.FrameSent += (_, frame) =>
        {
            lock (sizes)
            {
                sizes.Add(frame.Binary?.Length ?? Encoding.UTF8.GetByteCount(frame.Text ?? ""));
            }
        };
        return sizes;
    }

    private static void AssertBelowHubLimit(List<int> sent)
    {
        lock (sent)
        {
            sent.ShouldNotBeEmpty();
            sent.Max().ShouldBeLessThan(HubMessageLimit);
        }
    }
}

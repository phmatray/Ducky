using System.Text.Json;
using Ducky.E2E.Samples;
using Microsoft.Playwright;

namespace Ducky.E2E.Auto;

// SPEC §17.6 (Interactive Auto, store identity, DevTools) against samples/Ducky.Samples.Server. /auto renders the same
// StorePanel twice in one document, once on a circuit ("server") and once in WebAssembly ("wasm"): two stores sharing one
// localStorage, one sessionStorage and one ducky.js module instance (§11.7). Interactivity waits cover the circuit start,
// the WebAssembly download and store init on a freshly started host, on a 2-vCPU runner.
public sealed class InteractiveAutoTests
{
    private const string Sample = "Ducky.Samples.Server";
    private static readonly LocatorAssertionsToBeEnabledOptions _interactive = new() { Timeout = 30_000 };

    // INV-18: a write of either store reaches the other through the same-document relay, both ways, on a synced Local slice.
    [Fact]
    public async Task Auto_ServerAndWasmStoresInOneDocument_Converge()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await OpenAutoAsync(page, host);

        await page.GetByTestId("increment-server").ClickAsync();
        await Assertions.Expect(page.GetByTestId("count-wasm")).ToHaveTextAsync("1");

        await page.GetByTestId("increment-wasm").ClickAsync();
        await Assertions.Expect(page.GetByTestId("count-server")).ToHaveTextAsync("2");
        await Assertions.Expect(page.GetByTestId("count-wasm")).ToHaveTextAsync("2");
        await page.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":2')",
            null, new() { PollingInterval = 50 });
        errors.ShouldBeEmpty();
    }

    // INV-18: the relay also covers what storage events never carry across tabs: a Session slice and an unsynced Local slice.
    [Fact]
    public async Task Auto_SessionAndUnsyncedLocalSlices_ConvergeInOneDocument()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await OpenAutoAsync(page, host);

        await ChangeDraftAsync(page, "server", "hello");
        await Assertions.Expect(page.GetByTestId("draft-value-wasm")).ToHaveTextAsync("hello");
        await ChangeDraftAsync(page, "wasm", "world");
        await Assertions.Expect(page.GetByTestId("draft-value-server")).ToHaveTextAsync("world");
        await page.GetByTestId("toggle-theme-wasm").ClickAsync();
        await Assertions.Expect(page.GetByTestId("dark-server")).ToHaveTextAsync("True");
        await page.GetByTestId("toggle-theme-server").ClickAsync();
        await Assertions.Expect(page.GetByTestId("dark-wasm")).ToHaveTextAsync("False");

        await page.WaitForFunctionAsync("""
            () => (sessionStorage.getItem('ducky-samples:draft') ?? '').includes('"text":"world"')
                && (localStorage.getItem('ducky-samples:preferences') ?? '').includes('"dark":false')
                && localStorage.getItem('ducky-samples:draft') === null
            """, null, new() { PollingInterval = 50 });
        errors.ShouldBeEmpty();
    }

    // INV-18, §11.5: ClearPersistedState in the circuit store removes every browser key and resets the WASM store, which
    // writes nothing back; the clearing store keeps its in-memory state. A spy on Storage.setItem records every write after
    // the clear; a later WASM increment, which the browser writer (no debounce) flushes after any write-back the reset
    // would have caused, proves the spy saw the WASM store's writes, so the increment must be the only one recorded.
    [Fact]
    public async Task Auto_ClearInCircuitStore_ResetsWasmStore()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await page.AddInitScriptAsync("""
            window.writes = null;
            const setItem = Storage.prototype.setItem;
            Storage.prototype.setItem = function (key, value) {
                if (writes && key.startsWith('ducky-samples:')) writes.push(`${key}=${value}`);
                return setItem.call(this, key, value);
            };
            """);
        await OpenAutoAsync(page, host);
        await page.GetByTestId("increment-wasm").ClickAsync();
        await page.GetByTestId("increment-wasm").ClickAsync();
        await page.GetByTestId("toggle-theme-server").ClickAsync();
        await ChangeDraftAsync(page, "server", "hello");
        await Assertions.Expect(page.GetByTestId("count-server")).ToHaveTextAsync("2");
        await Assertions.Expect(page.GetByTestId("dark-wasm")).ToHaveTextAsync("True");
        await Assertions.Expect(page.GetByTestId("draft-value-wasm")).ToHaveTextAsync("hello");
        await page.WaitForFunctionAsync("""
            () => ['counter', 'preferences'].every(k => localStorage.getItem(`ducky-samples:${k}`) !== null)
                && sessionStorage.getItem('ducky-samples:draft') !== null
            """, null, new() { PollingInterval = 50 });
        await page.EvaluateAsync("() => { writes = []; }");

        await page.GetByTestId("clear-server").ClickAsync();

        await Assertions.Expect(page.GetByTestId("count-wasm")).ToHaveTextAsync("0");
        await Assertions.Expect(page.GetByTestId("dark-wasm")).ToHaveTextAsync("False");
        await Assertions.Expect(page.GetByTestId("draft-value-wasm")).ToHaveTextAsync("");
        await Assertions.Expect(page.GetByTestId("count-server")).ToHaveTextAsync("2");
        await Assertions.Expect(page.GetByTestId("dark-server")).ToHaveTextAsync("True");
        await Assertions.Expect(page.GetByTestId("draft-value-server")).ToHaveTextAsync("hello");
        (await page.EvaluateAsync<string?[]>("""
            () => [localStorage.getItem('ducky-samples:counter'), localStorage.getItem('ducky-samples:preferences'),
                sessionStorage.getItem('ducky-samples:draft')]
            """)).ShouldBe([null, null, null]);
        await page.GetByTestId("increment-wasm").ClickAsync();
        await page.WaitForFunctionAsync("() => (localStorage.getItem('ducky-samples:counter') ?? '').includes('\"value\":1')",
            null, new() { PollingInterval = 50 });
        var writes = await page.EvaluateAsync<string[]>("() => writes");
        writes.ShouldHaveSingleItem().ShouldStartWith("ducky-samples:counter=");
        writes[0].ShouldContain("\"value\":1");
        errors.ShouldBeEmpty();
    }

    // §11.6: the shared registration (with its [Persist(Storage = Server)] notes) starts on the WASM side, which registers
    // neither IDistributedCache nor AuthenticationStateProvider: no configuration error, Status Hydrated, and the inert
    // notes are never read there while the circuit store reads the signed-in user's cached notes.
    [Fact]
    public async Task Auto_SharedServerPersistRegistration_WasmStartsAndSkipsServerSlices()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await ServerSampleTests.SignInAsync(page, host, "alice");
        var add = page.GetByTestId("add-note");
        await Assertions.Expect(add).ToBeEnabledAsync(_interactive);
        await add.ClickAsync();
        await Assertions.Expect(page.GetByTestId("notes")).ToHaveTextAsync("1");

        await OpenAutoAsync(page, host);

        await Assertions.Expect(page.GetByTestId("services-wasm")).ToHaveTextAsync("cache: False, auth: False");
        await Assertions.Expect(page.GetByTestId("services-server")).ToHaveTextAsync("cache: True, auth: True");
        await Assertions.Expect(page.GetByTestId("status-wasm")).ToHaveTextAsync("Hydrated");
        await Assertions.Expect(page.GetByTestId("status-server")).ToHaveTextAsync("Hydrated");
        await Assertions.Expect(page.GetByTestId("notes-server")).ToHaveTextAsync("1");
        await Assertions.Expect(page.GetByTestId("notes-wasm")).ToHaveTextAsync("0");
        errors.ShouldBeEmpty();
    }

    // §11.7: each store registers under its own id, so the circuit store and the WASM store of one page each hold their own
    // storage listener: another tab's write reaches both, and their writes reach the other tab.
    [Fact]
    public async Task CrossTab_CircuitAndWasmStoresInSamePage_IndependentListeners()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var auto = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(auto);
        await auto.AddInitScriptAsync("""
            window.storageListeners = 0;
            const add = window.addEventListener, remove = window.removeEventListener;
            window.addEventListener = function (type, ...rest) { if (type === 'storage') storageListeners++; return add.call(this, type, ...rest); };
            window.removeEventListener = function (type, ...rest) { if (type === 'storage') storageListeners--; return remove.call(this, type, ...rest); };
            """);
        await OpenAutoAsync(auto, host);
        await auto.WaitForFunctionAsync("() => storageListeners === 2", null, new() { PollingInterval = 50, Timeout = 30_000 });
        var other = await harness.NewPageAsync(auto.Context);
        await other.GotoAsync(new Uri(host.BaseUrl, "/").ToString());
        var increment = other.GetByTestId("increment");
        await Assertions.Expect(increment).ToBeEnabledAsync(_interactive);

        await increment.ClickAsync();

        await Assertions.Expect(auto.GetByTestId("count-server")).ToHaveTextAsync("1");
        await Assertions.Expect(auto.GetByTestId("count-wasm")).ToHaveTextAsync("1");

        // One store at a time: the stores converge on whole values, so two concurrent increments would count once.
        await auto.GetByTestId("increment-wasm").ClickAsync();
        await Assertions.Expect(other.GetByTestId("count")).ToHaveTextAsync("2");
        await Assertions.Expect(auto.GetByTestId("count-server")).ToHaveTextAsync("2");
        await auto.GetByTestId("increment-server").ClickAsync();

        await Assertions.Expect(other.GetByTestId("count")).ToHaveTextAsync("3");
        (await auto.EvaluateAsync<int>("() => storageListeners")).ShouldBe(2);
        errors.ShouldBeEmpty();
    }

    // INV-22: in the browser the IHttpClientFactory handler scope resolves the app's one store, so the action the handler
    // dispatches reaches the store the page renders.
    [Fact]
    public async Task Wasm_DelegatingHandlerDispatch_ReachesUiStore()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await page.GotoAsync(new Uri(host.BaseUrl, "/handler").ToString());
        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("0", new() { Timeout = 30_000 });

        await page.GetByTestId("ping").ClickAsync();
        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("1");
        await page.GetByTestId("ping").ClickAsync();

        await Assertions.Expect(page.GetByTestId("api-calls")).ToHaveTextAsync("2");
        errors.ShouldBeEmpty();
    }

    // S-7 twin of the bUnit test (INV-17, §11.6): on /wasm-user the WASM store scopes its browser keys by NameIdentifier
    // from a renderer-scope AuthenticationStateProvider. A user change moves the scoped keys: the old key keeps its final
    // value, the new scope reads its own key, and later writes land under it; switching back restores the first user's.
    [Fact]
    public async Task Wasm_UserChange_ScopedBrowserKeysMoveToNewScope()
    {
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await page.GotoAsync(new Uri(host.BaseUrl, "/wasm-user").ToString());
        var increment = page.GetByTestId("increment");
        await Assertions.Expect(increment).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(page.GetByTestId("user")).ToHaveTextAsync("alice");
        await Assertions.Expect(page.GetByTestId("status")).ToHaveTextAsync("Hydrated");

        await increment.ClickAsync();
        await increment.ClickAsync();
        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("2");
        await WaitForCounterAsync(page, "alice", 2);

        await page.GetByTestId("sign-in-bob").ClickAsync();
        await Assertions.Expect(page.GetByTestId("user")).ToHaveTextAsync("bob");
        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("0");
        await Assertions.Expect(page.GetByTestId("status")).ToHaveTextAsync("Hydrated");
        await increment.ClickAsync();
        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("1");
        await WaitForCounterAsync(page, "bob", 1);

        await page.GetByTestId("sign-in-alice").ClickAsync();
        await Assertions.Expect(page.GetByTestId("count")).ToHaveTextAsync("2");
        await Assertions.Expect(page.GetByTestId("status")).ToHaveTextAsync("Hydrated");

        (await page.EvaluateAsync<string[]>("() => Object.keys(localStorage).filter(k => k.endsWith(':counter')).sort()"))
            .ShouldBe(["ducky-samples:alice:counter", "ducky-samples:bob:counter"]);
        (await page.EvaluateAsync<string>("() => localStorage.getItem('ducky-samples:alice:counter')")).ShouldContain("\"value\":2");
        (await page.EvaluateAsync<string>("() => localStorage.getItem('ducky-samples:bob:counter')")).ShouldContain("\"value\":1");
        errors.ShouldBeEmpty();
    }

    // INV-28, §11.8: a fake extension connected by both stores of /auto receives the sample's options and only sanitized
    // payloads: the draft/changed action payload and the draft slice are redacted in @@INIT and in every send of both
    // stores (the snapshot waits until each has sent the counter at 1, after the secret draft), while other slices pass
    // through, and the text typed never reaches the extension.
    [Fact]
    public async Task DevTools_FakeExtension_ReceivesSanitizedPayloads()
    {
        const string Secret = "top secret";
        await using var host = await PublishedHost.StartSampleAsync(Sample, "/health");
        await using var harness = await Harness.StartAsync();
        var page = await harness.NewPageAsync();
        var errors = ServerSampleTests.CollectErrors(page);
        await page.AddInitScriptAsync("""
            window.devtools = [];
            window.__REDUX_DEVTOOLS_EXTENSION__ = {
                connect(options) {
                    const connection = { options, messages: [] };
                    devtools.push(connection);
                    return {
                        init: state => connection.messages.push({ kind: 'init', state }),
                        send: (action, state) => connection.messages.push({ kind: 'send', action, state }),
                        subscribe: () => {},
                        unsubscribe: () => {},
                    };
                },
            };
            """);
        await OpenAutoAsync(page, host);
        await page.WaitForFunctionAsync("() => devtools.length === 2 && devtools.every(c => c.messages.some(m => m.kind === 'init'))",
            null, new() { PollingInterval = 50, Timeout = 30_000 });

        await ChangeDraftAsync(page, "server", Secret);
        await Assertions.Expect(page.GetByTestId("draft-value-wasm")).ToHaveTextAsync(Secret);
        await page.GetByTestId("increment-server").ClickAsync();
        await page.WaitForFunctionAsync("""
            () => devtools.some(c => c.messages.some(m => m.kind === 'send' && m.action.type === 'counter/increment'))
                && devtools.every(c => c.messages.some(m => m.kind === 'send' && m.state.counter && m.state.counter.value === 1))
            """, null, new() { PollingInterval = 50 });

        var json = await page.EvaluateAsync<string>("() => JSON.stringify(devtools)");
        json.ShouldNotContain(Secret);
        var connections = JsonDocument.Parse(json).RootElement.EnumerateArray().ToList();
        connections.Count.ShouldBe(2);
        var messages = connections.SelectMany(c => c.GetProperty("messages").EnumerateArray()).ToList();
        connections.ShouldAllBe(c => c.GetProperty("options").GetProperty("name").GetString() == "Ducky.Samples.Server");
        connections.ShouldAllBe(c => c.GetProperty("messages").EnumerateArray().Any(m => m.GetProperty("kind").GetString() == "send"));
        var inits = messages.Where(m => m.GetProperty("kind").GetString() == "init").ToList();
        inits.Count.ShouldBe(2);
        var sends = messages.Where(m => m.GetProperty("kind").GetString() == "send").ToList();
        inits.Concat(sends).ShouldAllBe(m => m.GetProperty("state").GetProperty("draft").GetString() == "<redacted>");
        var changed = sends.Single(m => m.GetProperty("action").GetProperty("type").GetString() == "draft/changed");
        changed.GetProperty("action").GetProperty("payload").GetString().ShouldBe("<redacted>");
        var increment = sends.Single(m => m.GetProperty("action").GetProperty("type").GetString() == "counter/increment");
        increment.GetProperty("state").GetProperty("counter").GetProperty("value").GetInt32().ShouldBe(1);
        errors.ShouldBeEmpty();
    }

    private static async Task OpenAutoAsync(IPage page, PublishedHost host)
    {
        await page.GotoAsync(new Uri(host.BaseUrl, "/auto").ToString());
        await Assertions.Expect(page.GetByTestId("increment-server")).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(page.GetByTestId("increment-wasm")).ToBeEnabledAsync(_interactive);
        await Assertions.Expect(page.GetByTestId("renderer-wasm")).ToHaveTextAsync("WebAssembly");
    }

    // The input dispatches on change, which fires on blur.
    private static async Task ChangeDraftAsync(IPage page, string store, string text)
    {
        var input = page.GetByTestId($"draft-{store}");
        await input.FillAsync(text);
        await input.BlurAsync();
    }

    private static async Task WaitForCounterAsync(IPage page, string user, int value) =>
        await page.WaitForFunctionAsync(
            $$"""() => (localStorage.getItem('ducky-samples:{{user}}:counter') ?? '').includes('"value":{{value}}')""",
            null, new() { PollingInterval = 50 });
}

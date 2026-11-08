using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.6 in the browser: the subscription is made inside the renderer hand-off callback, on the renderer's provider.
public sealed partial class ScopeSwitchWasmTests : BunitContext
{
    private readonly FakeJsRuntime _js = new();
    private readonly Dictionary<string, string> _storage = [];
    private readonly List<(string Key, string Value)> _writes = [];
    private readonly StoreScopeSeen _seen = new();

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task Wasm_UserChange_ScopedBrowserKeysMoveToNewScope()
    {
        // A browser Scope app with one provider per scope, as a real WASM app has for MSAL: before the hand-off no subscription
        // exists, the scope is resolved through the renderer's provider, and only that provider's user change moves the scoped
        // keys: the old key keeps its final value, the new key's value restores and renders, and later writes land under the
        // new key.
        var (store, init, rendererAuth, storeAuth) = StartPerScopeProviders();

        // No hand-off yet: nothing hears this, and the hand-off reads the provider's current user.
        rendererAuth.Change("alice");
        store.State.Get<PersistenceState>().ScopeEpoch.ShouldBe(0);

        var cut = Render<CounterView>();
        await init;
        await Persistence.WriterHarness.Until(() => cut.Markup == "4");

        await cut.InvokeAsync(() => store.Dispatch(new Increment()));
        await Persistence.WriterHarness.Until(() => Payloads("ducky:alice:counter").Contains(5));

        // The store scope's provider never hears MSAL in a real app: raising it moves nothing.
        storeAuth.Change("carol");
        store.State.Get<PersistenceState>().ScopeEpoch.ShouldBe(0);

        rendererAuth.Change("bob");
        await Persistence.WriterHarness.Until(() => cut.Markup == "6");
        await Persistence.WriterHarness.Until(() => store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });

        await cut.InvokeAsync(() => store.Dispatch(new Increment()));
        await Persistence.WriterHarness.Until(() => Payloads("ducky:bob:counter").Contains(7));

        Payloads("ducky:alice:counter").ShouldBe([5]);
        Writes().Select(static w => w.Key).ShouldBe(["ducky:alice:counter", "ducky:bob:counter"]);
        StorageReads().ShouldBe(["ducky:alice:counter", "ducky:bob:counter"]);
    }

    [Fact]
    public async Task Wasm_SameRendererUserReasserted_CheckedOnRendererServices()
    {
        // (non-normative) The re-assertion check resolves the scope through the renderer's services, as the switch does: with
        // the store scope's provider on another user, the renderer's same user re-notified is absorbed (no epoch, no read, one
        // Debug log), and its real change still switches.
        var (store, init, rendererAuth, storeAuth) = StartPerScopeProviders();
        var cut = Render<CounterView>();
        await init;
        await Persistence.WriterHarness.Until(() => cut.Markup == "4");

        storeAuth.Change("carol");
        rendererAuth.Change("alice");

        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        _logs.GetSnapshot().Where(static r => r.Id.Id == 2045).ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
        StorageReads().ShouldBe(["ducky:alice:counter"]);

        rendererAuth.Change("bob");
        await Persistence.WriterHarness.Until(() => store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await Persistence.WriterHarness.Until(() => cut.Markup == "6");
        StorageReads().ShouldBe(["ducky:alice:counter", "ducky:bob:counter"]);
    }

    [Fact]
    public async Task Wasm_HandOffAfterDispose_UserChangeNeverRunsScope()
    {
        // (non-normative) §11.6: a hand-off after dispose still subscribes (its callback is never withdrawn), but the user
        // change it hears takes no epoch and never calls the Scope delegate: dispose disposed the store-lifetime token the
        // re-assertion check would pass it.
        var auth = new SwitchingAuth("alice");
        var scopeCalls = 0;
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        Services.RemoveAll<IJSRuntime>();
        Services.AddSingleton<IJSRuntime>(_js);
        _js.Respond = Respond;
        Func<IServiceProvider, CancellationToken, ValueTask<string?>> scope = (services, token) =>
        {
            scopeCalls++;
            return DuckyScopes.NameIdentifier(services, token);
        };
        Services.AddDucky(d => d.UseJson(Persistence.HydrationJson.Default).AddSlice<CounterSlice>()
            .AddBlazor(o => (o.IsBrowser, o.Scope) = (true, scope))
            .Persist<CounterSlice>());
        var store = Services.GetRequiredService<IStore>();
        var init = store.InitializeAsync(Ct);
        await store.DisposeAsync();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        scopeCalls.ShouldBe(0); // the epoch-0 resolution waits for the hand-off, which never came before dispose

        Services.GetRequiredService<PersistenceSlice>().Gate.Register(isInteractive: true, Services);
        auth.Subscribers.ShouldBe(1);
        auth.Change("bob");

        scopeCalls.ShouldBe(0);
        store.State.Get<PersistenceState>().ScopeEpoch.ShouldBe(0);
    }

    // A browser Scope app with one provider per scope, as a real WASM app has for MSAL: a Singleton store, whose scope is not
    // the renderer's, starts its init before any hand-off. Alice's key holds 4 and Bob's 6.
    private (IStore Store, Task Init, SwitchingAuth Renderer, SwitchingAuth StoreScope) StartPerScopeProviders()
    {
        Services.AddScoped(static _ => new SwitchingAuth("alice"));
        Services.AddScoped<AuthenticationStateProvider>(static sp => sp.GetRequiredService<SwitchingAuth>());
        Services.AddSingleton(_seen);
        Services.RemoveAll<IJSRuntime>();
        Services.AddSingleton<IJSRuntime>(_js);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        _js.Respond = Respond;
        Services.AddDucky(static d =>
        {
            d.Lifetime = ServiceLifetime.Singleton;
            d.UseJson(Persistence.HydrationJson.Default).AddSlice<CounterSlice>()
                .AddBlazor(static o => (o.IsBrowser, o.Scope) = (true, DuckyScopes.NameIdentifier)).Use<StoreScopeProbe>()
                .Persist<CounterSlice>(static o => o.Debounce = TimeSpan.Zero);
        });
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
        _storage["ducky:alice:counter"] = Envelope(4);
        _storage["ducky:bob:counter"] = Envelope(6);
        var store = Services.GetRequiredService<IStore>();
        var init = store.InitializeAsync(Ct);
        var rendererAuth = Services.GetRequiredService<SwitchingAuth>();
        var storeAuth = _seen.StoreServices.ShouldNotBeNull().GetRequiredService<SwitchingAuth>();
        storeAuth.ShouldNotBeSameAs(rendererAuth);
        return (store, init, rendererAuth, storeAuth);
    }

    private IEnumerable<object?> StorageReads() => _js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]);

    private static string Envelope(int value) => EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    private object? Respond(string identifier, object?[] args)
    {
        switch (identifier)
        {
            case "import":
                return _js;
            case "storageGet":
                lock (_writes)
                {
                    return _storage.GetValueOrDefault((string)args[1]!);
                }

            case "storageSet":
                lock (_writes)
                {
                    _writes.Add(((string)args[1]!, (string)args[2]!));
                    _storage[(string)args[1]!] = (string)args[2]!;
                }

                return true;
            default:
                return null;
        }
    }

    // The writers' loops call Respond on pool threads: _writes and _storage are only touched under the lock.
    private List<(string Key, string Value)> Writes()
    {
        lock (_writes)
        {
            return [.. _writes];
        }
    }

    private List<int> Payloads(string key) =>
        [.. Writes().Where(w => w.Key == key).Select(static w => System.Text.Json.JsonDocument.Parse(w.Value).RootElement.GetProperty("s").GetProperty("Value").GetInt32())];
}

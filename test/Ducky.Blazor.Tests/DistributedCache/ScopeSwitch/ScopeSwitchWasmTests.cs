using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.6 in the browser: the subscription is made inside the renderer hand-off callback, on the renderer's provider.
public sealed class ScopeSwitchWasmTests : BunitContext
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
        Services.AddScoped(static _ => new SwitchingAuth("alice"));
        Services.AddScoped<AuthenticationStateProvider>(static sp => sp.GetRequiredService<SwitchingAuth>());
        Services.AddSingleton(_seen);
        Services.RemoveAll<IJSRuntime>();
        Services.AddSingleton<IJSRuntime>(_js);
        _js.Respond = Respond;
        Services.AddDucky(static d =>
        {
            d.Lifetime = ServiceLifetime.Singleton; // as in a WASM app: the store's scope is not the renderer's
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
        _writes.Select(static w => w.Key).ShouldBe(["ducky:alice:counter", "ducky:bob:counter"]);
        _js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["ducky:alice:counter", "ducky:bob:counter"]);
    }

    private static string Envelope(int value) => EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    private object? Respond(string identifier, object?[] args)
    {
        switch (identifier)
        {
            case "import":
                return _js;
            case "storageGet":
                return _storage.GetValueOrDefault((string)args[1]!);
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

    private List<int> Payloads(string key)
    {
        lock (_writes)
        {
            return [.. _writes.Where(w => w.Key == key).Select(static w => System.Text.Json.JsonDocument.Parse(w.Value).RootElement.GetProperty("s").GetProperty("Value").GetInt32())];
        }
    }
}

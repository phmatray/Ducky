using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.6 in the browser: inert Server-storage keys put a scope in use only with a resolvable AuthenticationStateProvider;
// a switch never reads them (there is no IDistributedCache) but its reset restores them.
public sealed partial class ScopeSwitchWasmTests
{
    private readonly FakeLogCollector _logs = new();

    [Fact]
    public async Task Wasm_UserChange_InertServerSlicesResetNotRead()
    {
        // An unscoped Local slice next to an inert Server slice the previous user's prerender seeded, and a renderer provider:
        // the user change resets the Server slice to its initial state, so the seeded value is not kept, and reads nothing: the
        // Local slice is neither reset nor read again, and no key is written.
        var seed = await PrerenderAsync(5);
        var auth = new SwitchingAuth("alice");
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        var store = Start(static d => d.Prerender<CounterSlice>().Persist<TallySlice>(static o => o.Debounce = TimeSpan.Zero));
        _storage["app:tally"] = Envelope(7);
        await Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);
        var init = store.InitializeAsync(Ct);
        var cut = Render<CounterView>();
        await init;
        await WriterHarness.Until(() => cut.Markup == "5");
        store.State.Get<Tally>().ShouldBe(new Tally(7));
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        auth.Subscribers.ShouldBe(1);

        auth.Change("bob");

        await WriterHarness.Until(() => store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => cut.Markup == "0");
        store.State.Get<Tally>().ShouldBe(new Tally(7));
        StorageReads().ShouldBe(["app:tally"]);
        Writes().ShouldBeEmpty();
    }

    [Fact]
    public async Task Wasm_ServerStorageOnly_NoAuthProvider_NoSubscriptionNoThrow()
    {
        // The Interactive Auto WASM side: its only scoped slice is Server storage, and it registers no provider. The hand-off
        // resolves none (GetService, never GetRequiredService, inside the component's Select): no subscription, no throw,
        // nothing logged as failed, and Status stays Hydrated.
        Services.RemoveAll<AuthenticationStateProvider>(); // bUnit registers a placeholder
        var store = Start(static _ => { });
        var init = store.InitializeAsync(Ct);
        var cut = Render<CounterView>();
        await init;
        await cut.InvokeAsync(() => store.Dispatch(new Increment()));

        await WriterHarness.Until(() => cut.Markup == "1");
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        Services.GetService<AuthenticationStateProvider>().ShouldBeNull();
        _js.Calls.ShouldNotContain(static c => c.Identifier == "storageGet");
        _logs.GetSnapshot().ShouldNotContain(static r => r.Exception != null || r.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task Wasm_ServerStorageOnlyWithProvider_UserChangeResetsInertSlice()
    {
        // (non-normative) The same store with a provider: the inert slice puts a scope in use, so the hand-off subscribes. Init
        // hydrates nothing (Status stays Hydrated); a user change takes an epoch, resets the slice, reads nothing and ends.
        var auth = new SwitchingAuth("alice");
        Services.AddSingleton<AuthenticationStateProvider>(auth);
        var store = Start(static _ => { });
        await store.InitializeAsync(Ct);
        store.State.Get<PersistenceState>().ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        auth.Subscribers.ShouldBe(0);
        var cut = Render<CounterView>();
        auth.Subscribers.ShouldBe(1);
        await cut.InvokeAsync(() => store.Dispatch(new Increment()));
        await WriterHarness.Until(() => cut.Markup == "1");

        auth.Change("bob");

        await WriterHarness.Until(() => store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => cut.Markup == "0");
        _js.Calls.ShouldNotContain(static c => c.Identifier == "storageGet");
        Writes().ShouldBeEmpty();
    }

    // Alice's prerender pass on the Interactive Auto server side, where the slice is real Server storage: a static render whose
    // counter reached `count` (written through to the cache), then the framework persists the page.
    private static async Task<FakeComponentStateStore> PrerenderAsync(int count)
    {
        using var cache = new TestCache(yield: false);
        await using var side = new BunitContext();
        side.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        side.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        side.Services.AddSingleton<IDistributedCache>(cache).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice"));
        side.Services.AddDucky(static d => d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddBlazor(static o => o.KeyPrefix = "app")
            .Prerender<CounterSlice>().Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server));
        side.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        side.Render<CounterView>();
        var store = side.Services.GetRequiredService<IStore>();
        await store.InitializeAsync(Ct);
        for (var i = 0; i < count; i++)
        {
            store.Dispatch(new Increment());
        }

        var persisted = new FakeComponentStateStore();
        await side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, side.Renderer);
        return persisted;
    }

    // A WASM store (singleton, browser, Interactive renderer) persisting the Counter slice as inert Server storage.
    private IStore Start(Action<DuckyBuilder> more)
    {
        Services.RemoveAll<IJSRuntime>();
        Services.AddSingleton<IJSRuntime>(_js);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        _js.Respond = Respond;
        Services.AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Singleton;
            d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddSlice<TallySlice>()
                .AddBlazor(static o => (o.IsBrowser, o.KeyPrefix) = (true, "app"))
                .Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server);
            more(d);
        });
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
        return Services.GetRequiredService<IStore>();
    }
}

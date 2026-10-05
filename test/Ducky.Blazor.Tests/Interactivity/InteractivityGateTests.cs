using System.Text.Json;
using Bunit;
using Ducky.Blazor.Tests.Components;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Interactivity;

// SPEC §11.4 (InteractivityGate and its probe), §11.2 (the first-toucher hand-off), §6.10 (the S-7 fallback); INV-15.
public sealed class InteractivityGateTests : BunitContext
{
    private static readonly RendererInfo _wasm = new("WebAssembly", isInteractive: true);
    private readonly StoreScopeSeen _seen = new();

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    private InteractivityGate Gate => Services.GetRequiredService<PersistenceSlice>().Gate;

    [Fact]
    public void Gate_Unknown_ProbeDetectsPrerender()
    {
        // A store touched first by non-component code: the first built-in that needs to know probes once, synchronously,
        // through JsBridge, without awaiting the import.
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddBlazor());
        _ = Store.State;

        // No Interactive Server (UnsupportedJavaScriptRuntime): the import throws synchronously.
        var gate = Gate;
        var js = new FakeJsRuntime { Respond = static (_, _) => throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.") };
        gate.Resolve(isBrowser: false, Bridge(js)).ShouldBe(InteractivityMode.NonInteractive);
        js.Calls.Count.ShouldBe(1); // probed once: the outcome is kept

        // RemoteJSRuntime during prerender: the returned task is already faulted with an InvalidOperationException.
        Probe(static (_, _) => Task.FromException<object?>(new InvalidOperationException("prerendering"))).ShouldBe(InteractivityMode.NonInteractive);

        // A live runtime: the import is pending, kept as the module task, and not imported again.
        var pending = new TaskCompletionSource<object?>();
        var liveJs = new FakeJsRuntime { Respond = (_, _) => pending.Task };
        var bridge = Bridge(liveJs);
        new InteractivityGate().Resolve(isBrowser: false, bridge).ShouldBe(InteractivityMode.Interactive);
        _ = bridge.ImportAsync();
        liveJs.Calls.Count.ShouldBe(1);

        // Any other outcome is interactive too, but a faulted import is not kept: the next use imports again.
        var failedJs = new FakeJsRuntime { Respond = static (_, _) => Task.FromException<object?>(new JSException("network")) };
        var failedBridge = Bridge(failedJs);
        new InteractivityGate().Resolve(isBrowser: false, failedBridge).ShouldBe(InteractivityMode.Interactive);
        _ = failedBridge.ImportAsync();
        failedJs.Calls.Count.ShouldBe(2);

        // In the browser, and once a component recorded its renderer, nothing is probed.
        var browserJs = new FakeJsRuntime();
        new InteractivityGate().Resolve(isBrowser: true, Bridge(browserJs)).ShouldBe(InteractivityMode.Interactive);
        var registered = new InteractivityGate();
        registered.Register(isInteractive: false, Services);
        registered.Resolve(isBrowser: false, Bridge(browserJs)).ShouldBe(InteractivityMode.NonInteractive);
        browserJs.Calls.ShouldBeEmpty();

        // A registration after the probe keeps the probed outcome.
        gate.Register(isInteractive: true, Services);
        gate.Resolve(isBrowser: false, Bridge(js)).ShouldBe(InteractivityMode.NonInteractive);
    }

    [Fact]
    public async Task Gate_Wasm_ComponentRegistrationVisibleToMiddleware()
    {
        UseWasmStore();

        // A Program.cs preload: the store-scope middleware exists before any component and waits for its registration.
        await Store.InitializeAsync(Ct);
        _seen.Registered.ShouldBeNull();

        Renderer.SetRendererInfo(_wasm);
        Render<CounterView>().Markup.ShouldBe("0");

        // One gate per store, whatever the scope: the store-scope middleware saw the renderer's registration.
        // The renderer's scope, not the middleware's own store scope (the scoped-fake tests check the identity).
        _seen.Registered.ShouldNotBeNull().ShouldNotBeSameAs(_seen.StoreServices);
        _seen.Gate.ShouldBeSameAs(Gate);
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<PersistenceSlice>().Gate.ShouldBeSameAs(Gate);
        _seen.StoreServices.ShouldNotBeNull().GetRequiredService<PersistenceSlice>().Gate.ShouldBeSameAs(Gate);
        var js = new FakeJsRuntime();
        Gate.Resolve(isBrowser: false, Bridge(js)).ShouldBe(InteractivityMode.Interactive); // recorded: nothing probed
        js.Calls.ShouldBeEmpty();
        Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
    }

    [Fact]
    public async Task Gate_Wasm_HandsOverPersistentStateAndJsRuntime()
    {
        UseScopedRendererServices();
        UseWasmStore();
        await Store.InitializeAsync(Ct);
        var storeServices = _seen.StoreServices.ShouldNotBeNull();
        var (storeJs, rendererJs) = (storeServices.GetRequiredService<FakeJsRuntime>(), Services.GetRequiredService<FakeJsRuntime>());
        var (storeManager, rendererManager) = (storeServices.GetRequiredService<ComponentStatePersistenceManager>(), Services.GetRequiredService<ComponentStatePersistenceManager>());
        rendererJs.ShouldNotBeSameAs(storeJs);
        await storeManager.RestoreStateAsync(Seeded("store"));
        var rendererStore = Seeded("renderer");
        await rendererManager.RestoreStateAsync(rendererStore);
        var storeState = storeServices.GetRequiredService<PersistentComponentState>();
        await using var bridge = Gate.CreateBridge(storeServices.GetRequiredService<IJSRuntime>(), NullLogger.Instance);

        // Before any component registered: the store scope's instances.
        (await bridge.TryInvokeAsync<bool>("storageRemove", Ct, "local", "k", 1)).Delivered.ShouldBeTrue();
        storeJs.Calls.Select(static call => call.Identifier).ShouldBe(["import", "storageRemove"]);
        Take(Gate.PersistentStateOr(storeState)).ShouldBe("store");
        PersistSeed(Gate.PersistentStateOr(storeState), "before");

        Renderer.SetRendererInfo(_wasm);
        Render<CounterView>();

        // Once one has: the renderer's, for the import and the calls, and for the seed taken and persisted.
        (await bridge.TryInvokeAsync<bool>("storageRemove", Ct, "local", "k", 1)).Delivered.ShouldBeTrue();
        rendererJs.Calls.Select(static call => call.Identifier).ShouldBe(["import", "storageRemove"]);
        storeJs.Calls.Count.ShouldBe(2);
        var state = Gate.PersistentStateOr(storeState);
        Take(state).ShouldBe("renderer");
        PersistSeed(state, "next");
        await rendererManager.PersistStateAsync(rendererStore, Renderer);
        JsonSerializer.Deserialize<string>(rendererStore.State["ducky:seed"]).ShouldBe("next");

        // The store scope persists only what was registered on it before the registration, never the renderer path's seed.
        var storeStore = new FakeComponentStateStore();
        await storeManager.PersistStateAsync(storeStore, Renderer);
        JsonSerializer.Deserialize<string>(storeStore.State["ducky:seed"]).ShouldBe("before");
    }

    [Fact]
    public void Gate_FirstRegistrationCallbackThrows_OthersStillRunOnce()
    {
        // (non-normative) Callbacks must not throw; if a built-in's does, the others still run, once, and the registering
        // toucher sees the failure.
        var gate = new InteractivityGate();
        var ran = 0;
        gate.OnFirstRegistration(static () => throw new InvalidOperationException("broken built-in"));
        gate.OnFirstRegistration(() => ran++);

        Should.Throw<AggregateException>(() => gate.Register(isInteractive: true, Services))
            .InnerExceptions.ShouldHaveSingleItem().Message.ShouldBe("broken built-in");
        gate.Register(isInteractive: true, Services);

        ran.ShouldBe(1);
    }

    [Fact]
    public void Gate_HandOverResolveThrows_NextToucherRegisters()
    {
        // (non-normative) A toucher whose services can't be resolved leaves the gate unregistered: the next one registers.
        var gate = new InteractivityGate();
        IServiceProvider? seen = null;
        gate.OnFirstRegistration(() => seen = gate.Services);

        Should.Throw<InvalidOperationException>(() => gate.Register(isInteractive: false, new ThrowingProvider()));
        seen.ShouldBeNull();
        gate.Register(isInteractive: true, Services);

        seen.ShouldBeSameAs(Services);
        gate.Resolve(isBrowser: false, Bridge(new FakeJsRuntime())).ShouldBe(InteractivityMode.Interactive);
    }

    [Fact]
    public async Task Component_WithoutAddBlazor_SelectsAndRerenders()
    {
        // No gate to inform: the component never reads its RendererInfo (bUnit would throw, none is set) and never asks the
        // container for an internal type.
        Services.AddDucky(d => d.AddSlice<CounterSlice>());
        var cut = Render<CounterView>();
        cut.Markup.ShouldBe("0");

        Store.Dispatch(new Increment());
        await cut.InvokeAsync(static () => { });

        cut.Markup.ShouldBe("1");
        Services.GetService<PersistenceSlice>().ShouldBeNull();
    }

    [Fact]
    public async Task StoreSelectionExtensions_Wasm_HandsOverScopedServices()
    {
        // A foreign-base component hands over the same scope as a Ducky component, from its rendererInfo and services.
        UseScopedRendererServices();
        UseWasmStore();
        Renderer.SetRendererInfo(_wasm);

        Render<ForeignCounter>().Markup.ShouldBe("0");

        var rendererJs = Services.GetRequiredService<FakeJsRuntime>();
        Gate.Services.ShouldNotBeNull().GetRequiredService<FakeJsRuntime>().ShouldBeSameAs(rendererJs);
        Gate.PersistentStateOr(null!).ShouldBeSameAs(Services.GetRequiredService<PersistentComponentState>());
        await using var bridge = Gate.CreateBridge(null!, NullLogger.Instance);
        _ = bridge.ImportAsync();
        rendererJs.Calls.ShouldHaveSingleItem().Identifier.ShouldBe("import");
        Gate.Resolve(isBrowser: false, Bridge(new FakeJsRuntime())).ShouldBe(InteractivityMode.Interactive);
    }

    [Fact]
    public void Gate_OnFirstRegistration_RunsSynchronouslyInsideSelect()
    {
        // (non-normative) The callback runs on the registering thread, inside the component's Select: after the hand-off,
        // before the store is touched (materialized) and before the component renders; once.
        Services.AddSingleton(_seen);
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddBlazor().Use<StoreScopeProbe>());
        Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var gate = Gate;
        var thread = -1;
        gate.OnFirstRegistration(() =>
        {
            thread = Environment.CurrentManagedThreadId;
            _seen.Order.Add($"callback {gate.Services is not null}");
        });

        Render<CounterView>(p => p.Add(c => c.Order, _seen.Order));
        Render<CounterView>();

        _seen.Order.ShouldBe(["callback True", "materialized", "render"]);
        thread.ShouldBe(Environment.CurrentManagedThreadId);

        // A callback registered after the first registration runs at once, on the registering thread.
        var late = -1;
        gate.OnFirstRegistration(() => late = Environment.CurrentManagedThreadId);
        late.ShouldBe(Environment.CurrentManagedThreadId);
    }

    [Fact]
    public async Task DuckyInitializer_HandsOverBeforeInit()
    {
        // (non-normative) DuckyInitializer records its renderer and hands over its scope before it calls InitializeAsync.
        Services.AddSingleton(_seen);
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddBlazor().Use<StoreScopeProbe>());
        Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        Gate.OnFirstRegistration(() => _seen.Order.Add("registered"));

        var cut = Render<DuckyInitializer>(p => p.Add(c => c.ChildContent, "ready"));
        await Store.InitializeAsync(Ct);
        await cut.InvokeAsync(static () => { });

        cut.Markup.ShouldBe("ready");
        _seen.Order.ShouldBe(["registered", "materialized"]);
        Gate.Resolve(isBrowser: false, Bridge(new FakeJsRuntime())).ShouldBe(InteractivityMode.NonInteractive);
    }

    private static JsBridge Bridge(FakeJsRuntime js) => new(js, NullLogger.Instance);

    private static InteractivityMode Probe(Func<string, object?[], object?> respond) =>
        new InteractivityGate().Resolve(isBrowser: false, Bridge(new FakeJsRuntime { Respond = respond }));

    private static FakeComponentStateStore Seeded(string seed)
    {
        var store = new FakeComponentStateStore();
        store.State["ducky:seed"] = JsonSerializer.SerializeToUtf8Bytes(seed);
        return store;
    }

    private static void PersistSeed(PersistentComponentState state, string seed) =>
        state.RegisterOnPersisting(
            () =>
            {
                state.PersistAsJson("ducky:seed", seed);
                return Task.CompletedTask;
            },
            RenderMode.InteractiveAuto);

    private static string? Take(PersistentComponentState state) =>
        state.TryTakeFromJson<string>("ducky:seed", out var seed) ? seed : null;

    // A WASM store (one per app, its own store scope) with a store-scope party watching the gate.
    private void UseWasmStore()
    {
        Services.AddSingleton(_seen);
        Services.AddDucky(d =>
        {
            d.Lifetime = ServiceLifetime.Singleton;
            d.AddSlice<CounterSlice>().AddBlazor(static o => o.IsBrowser = true).Use<StoreScopeProbe>();
        });
    }

    // A framework where PersistentComponentState and IJSRuntime are scoped: the renderer scope's instances differ from
    // the store scope's, which only the hand-off can route to.
    private void UseScopedRendererServices()
    {
        Services.AddScoped<FakeJsRuntime>();
        Services.AddScoped<IJSRuntime>(static sp => sp.GetRequiredService<FakeJsRuntime>());
        Services.AddScoped(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        Services.AddScoped(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
    }

    private sealed class ThrowingProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => throw new InvalidOperationException("disposed provider");
    }
}

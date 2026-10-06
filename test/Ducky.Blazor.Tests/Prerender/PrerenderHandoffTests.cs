using System.Text;
using System.Text.Json;
using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Blazor.Tests.Prerender;

// SPEC §11.4 (the prerender handoff outside the browser: seed take, SeedSettled, the Interactive Auto persisting
// registration, the browser's bounded wait for the seed); INV-15. Both sides drive a real ComponentStatePersistenceManager over a FakeComponentStateStore (§17.2):
// this context is the interactive side, a second BunitContext the prerender pass or the paused circuit.
public sealed partial class PrerenderHandoffTests : BunitContext
{
    private const string SeedKey = "ducky:seed";
    private static readonly RendererInfo _static = new("Static", isInteractive: false);
    private static readonly RendererInfo _server = new("Server", isInteractive: true);
    private static readonly RendererInfo _wasm = new("WebAssembly", isInteractive: true);
    private readonly Loads _loads = new();
    private readonly FakeTimeProvider _time = new();
    private readonly FakeLogCollector _logs = new();

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    private bool SeedSettled => Services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.IsCompletedSuccessfully;

    [Fact]
    public async Task PrerenderSeed_AppliedBeforeFirstInteractiveRender()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 3));
        await InteractiveAsync(seed, static d => d.Prerender<CounterSlice>());

        // The component's Select starts init, whose synchronous prefix restores the seed before the first render.
        List<string> order = [];
        var cut = Render<CounterView>(p => p.Add(c => c.Order, order));

        cut.Markup.ShouldBe("3");
        order.ShouldBe(["render"]);
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        SeedSettled.ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_InitStartedByDuckyInitializer_SeedAppliedBeforeFirstRender()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 2));
        await InteractiveAsync(seed, static d => d.Prerender<CounterSlice>());

        // DuckyInitializer is the first toucher: it hands over and starts init, and its content's first render is seeded.
        List<string> order = [];
        var cut = Render<DuckyInitializer>(p => p.AddChildContent<CounterView>(c => c.Add(v => v.Order, order)));

        cut.Markup.ShouldBe("2");
        order.ShouldBe(["render"]);
    }

    [Fact]
    public async Task Circuit_Resume_SeedRestoresPrerenderSlices()
    {
        // A live circuit changes both slices, then .NET 10 pauses it: the same persisting registration writes the seed.
        var seed = await PersistAsync(_server, static d => d.AddSlice<ProductsSlice>().Prerender<CounterSlice>(), static store =>
        {
            Increment(store, 4);
            store.Dispatch(new ProductsLoaded(7));
        });

        // A pause seed. The resumed circuit is a fresh store: the Prerender<T> slice comes back from the seed, the other
        // one resets.
        Envelope(seed).GetProperty("src").GetString().ShouldBe("pause");
        await InteractiveAsync(seed, static d => d.AddSlice<ProductsSlice>().Prerender<CounterSlice>());
        Render<CounterView>().Markup.ShouldBe("4");
        Store.State.Get<Products>().ShouldBe(new Products(0, null));
        Store.State.WasRestored<Products>().ShouldBeFalse();
    }

    [Fact]
    public async Task Prerender_RestoredSlice_LoadEffectRunsOnceAcrossHandoff()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<ProductsSlice>().AddEffect<LoadProducts>());
        _loads.Count.ShouldBe(1);

        await InteractiveAsync(seed, static d => d.Prerender<ProductsSlice>().AddEffect<LoadProducts>());
        Render<CounterView>();
        await Store.WhenIdleAsync(Ct);

        // The interactive store took the prerendered value and its load rule saw the slice restored: no second load.
        _loads.Count.ShouldBe(1);
        Store.State.Get<Products>().ShouldBe(new Products(10, null));
        Store.State.WasRestored<Products>().ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_OnPersistingRegisteredWithInteractiveAutoMode()
    {
        // The fake store accepts only Interactive Auto: the seed is persisted iff the registration's mode is Interactive Auto.
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 1));

        seed.RenderModes.ShouldBe([RenderMode.InteractiveAuto]);
        seed.State.Keys.ShouldBe([SeedKey]);
        var envelope = JsonDocument.Parse(JsonSerializer.Deserialize<byte[]>(seed.State[SeedKey])).RootElement;
        envelope.GetProperty("v").GetInt32().ShouldBe(1);
        envelope.GetProperty("src").GetString().ShouldBe("prerender");
        envelope.GetProperty("slices").GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Prerender_NoPrerenderSlices_Inert()
    {
        // (non-normative) Without Prerender<T> slices nothing is registered, nothing is taken, and SeedSettled is complete
        // from construction.
        var seed = await PrerenderAsync(static _ => { });
        seed.RenderModes.ShouldBeEmpty();
        seed.State.ShouldBeEmpty();

        await InteractiveAsync(Seeded(Encoding.UTF8.GetBytes("""{"v":1,"slices":{}}""")), static _ => { });
        Render<CounterView>();
        SeedSettled.ShouldBeTrue();
        Services.GetRequiredService<PersistentComponentState>().TryTakeFromJson<byte[]>(SeedKey, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_IncludeFalseOrUnserializable_SliceLeftOutOfSeed()
    {
        // (non-normative) A slice whose include predicate is false, or whose state can't be serialized, is left out; the
        // others, a slice whose predicate holds included, are still seeded, and the left-out slice loads again on the
        // interactive side. The predicate logs Debug 2011, the unserializable state the budget Warning 2021 (§10).
        static void Setup(DuckyBuilder d) => d
            .AddSlice<RatioSlice>().Prerender<RatioSlice>()
            .Prerender<ProductsSlice, Products>(static p => p.Error is null).AddEffect<LoadProducts>()
            .Prerender<CounterSlice, Counter>(static c => c.Value > 0);
        var logs = new FakeLogCollector();
        var seed = await PrerenderAsync(Setup, static store =>
        {
            Increment(store, 5);
            store.Dispatch(new ProductsFailed("relative URL"));
            store.Dispatch(new SetRatio(double.NaN));
        }, logs);
        logs.GetSnapshot().Where(static r => r.Id.Id == 2011).ShouldHaveSingleItem().Message.ShouldContain(new ProductsSlice().Key);
        logs.GetSnapshot().Where(static r => r.Id.Id == 2021).ShouldHaveSingleItem().Message.ShouldContain(new RatioSlice().Key);

        await InteractiveAsync(seed, Setup);
        Render<CounterView>().Markup.ShouldBe("5");
        await Store.WhenIdleAsync(Ct);
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        Store.State.WasRestored<Products>().ShouldBeFalse();
        Store.State.WasRestored<Ratio>().ShouldBeFalse();
        Store.State.Get<Products>().ShouldBe(new Products(20, null));
        _loads.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Prerender_StateAtTheStoreMaxDepth_RoundTrips()
    {
        // (non-normative) The envelope adds two levels above each state: the deepest state the store's options write
        // (MaxDepth 128 here) is still written and read back.
        var collector = new FakeLogCollector();
        Services.AddLogging(b => b.AddProvider(new FakeLoggerProvider(collector)));
        static void Setup(DuckyBuilder d) => d.UseJson(Persistence.DeepEnvelopeJson.Default).AddSlice<DeepNestSlice>().Prerender<DeepNestSlice>();
        var seed = await PrerenderAsync(Setup);

        await InteractiveAsync(seed, Setup);
        Render<CounterView>();

        Store.State.WasRestored<Persistence.Nest>().ShouldBeTrue();
        Store.State.Get<Persistence.Nest>().ShouldBe(Persistence.Nest.Deep(127));
        collector.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2010);
    }

    [Fact]
    public async Task Prerender_UnreadableSeed_LoggedNothingRestored()
    {
        // (non-normative) A seed that is not an envelope restores nothing, logs Warning 2010 and still settles.
        var collector = new FakeLogCollector();
        Services.AddLogging(b => b.AddProvider(new FakeLoggerProvider(collector)));
        await InteractiveAsync(Seeded(Encoding.UTF8.GetBytes("not json")), static d => d.Prerender<CounterSlice>());

        Render<CounterView>().Markup.ShouldBe("0");
        SeedSettled.ShouldBeTrue();
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        var record = collector.GetSnapshot().Where(static r => r.Id.Id == 2010).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Exception.ShouldBeOfType<JsonException>();

        // An envelope without slices (JSON null) is unreadable too; no seed at all is not (the prerender pass itself).
        foreach (var (persisted, logged) in new[] { (Seeded(Encoding.UTF8.GetBytes("null")), true), (new FakeComponentStateStore(), false) })
        {
            await using var other = new BunitContext();
            var otherLogs = new FakeLogCollector();
            other.Services.AddLogging(b => b.AddProvider(new FakeLoggerProvider(otherLogs)));
            Configure(other, _static, static d => d.Prerender<CounterSlice>(), _loads);
            await other.Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(persisted);
            other.Services.GetRequiredService<IStore>().State.WasRestored<Counter>().ShouldBeFalse();
            other.Services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.IsCompletedSuccessfully.ShouldBeTrue();
            var unreadable = otherLogs.GetSnapshot().Where(static r => r.Id.Id == 2010).ToList();
            unreadable.Count.ShouldBe(logged ? 1 : 0);
            unreadable.ShouldAllBe(static r => r.Exception == null);
        }
    }

    [Theory]
    [InlineData("circuit")]
    [InlineData("browser-sync-prefix")]
    [InlineData("browser-first-registration")]
    public async Task Prerender_SeedNotAByteArray_LoggedNothingRestored(string path)
    {
        // (non-normative) A value under the key that is not a JSON byte[] (a foreign or hand-edited payload) makes
        // TryTakeFromJson throw: on every take path that is an unreadable seed too (Warning 2010, nothing restored,
        // SeedSettled completed, the first component renders).
        var raw = new FakeComponentStateStore();
        raw.State[SeedKey] = "{}"u8.ToArray();
        if (path == "circuit")
        {
            Services.AddLogging(b => b.AddProvider(new FakeLoggerProvider(_logs)));
            await InteractiveAsync(raw, Wasm);
        }
        else
        {
            Browser(Wasm);
            if (path == "browser-sync-prefix")
            {
                await RunAsync(raw);
                await Store.InitializeAsync(Ct);
            }
            else
            {
                Store.Dispatch(new ProductsLoaded(5));
                await RunAsync(raw);
            }
        }

        Render<CounterView>().Markup.ShouldBe("0");
        SeedSettled.ShouldBeTrue();
        await Store.InitializeAsync(Ct);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        var record = _logs.GetSnapshot().Where(static r => r.Id.Id == 2010).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Exception.ShouldBeOfType<JsonException>();
    }

    [Fact]
    public async Task Prerender_BrowserNegativeWaitTimeout_StillSettles()
    {
        // (non-normative) A negative PrerenderSeedWaitTimeout (other than infinite) is invalid for the wait: init fails
        // (logged), but the wait still settles, so persistence never stalls on SeedSettled.
        Browser(static d => d.AddBlazor(static o => o.PrerenderSeedWaitTimeout = TimeSpan.FromSeconds(-5)).Prerender<CounterSlice>());

        await Store.InitializeAsync(Ct);

        SeedSettled.ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_Browser_NoPersistingRegistration()
    {
        // (non-normative) In the browser the seed is taken but nothing is persisted: pausing is a circuit feature (§11.4).
        var seed = await PersistAsync(_server, static d => d.AddBlazor(static o => o.IsBrowser = true).Prerender<CounterSlice>(), static store => Increment(store, 1));

        seed.RenderModes.ShouldBeEmpty();
        seed.State.ShouldBeEmpty();
    }

    [Fact]
    public async Task Prerender_NoPersistentComponentState_InitCompletes()
    {
        // (non-normative) A host without PersistentComponentState (a console or a test container) has nothing to take
        // or persist: init still completes and SeedSettled settles.
        await using var services = new ServiceCollection()
            .AddDucky(static d => d.UseJson(PrerenderJson.Default).AddSlice<CounterSlice>().Prerender<CounterSlice>())
            .BuildServiceProvider();
        var store = services.GetRequiredService<IStore>();

        await store.InitializeAsync(Ct);

        store.State.Get<Counter>().Value.ShouldBe(0);
        services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.IsCompletedSuccessfully.ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_WasmPreloadBeforeRun_SeedStillApplied()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 3));
        Browser(Wasm);

        // A Program.cs preload dispatches before RunAsync: init starts while PersistentComponentState is still empty, so
        // the take fails and init waits for the first registration.
        Store.Dispatch(new ProductsLoaded(5));
        SeedSettled.ShouldBeFalse();

        // RunAsync restores the page's state, then the first component registers: its Select takes the seed before it
        // renders.
        await RunAsync(seed);
        List<string> order = [];
        var cut = Render<CounterView>(p => p.Add(c => c.Order, order));

        cut.Markup.ShouldBe("3");
        order.ShouldBe(["render"]);
        SeedSettled.ShouldBeTrue();
        await Store.InitializeAsync(Ct);
        Store.State.WasRestored<Counter>().ShouldBeTrue();

        // InitializeAsync ends at StoreInitialized; the buffered preload replays behind it, possibly on another drainer.
        await Store.WhenIdleAsync(Ct);
        Store.State.Get<Products>().ShouldBe(new Products(5, null));
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2022);
    }

    [Fact]
    public async Task Prerender_EnhancedNavLaterSeed_Ignored()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 3));
        var later = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 9));
        Browser(Wasm);
        Store.Dispatch(new ProductsLoaded(5));
        await RunAsync(seed);
        Render<CounterView>().Markup.ShouldBe("3");
        await Store.InitializeAsync(Ct);

        // The buffered preload may still be replaying on another drainer, so a plain Dispatch could return unreduced.
        await Store.DispatchAsync(new Increment());

        // An enhanced navigation (or a new WASM island) brings a later seed: the store took its seed once, and after
        // Ready the live store is authoritative, so nothing reads it.
        await Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(later, RestoreContext.ValueUpdate);
        Render<CounterView>().Markup.ShouldBe("4");
        await Store.WhenIdleAsync(Ct);
        Store.State.Get<Counter>().Value.ShouldBe(4);
        Services.GetRequiredService<PersistentComponentState>().TryTakeFromJson<byte[]>(SeedKey, out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_BrowserWaitTimeout_WarnsOnce()
    {
        // (non-normative) An awaited preload: no component registers within PrerenderSeedWaitTimeout and nothing was
        // restored, so the wait ends at the bound with Warning 2022 naming the fix, and SeedSettled completes.
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 7));
        Browser(static d => d.AddBlazor(static o => o.PrerenderSeedWaitTimeout = TimeSpan.FromSeconds(1)).Prerender<CounterSlice>());
        var init = Store.InitializeAsync(Ct);

        _time.Advance(TimeSpan.FromMilliseconds(999));
        SeedSettled.ShouldBeFalse();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        await init;

        SeedSettled.ShouldBeTrue();
        var warning = _logs.GetSnapshot().Where(static r => r.Id.Id == 2022).ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);
        warning.Message.ShouldContain("RunAsync");
        warning.Message.ShouldContain("<DuckyInitializer>");

        // A seed restored after the bound is never read: the first registration neither takes nor warns again.
        await RunAsync(seed);
        Render<CounterView>().Markup.ShouldBe("0");
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2022).ShouldBe(1);
    }

    [Fact]
    public async Task Prerender_BrowserWaitTimeout_TakesSeedRestoredMeanwhile()
    {
        // (non-normative) The timeout takes once more: a seed restored while no component registered is still applied at
        // the default 250 ms bound, without the warning.
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 2));
        Browser(Wasm);
        var init = Store.InitializeAsync(Ct);
        await RunAsync(seed);

        _time.Advance(TimeSpan.FromMilliseconds(249));
        SeedSettled.ShouldBeFalse();
        _time.Advance(TimeSpan.FromMilliseconds(1));
        await init;

        Store.State.Get<Counter>().Value.ShouldBe(2);
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2022);
    }

    [Fact]
    public async Task Prerender_BrowserSeedRestoredBeforeInit_TakenSynchronously()
    {
        // (non-normative) When init starts after RunAsync restored (here before any component registers), the synchronous
        // prefix takes the seed and settles at once: nothing waits for a registration or the bound.
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 3));
        Browser(Wasm);
        await RunAsync(seed);

        var init = Store.InitializeAsync(Ct);
        SeedSettled.ShouldBeTrue();
        await init;

        Render<CounterView>().Markup.ShouldBe("3");
        Store.State.WasRestored<Counter>().ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_BrowserNoSeed_FirstRegistrationSettlesWithoutWarning()
    {
        // (non-normative) WASM restores before the first render, so a failed take at the first registration means there is
        // no seed: it settles the wait without the timeout's warning.
        Browser(Wasm);
        Store.Dispatch(new ProductsLoaded(5));
        await RunAsync(new FakeComponentStateStore());

        Render<CounterView>().Markup.ShouldBe("0");

        SeedSettled.ShouldBeTrue();
        await Store.InitializeAsync(Ct);
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2022);
    }

    [Fact]
    public async Task Prerender_WasmOverflowAbortBeforeFirstRegistration_SeedIgnored()
    {
        var seed = await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 9));
        Browser(static d =>
        {
            d.InitBufferCapacity = 1;
            Wasm(d);
        });

        // A Program.cs preload starts init before RunAsync, RunAsync restores the seed, and the preload then dispatches past
        // InitBufferCapacity before any component registered: the overflow abort cancels the init token and makes the
        // store Ready, which ends the seed wait at once; init still ends normally.
        Store.Dispatch(new Increment());
        await RunAsync(seed);
        Increment(Store, 2);
        await Store.InitializeAsync(Ct);
        await Services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.WaitAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        // The seed is there, but the restore is skipped: it would land after StoreInitialized and the replayed actions,
        // overwrite them with 9 and flip WasRestored.
        Store.State.Get<Counter>().Value.ShouldBe(3);
        Render<CounterView>().Markup.ShouldBe("3");
        Store.State.WasRestored<Counter>().ShouldBeFalse();
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2022);
        _logs.GetSnapshot().Where(static r => r.Id.Id == 2025).ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
    }

    [Fact]
    public void Prerender_StateWithoutTypeInfo_Ducky306()
    {
        // (non-normative) Both overloads require the state's JSON type info (§10) and add Ducky.Blazor.
        foreach (var configure in new Action<DuckyBuilder>[]
        {
            static d => d.AddSlice<Persistence.TallySlice>().Prerender<Persistence.TallySlice>(),
            static d => d.AddSlice<Persistence.TallySlice>().Prerender<Persistence.TallySlice, Persistence.Tally>(static _ => true),
        })
        {
            using var services = new ServiceCollection().AddDucky(configure).BuildServiceProvider();
            services.GetService<BlazorOptions>().ShouldNotBeNull();
            var error = Should.Throw<DuckyConfigurationException>(() => services.GetRequiredService<IStore>()).Errors.ShouldHaveSingleItem();
            error.Code.ShouldBe("DUCKY306");
            error.Message.ShouldContain("Prerender<TallySlice> needs");
        }

        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.Prerender<CounterSlice>(null!)).ParamName.ShouldBe("builder");
        Should.Throw<ArgumentNullException>(() => DuckyBlazorBuilderExtensions.Prerender<CounterSlice, Counter>(null!, static _ => true)).ParamName.ShouldBe("builder");
        Should.Throw<ArgumentNullException>(() => new ServiceCollection().AddDucky(static d => d.Prerender<CounterSlice, Counter>(null!))).ParamName.ShouldBe("include");
    }

    private static void Increment(IStore store, int times)
    {
        for (var i = 0; i < times; i++)
        {
            store.Dispatch(new Increment());
        }
    }

    private static FakeComponentStateStore Seeded(byte[] envelope)
    {
        var store = new FakeComponentStateStore();
        store.State[SeedKey] = JsonSerializer.SerializeToUtf8Bytes(envelope);
        return store;
    }

    // One side of the handoff: a real persistence manager and a Ducky store with Ducky.Blazor, rendered by `context`.
    private static void Configure(
        BunitContext context, RendererInfo renderer, Action<DuckyBuilder> ducky, Loads loads, FakeLogCollector? logs = null, TimeProvider? time = null)
    {
        if (logs is not null)
        {
            context.Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(logs)));
        }

        if (time is not null)
        {
            context.Services.AddSingleton(time);
        }

        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddSingleton(loads);
        context.Services.AddDucky(d => ducky(d.UseJson(PrerenderJson.Default).AddSlice<CounterSlice>().AddSlice<ProductsSlice>().AddBlazor()));
        context.Renderer.SetRendererInfo(renderer);
    }

    // The prerender pass: a static render touches the store, then the framework persists the page's state.
    private Task<FakeComponentStateStore> PrerenderAsync(Action<DuckyBuilder> ducky, Action<IStore>? act = null, FakeLogCollector? logs = null) =>
        PersistAsync(_static, ducky, act, logs);

    private async Task<FakeComponentStateStore> PersistAsync(
        RendererInfo renderer, Action<DuckyBuilder> ducky, Action<IStore>? act = null, FakeLogCollector? logs = null)
    {
        await using var side = new BunitContext();
        Configure(side, renderer, ducky, _loads, logs);
        side.Render<CounterView>();
        var store = side.Services.GetRequiredService<IStore>();
        act?.Invoke(store);
        var persisted = new FakeComponentStateStore();
        await Persist(side, persisted);
        return persisted;
    }

    private static void Wasm(DuckyBuilder ducky) => ducky.Prerender<CounterSlice>();

    // The WASM side before RunAsync: the store exists in the browser, its PersistentComponentState not restored yet.
    private void Browser(Action<DuckyBuilder> ducky)
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        Configure(this, _wasm, d => ducky(d.AddBlazor(static o => o.IsBrowser = true)), _loads);
    }

    // What WASM's RunAsync does before the first render: restore the page's persistent state.
    private Task RunAsync(FakeComponentStateStore seed) =>
        Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);

    // The framework persisting the page's (or the paused circuit's) state into `persisted`, through every registration.
    private static Task Persist(BunitContext side, IPersistentComponentStateStore persisted) =>
        side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, side.Renderer);

    // The interactive side, its persistent state restored from `seed` as the circuit (or WASM host) does before rendering.
    private async Task InteractiveAsync(FakeComponentStateStore seed, Action<DuckyBuilder> ducky)
    {
        Configure(this, _server, ducky, _loads);
        await Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);
    }
}

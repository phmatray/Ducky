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

namespace Ducky.Blazor.Tests.Prerender;

// SPEC §11.4 (the prerender handoff outside the browser: seed take, SeedSettled, the Interactive Auto persisting
// registration); INV-15. Both sides drive a real ComponentStatePersistenceManager over a FakeComponentStateStore (§17.2):
// this context is the interactive side, a second BunitContext the prerender pass or the paused circuit.
public sealed class PrerenderHandoffTests : BunitContext
{
    private const string SeedKey = "ducky:seed";
    private static readonly RendererInfo _static = new("Static", isInteractive: false);
    private static readonly RendererInfo _server = new("Server", isInteractive: true);
    private readonly Loads _loads = new();

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

        // The resumed circuit is a fresh store: the Prerender<T> slice comes back from the seed, the other one resets.
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
        // interactive side. M6-05 adds the budget Warning 2021 (§10) for the unserializable slice and its assertion here.
        static void Setup(DuckyBuilder d) => d
            .AddSlice<RatioSlice>().Prerender<RatioSlice>()
            .Prerender<ProductsSlice, Products>(static p => p.Error is null).AddEffect<LoadProducts>()
            .Prerender<CounterSlice, Counter>(static c => c.Value > 0);
        var seed = await PrerenderAsync(Setup, static store =>
        {
            Increment(store, 5);
            store.Dispatch(new ProductsFailed("relative URL"));
            store.Dispatch(new SetRatio(double.NaN));
        });

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
    private static void Configure(BunitContext context, RendererInfo renderer, Action<DuckyBuilder> ducky, Loads loads)
    {
        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddSingleton(loads);
        context.Services.AddDucky(d => ducky(d.UseJson(PrerenderJson.Default).AddSlice<CounterSlice>().AddSlice<ProductsSlice>().AddBlazor()));
        context.Renderer.SetRendererInfo(renderer);
    }

    // The prerender pass: a static render touches the store, then the framework persists the page's state.
    private Task<FakeComponentStateStore> PrerenderAsync(Action<DuckyBuilder> ducky, Action<IStore>? act = null) =>
        PersistAsync(_static, ducky, act);

    private async Task<FakeComponentStateStore> PersistAsync(RendererInfo renderer, Action<DuckyBuilder> ducky, Action<IStore>? act = null)
    {
        await using var side = new BunitContext();
        Configure(side, renderer, ducky, _loads);
        side.Render<CounterView>();
        var store = side.Services.GetRequiredService<IStore>();
        act?.Invoke(store);
        await store.WhenIdleAsync(Ct);
        var persisted = new FakeComponentStateStore();
        await side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, side.Renderer);
        return persisted;
    }

    // The interactive side, its persistent state restored from `seed` as the circuit (or WASM host) does before rendering.
    private async Task InteractiveAsync(FakeComponentStateStore seed, Action<DuckyBuilder> ducky)
    {
        Configure(this, _server, ducky, _loads);
        await Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);
    }
}

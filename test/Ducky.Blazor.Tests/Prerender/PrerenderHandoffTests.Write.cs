using System.Text.Json;
using Bunit;
using CsCheck;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Ducky.TestSupport;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Blazor.Tests.Prerender;

// SPEC §11.4 OnPersisting (seed writing): the idle wait bounded by PrerenderIdleTimeout (Warning 2014), one snapshot, the
// include predicate (Debug 2011), the PrerenderSeedMaxWireBytes budget over the wire estimate (Warning 2021) and src.
public sealed partial class PrerenderHandoffTests
{
    private static readonly IDataProtector _circuitState =
        new EphemeralDataProtectionProvider().CreateProtector("Microsoft.AspNetCore.Components.Server.State");

    [Fact]
    public async Task Prerender_LoadEffectSlowerThanRender_SeedWaitsForIdle()
    {
        static void Setup(DuckyBuilder d) => d.Prerender<CounterSlice>().AddEffect<SlowLoadCounter>();
        await using var side = new BunitContext();
        Configure(side, _static, Setup, _loads);
        side.Render<CounterView>().Markup.ShouldBe("0");

        // The page rendered before the load completed: the framework's persist waits for the store to go idle.
        var seed = new FakeComponentStateStore();
        var persisting = Persist(side, seed);
        persisting.IsCompleted.ShouldBeFalse();
        _loads.Release.SetResult();
        await persisting.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Slices(seed).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(1);
        await InteractiveAsync(seed, Setup);
        Render<CounterView>().Markup.ShouldBe("1");
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        _loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Prerender_IdleTimeout_PersistsNoSeed_LogsOnce()
    {
        // An app with both render modes: the framework calls the registration once per payload. The load never completes
        // within PrerenderIdleTimeout (an hour here, on the injected TimeProvider: a wall-clock bound never fires within the
        // wait below): neither payload gets a seed, and the timeout is logged once for the store.
        var time = new FakeTimeProvider();
        var logs = new FakeLogCollector();
        await using var side = new BunitContext();
        Configure(side, _static, static d => d.AddBlazor(static o => o.PrerenderIdleTimeout = TimeSpan.FromHours(1))
            .Prerender<CounterSlice>().AddEffect<SlowLoadCounter>(), _loads, logs, time);
        side.Render<CounterView>();

        var payloads = new CompositeStateStore();
        var persisting = Persist(side, payloads);
        time.Advance(TimeSpan.FromHours(1));
        await persisting.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        payloads.Server.State.ShouldBeEmpty();
        payloads.Wasm.State.ShouldBeEmpty();
        var record = logs.GetSnapshot().Where(static r => r.Id.Id == 2014).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        _loads.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Prerender_IdleOneTickBeforeTimeout_PersistsSeed()
    {
        // The wait lasts the whole PrerenderIdleTimeout: a shorter bound would already have cancelled it inside Advance
        // (FakeTimeProvider fires the CTS timer synchronously, and Task.WaitAsync cancels synchronously), so the outcome
        // is settled before the release and does not depend on when the continuations run.
        var time = new FakeTimeProvider();
        var logs = new FakeLogCollector();
        await using var side = new BunitContext();
        Configure(side, _static, static d => d.AddBlazor(static o => o.PrerenderIdleTimeout = TimeSpan.FromHours(1))
            .Prerender<CounterSlice>().AddEffect<SlowLoadCounter>(), _loads, logs, time);
        side.Render<CounterView>();

        var seed = new FakeComponentStateStore();
        var persisting = Persist(side, seed);
        time.Advance(TimeSpan.FromHours(1) - TimeSpan.FromTicks(1));
        _loads.Release.SetResult();
        await persisting.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Slices(seed).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(1);
        logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2014);
    }

    [Fact]
    public async Task Prerender_LongRunningEffect_DoesNotBlockSeed()
    {
        // A poller runs for the store's lifetime; the seed still waits for the ordinary load, and only for it (the idle
        // timeout runs on fake time, which never advances here).
        static void Setup(DuckyBuilder d) => d.Prerender<CounterSlice>().AddEffect<SlowLoadCounter>().AddEffect<Poller>();
        var logs = new FakeLogCollector();
        await using var side = new BunitContext();
        Configure(side, _static, Setup, _loads, logs, new FakeTimeProvider());
        side.Render<CounterView>();

        var seed = new FakeComponentStateStore();
        var persisting = Persist(side, seed);
        persisting.IsCompleted.ShouldBeFalse();
        _loads.Release.SetResult();
        await persisting.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Slices(seed).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(1);
        logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2014);
    }

    [Fact]
    public async Task PrerenderSeed_AboveBudget_OmitsSliceAndLogs()
    {
        // In registration order: Counter fits, Note (10 000 characters, about 24 KB on the wire) would push the seed past
        // the 20 KB default and is left out, and Ratio, after it, still fits. Both payloads carry the same seed; the
        // omission is logged once.
        static void Setup(DuckyBuilder d) => d.AddSlice<NoteSlice>().AddSlice<RatioSlice>()
            .Prerender<CounterSlice>().Prerender<NoteSlice>().Prerender<RatioSlice>();
        var logs = new FakeLogCollector();
        await using (var side = new BunitContext())
        {
            Configure(side, _static, Setup, _loads, logs);
            side.Render<CounterView>();
            var store = side.Services.GetRequiredService<IStore>();
            Increment(store, 2);
            store.Dispatch(new SetNote(new string('a', 10_000)));
            store.Dispatch(new SetRatio(0.5));
            var payloads = new CompositeStateStore();
            await Persist(side, payloads);
            payloads.Wasm.State[SeedKey].ShouldBe(payloads.Server.State[SeedKey]);
            await InteractiveAsync(payloads.Server, Setup);
        }

        var record = logs.GetSnapshot().Where(static r => r.Id.Id == 2021).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldContain(new NoteSlice().Key);

        Render<CounterView>().Markup.ShouldBe("2");
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        Store.State.WasRestored<Ratio>().ShouldBeTrue();
        Store.State.Get<Ratio>().ShouldBe(new Ratio(0.5));
        Store.State.WasRestored<Note>().ShouldBeFalse();
        Store.State.Get<Note>().ShouldBe(new Note(string.Empty));
    }

    [Fact]
    public async Task PrerenderSeed_SliceAtTheBudget_Included()
    {
        // (non-normative) A slice whose seed's estimate equals PrerenderSeedMaxWireBytes is kept; one byte less leaves it out.
        var n = SeedBytes(await PrerenderAsync(static d => d.Prerender<CounterSlice>(), static store => Increment(store, 1))).Length;
        var budget = (int)SeedWriter.WireEstimate(n);

        var atBudget = await PrerenderAsync(d => d.AddBlazor(o => o.PrerenderSeedMaxWireBytes = budget).Prerender<CounterSlice>(), static store => Increment(store, 1));
        Slices(atBudget).TryGetProperty(new CounterSlice().Key, out _).ShouldBeTrue();

        var logs = new FakeLogCollector();
        var below = await PrerenderAsync(d => d.AddBlazor(o => o.PrerenderSeedMaxWireBytes = budget - 1).Prerender<CounterSlice>(), static store => Increment(store, 1), logs);
        Slices(below).EnumerateObject().ShouldBeEmpty();
        logs.GetSnapshot().ShouldContain(static r => r.Id.Id == 2021);
    }

    [Fact]
    public void PrerenderSeed_WireEstimate_NeverBelowActualEncoding()
    {
        // Quote-dense and CJK states, the worst cases for a string seed: the estimate for the envelope's n bytes is never
        // below the length Interactive Server really sends, the seed persisted by a real ComponentStatePersistenceManager,
        // its state dictionary serialized, data-protected and base64-encoded as the protected prerender store does.
        var chars = Gen.OneOf(Gen.Char["\"\\<>&'\u2028\u00e9"], Gen.Char['\u4e00', '\u9fff']);
        Property.Check(Gen.String[chars, 0, 3_000], text =>
        {
            using var side = new BunitContext();
            Configure(side, _static, static d => d.AddBlazor(static o => o.PrerenderSeedMaxWireBytes = int.MaxValue).AddSlice<NoteSlice>().Prerender<NoteSlice>(), new Loads());
            side.Render<CounterView>();
            side.Services.GetRequiredService<IStore>().Dispatch(new SetNote(text));
            var seed = new FakeComponentStateStore();
            Persist(side, seed).IsCompletedSuccessfully.ShouldBeTrue();

            Slices(seed).GetProperty(new NoteSlice().Key).GetProperty("text").GetString().ShouldBe(text);
            var wire = Convert.ToBase64String(_circuitState.Protect(JsonSerializer.SerializeToUtf8Bytes<IReadOnlyDictionary<string, byte[]>>(seed.State))).Length;
            SeedWriter.WireEstimate(SeedBytes(seed).Length).ShouldBeGreaterThanOrEqualTo(wire);
        });
    }

    [Fact]
    public void PrerenderSeed_WireEstimate_ReproducesThePipeline()
    {
        // (non-normative) e = 4·⌈n/3⌉ + 2, then wire = 4·⌈(4·⌈e/3⌉ + 256)/3⌉ (§11.4).
        SeedWriter.WireEstimate(0).ShouldBe(348);
        SeedWriter.WireEstimate(1).ShouldBe(352);
        SeedWriter.WireEstimate(1_000).ShouldBe(2_720);
        SeedWriter.WireEstimate(20_000).ShouldBe(47_756);
    }

    [Fact]
    public async Task DuckyInitializerFirstToucher_SeedWrittenOnPrerenderAndAppliedBeforeFirstInteractiveRender()
    {
        static void Setup(DuckyBuilder d) => d.Prerender<CounterSlice>().AddEffect<SlowLoadCounter>();

        // Prerender: DuckyInitializer is the first toucher; its load completes after the render, and the seed waits for it.
        var seed = new FakeComponentStateStore();
        await using (var side = new BunitContext())
        {
            Configure(side, _static, Setup, _loads);
            side.Render<DuckyInitializer>(p => p.AddChildContent<CounterView>());
            var persisting = Persist(side, seed);
            _loads.Release.SetResult();
            await persisting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        }

        // Interactive: DuckyInitializer is the first toucher again, and its content's first render shows the seed.
        await InteractiveAsync(seed, Setup);
        List<string> order = [];
        var cut = Render<DuckyInitializer>(p => p.AddChildContent<CounterView>(c => c.Add(v => v.Order, order)));

        cut.Markup.ShouldBe("1");
        order.ShouldBe(["render"]);
        _loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Prerender_LoadFailedDuringPrerender_InteractiveSideRetries()
    {
        // The prerender-time load fails and the store goes idle on the error state; the include predicate leaves it out
        // (Debug 2011), so the interactive side doesn't see it restored and loads again.
        static void Setup(DuckyBuilder d) => d.Prerender<ProductsSlice, Products>(static p => p.Error is null).AddEffect<LoadProducts>();
        var logs = new FakeLogCollector();
        _loads.Fail = true;
        var seed = await PrerenderAsync(Setup, logs: logs);

        Slices(seed).TryGetProperty(new ProductsSlice().Key, out _).ShouldBeFalse();
        var record = logs.GetSnapshot().Where(static r => r.Id.Id == 2011).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Debug);
        record.Message.ShouldContain(new ProductsSlice().Key);

        _loads.Fail = false;
        await InteractiveAsync(seed, Setup);
        Render<CounterView>();
        await Store.WhenIdleAsync(Ct);
        Store.State.WasRestored<Products>().ShouldBeFalse();
        Store.State.Get<Products>().ShouldBe(new Products(10, null));
        _loads.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PrerenderSeed_IncludeThrows_OmitsSliceAndKeepsThePagesOtherState()
    {
        // (non-normative) A predicate that dereferences what an incomplete state leaves null throws: like a throwing getter
        // (§10), only that slice is left out (Warning 2021); the other slices, and the page's other persisted state (the
        // template's authentication state), are still persisted.
        static void Setup(DuckyBuilder d) => d.Prerender<CounterSlice>().Prerender<ProductsSlice, Products>(static p => p.Error!.Length == 0);
        var logs = new FakeLogCollector();
        await using var side = new BunitContext();
        Configure(side, _static, Setup, _loads, logs);
        side.Render<CounterView>();
        Increment(side.Services.GetRequiredService<IStore>(), 3);

        var persisted = await PersistWithAuthAsync(side);

        persisted.State.Keys.Order().ShouldBe(["auth", SeedKey]);
        Slices(persisted).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(3);
        Slices(persisted).TryGetProperty(new ProductsSlice().Key, out _).ShouldBeFalse();
        var record = logs.GetSnapshot().Where(static r => r.Id.Id == 2021).ShouldHaveSingleItem();
        record.Message.ShouldContain(new ProductsSlice().Key);
        record.Exception.ShouldBeOfType<NullReferenceException>();
    }

    [Fact]
    public async Task PrerenderSeed_WriteFails_PersistsNoSeedAndKeepsThePagesOtherState()
    {
        // (non-normative) A failure writing the seed (here a negative PrerenderIdleTimeout, which no CancellationTokenSource
        // accepts) only degrades to no seed (Warning 2012): it never fails the page's whole state persistence.
        var logs = new FakeLogCollector();
        await using var side = new BunitContext();
        Configure(side, _static, static d => d.AddBlazor(static o => o.PrerenderIdleTimeout = TimeSpan.FromSeconds(-5)).Prerender<CounterSlice>(), _loads, logs);
        side.Render<CounterView>();

        var persisted = await PersistWithAuthAsync(side);

        persisted.State.Keys.ShouldBe(["auth"]);
        var record = logs.GetSnapshot().Where(static r => r.Id.Id == 2012).ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Warning);
        record.Exception.ShouldBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(true, "pause")]
    [InlineData(false, "prerender")]
    public async Task Seed_GateUndecided_ProbesOnceForSrc(bool circuit, string src)
    {
        // (non-normative) No Ducky component touched the store (an injected SliceStore in a foreign component): the handoff
        // never depends on an Unknown gate, so it probes once after the idle wait. A live circuit's import succeeds (a
        // pause seed); a prerender pass's import is already faulted with an InvalidOperationException (a prerender seed).
        var js = new FakeJsRuntime();
        if (!circuit)
        {
            js.Respond = static (_, _) => Task.FromException<object?>(new InvalidOperationException("prerendering"));
        }

        await using var side = new BunitContext();
        side.Services.AddSingleton<Microsoft.JSInterop.IJSRuntime>(js);
        Configure(side, circuit ? _server : _static, static d => d.Prerender<CounterSlice>(), _loads);
        Increment(side.Services.GetRequiredService<IStore>(), 2);

        var persisted = new FakeComponentStateStore();
        await Persist(side, persisted);

        Envelope(persisted).GetProperty("src").GetString().ShouldBe(src);
        Slices(persisted).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(2);
        js.Calls.ShouldHaveSingleItem().Identifier.ShouldBe("import");
    }

    // The page's state persisted with another PersistentComponentState user, such as the template's authentication state.
    private static async Task<FakeComponentStateStore> PersistWithAuthAsync(BunitContext side)
    {
        var state = side.Services.GetRequiredService<PersistentComponentState>();
        using var auth = state.RegisterOnPersisting(() =>
        {
            state.PersistAsJson("auth", 1);
            return Task.CompletedTask;
        }, RenderMode.InteractiveAuto);
        var persisted = new FakeComponentStateStore();
        await Persist(side, persisted);
        return persisted;
    }

    private static byte[] SeedBytes(FakeComponentStateStore persisted) => JsonSerializer.Deserialize<byte[]>(persisted.State[SeedKey])!;

    private static JsonElement Envelope(FakeComponentStateStore persisted) => JsonDocument.Parse(SeedBytes(persisted)).RootElement;

    private static JsonElement Slices(FakeComponentStateStore persisted) => Envelope(persisted).GetProperty("slices");
}

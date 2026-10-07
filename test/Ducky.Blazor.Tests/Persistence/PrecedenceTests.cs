using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.4 precedence (matrix conflict 8, ADR-0011), §11.5 step 5; INV-15. A slice both prerendered and persisted:
// the prerender seed applies first (no flicker), then a valid browser-storage envelope wins, whatever its age and
// whatever the host timing, because persistence enqueues its restore only after SeedSettled. Each seed comes from a real
// prerender pass (a second BunitContext); storage is ducky.js behind a FakeJsRuntime (§17.2).
public sealed class PrecedenceTests : BunitContext
{
    private static readonly RendererInfo _static = new("Static", isInteractive: false);
    private static readonly RendererInfo _server = new("Server", isInteractive: true);
    private static readonly RendererInfo _wasm = new("WebAssembly", isInteractive: true);
    private readonly FakeJsRuntime _js = new();
    private readonly Dictionary<(string Area, string Key), object?> _storage = [];
    private readonly HydrationLog _log = new();
    private readonly FakeLogCollector _logs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    public PrecedenceTests() => _js.Respond = (identifier, args) => identifier switch
    {
        "import" => _js,
        "storageGet" => _storage.GetValueOrDefault(((string)args[0]!, (string)args[1]!)),
        _ => null,
    };

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    private bool SeedSettled => Services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.IsCompletedSuccessfully;

    private IEnumerable<object?[]> Reads => _js.Calls.Where(static call => call.Identifier == "storageGet").Select(static call => call.Args);

    [Fact]
    public async Task Precedence_ValidStorageEnvelope_WinsOverSeed()
    {
        var seed = await PrerenderAsync(3);
        _storage[("local", "ducky:counter")] = Envelope(7, _time.GetUtcNow());
        Configure(_wasm, browser: true);

        // A Program.cs preload starts init before RunAsync: the storage read completes at once, but the seed is taken only
        // at the first registration. The storage restore waits for that settle, so it still comes second and wins.
        Store.Dispatch(new Increment());
        Reads.ShouldHaveSingleItem();
        SeedSettled.ShouldBeFalse();
        Store.State.Get<Counter>().Value.ShouldBe(0);

        await RestoreAsync(seed);
        var cut = Render<CounterView>();
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
            "Ducky.Blazor.Tests.Core.Increment Hydrated:0",
        ]);
        Store.State.Get<Counter>().ShouldBe(new Counter(8));
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        cut.WaitForAssertion(() => cut.Markup.ShouldBe("8"));
    }

    [Fact]
    public async Task Precedence_OlderStorageEnvelope_StillWins()
    {
        // The envelope was written a month before the prerender: there is no timestamp comparison (clocks skew), so a
        // valid envelope still wins over the seed.
        var seed = await PrerenderAsync(3);
        _storage[("local", "ducky:counter")] = Envelope(7, _time.GetUtcNow().AddDays(-30));
        Configure(_server);
        await RestoreAsync(seed);

        Render<CounterView>();
        await Store.InitializeAsync(Ct);

        _log.Entries.ShouldBe([
            "restore:counter Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        Store.State.Get<Counter>().ShouldBe(new Counter(7));
    }

    [Fact]
    public async Task Precedence_PrerenderSeed_StorageStillWins()
    {
        // A circuit after its prerender pass, with a storage read slower than the first render: the seed renders at once
        // (no flicker), then the stored value wins at the cost of one extra render.
        var seed = await PrerenderAsync(3);
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _js.Respond = (identifier, _) => identifier switch
        {
            "import" => _js,
            "storageGet" => read.Task,
            _ => null,
        };
        Configure(_server);
        await RestoreAsync(seed);

        List<string> order = [];
        var cut = Render<CounterView>(p => p.Add(c => c.Order, order));
        cut.Markup.ShouldBe("3");
        SeedSettled.ShouldBeTrue();

        read.SetResult(Envelope(7, _time.GetUtcNow()));
        await Store.InitializeAsync(Ct);

        cut.WaitForAssertion(() => cut.Markup.ShouldBe("7"));
        order.ShouldBe(["render", "render"]);
        Store.State.WasRestored<Counter>().ShouldBeTrue();
    }

    [Fact]
    public async Task Prerender_WasmAwaitedPreload_WarnsAndDoesNotInvertPrecedence()
    {
        var seed = await PrerenderAsync(3);
        _storage[("local", "ducky:counter")] = Envelope(7, _time.GetUtcNow());
        Configure(_wasm, browser: true);

        // Program.cs awaits a preload before RunAsync: no component can register and nothing is restored, so the seed wait
        // runs to its bound. The storage read is not held by it, but its restore is.
        var init = Store.InitializeAsync(Ct);
        Reads.ShouldHaveSingleItem();
        _time.Advance(TimeSpan.FromMilliseconds(249));
        SeedSettled.ShouldBeFalse();
        Store.State.Get<Counter>().Value.ShouldBe(0);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        await init;

        Store.State.Get<Counter>().ShouldBe(new Counter(7));
        var warning = _logs.GetSnapshot().Where(static r => r.Id.Id == 2022).ShouldHaveSingleItem();
        warning.Level.ShouldBe(LogLevel.Warning);

        // The seed restored afterwards is never applied: it can't roll storage's value back.
        await RestoreAsync(seed);
        Render<CounterView>().Markup.ShouldBe("7");
        await Store.WhenIdleAsync(Ct);
        Store.State.Get<Counter>().ShouldBe(new Counter(7));
        _log.Entries.ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "restore:counter Hydrating:0",
            "completed:True:0:System Hydrated:0",
            "initialized Hydrated:0",
        ]);
        _logs.GetSnapshot().Count(static r => r.Id.Id == 2022).ShouldBe(1);
    }

    private static string Envelope(int value, DateTimeOffset at) => EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, at);

    // The prerender pass: a static render of a store whose counter reached `count`, then the framework persists the page.
    private async Task<FakeComponentStateStore> PrerenderAsync(int count)
    {
        await using var side = new BunitContext();
        AddDucky(side, _static);
        side.Render<CounterView>();
        var store = side.Services.GetRequiredService<IStore>();
        for (var i = 0; i < count; i++)
        {
            store.Dispatch(new Increment());
        }

        var persisted = new FakeComponentStateStore();
        await side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, side.Renderer);
        return persisted;
    }

    // The interactive side: a circuit, or the WASM host (browser) before RunAsync restored its persistent state.
    private void Configure(RendererInfo renderer, bool browser = false)
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton(_log);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        AddDucky(this, renderer, browser, d => d.Use<HydrationRecorder>());
    }

    // What the circuit or WASM's RunAsync does before the first render: restore the page's persistent state.
    private Task RestoreAsync(FakeComponentStateStore seed) =>
        Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);

    private void AddDucky(BunitContext context, RendererInfo renderer, bool browser = false, Action<DuckyBuilder>? more = null)
    {
        context.Services.RemoveAll<IJSRuntime>();
        context.Services.AddSingleton<IJSRuntime>(_js);
        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddDucky(d =>
        {
            d.UseJson(HydrationJson.Default).AddSlice<CounterSlice>().AddBlazor(o => o.IsBrowser = browser).Prerender<CounterSlice>().Persist<CounterSlice>();
            more?.Invoke(d);
        });
        context.Renderer.SetRendererInfo(renderer);
    }
}

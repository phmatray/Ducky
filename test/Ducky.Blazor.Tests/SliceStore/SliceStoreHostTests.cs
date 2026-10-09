using Bunit;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.SliceStore;

// SPEC §12 (SliceStore.State is a point-in-time read that starts init), §11.4 (seed take and the browser's bounded wait),
// INV-15. A SliceStore in a Blazor host, touched by nothing but its own State and methods: the seed and storage still
// apply, a plain-component WASM app never stalls on the seed wait, and markup that must follow restores selects. Each
// seed comes from a real prerender pass (a second BunitContext) whose only toucher is the SliceStore; storage is ducky.js
// behind a FakeJsRuntime (§17.2).
public sealed class SliceStoreHostTests : BunitContext
{
    private static readonly RendererInfo _static = new("Static", isInteractive: false);
    private static readonly RendererInfo _server = new("Server", isInteractive: true);
    private static readonly RendererInfo _wasm = new("WebAssembly", isInteractive: true);
    private static readonly string _storageKey = $"ducky:{new CartStore().Key}";
    private readonly FakeJsRuntime _js = new();
    private readonly TaskCompletionSource<object?> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly FakeLogCollector _logs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    // A storage read slower than the first render: the test completes it when it chooses.
    public SliceStoreHostTests() => _js.Respond = (identifier, args) => identifier switch
    {
        "import" => _js,
        "storageGet" when (string)args[1]! == _storageKey => _read.Task,
        _ => null,
    };

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    private IStore Store => Services.GetRequiredService<IStore>();

    private CartStore Cart => Services.GetRequiredService<CartStore>();

    private bool SeedSettled => Services.GetRequiredService<PersistenceSlice>().SeedSettled.Task.IsCompletedSuccessfully;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SliceStore_StateReadFirst_SeedAndStorageApplied(bool browser)
    {
        static void Slices(DuckyBuilder d) => d.Prerender<CartStore>().Persist<CartStore>();
        var seed = await PrerenderAsync(3, Slices);
        Configure(browser ? _wasm : _server, browser, Slices);
        await RestoreAsync(seed);

        // Nothing touched the store before this read (no component, no IStore call): it starts init, whose synchronous
        // prefix restores the seed before the read returns.
        Cart.Count.ShouldBe(3);
        SeedSettled.ShouldBeTrue();

        // Storage still wins over the seed.
        _read.SetResult(Envelope(7));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);
        Cart.Count.ShouldBe(7);
        Store.State.WasRestored<Cart>().ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prerender_PlainComponentsOnlyWasm_NoInitStall(bool preload)
    {
        static void Slices(DuckyBuilder d) => d.Prerender<CartStore>();
        var seed = await PrerenderAsync(3, Slices);
        Configure(_wasm, browser: true, Slices);
        if (preload)
        {
            // Program.cs reads the SliceStore before RunAsync: init starts while PersistentComponentState is still empty,
            // so the take fails and init waits for a first registration.
            Cart.Count.ShouldBe(0);
        }

        await RestoreAsync(seed);

        // Plain components never register with the gate: either the read that starts init takes the seed at once, or the
        // wait ends at its bound and takes the seed restored meanwhile. Init never waits for a registration.
        var cut = Render<PlainCartView>();
        var init = Store.InitializeAsync(Ct);
        cut.Find("span").TextContent.ShouldBe(preload ? "0" : "3");
        SeedSettled.ShouldBe(!preload);
        _time.Advance(TimeSpan.FromMilliseconds(249));
        init.IsCompleted.ShouldBe(!preload);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        Cart.Count.ShouldBe(3);
        Store.State.WasRestored<Cart>().ShouldBeTrue();
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == 2022);

        // A plain component shows the restore only once its own handler re-renders it (§12): it holds no subscription.
        cut.Find("span").TextContent.ShouldBe(preload ? "0" : "3");
        await cut.Find("button").ClickAsync(new());
        cut.Find("span").TextContent.ShouldBe("4");
    }

    [Fact]
    public async Task SliceStore_RenderedThroughSelect_RerendersOnStorageRestore()
    {
        Configure(_server, browser: false, static d => d.Persist<CartStore>());
        var selected = Render<SelectedCartView>();
        var plain = Render<PlainCartView>();
        selected.Find("span").TextContent.ShouldBe("0");

        // The hydration restore commits after the first render: the selection re-renders its component, the plain
        // component's point-in-time read stays stale.
        _read.SetResult(Envelope(7));
        await Store.InitializeAsync(Ct);
        await Store.WhenIdleAsync(Ct);

        selected.WaitForAssertion(() => selected.Find("span").TextContent.ShouldBe("7"));
        plain.Find("span").TextContent.ShouldBe("0");

        // Mutations still go through the SliceStore's methods, and the selection follows them.
        await selected.Find("button").ClickAsync(new());
        selected.WaitForAssertion(() => selected.Find("span").TextContent.ShouldBe("8"));
        Cart.Count.ShouldBe(8);
    }

    private static string Envelope(int count) =>
        EnvelopeWriter.Write($$"""{"Count":{{count}}}""", version: 1, new DateTimeOffset(2026, 9, 30, 11, 0, 0, TimeSpan.Zero));

    // The prerender pass, configured like the interactive side: a static render whose only Ducky code is the injected
    // SliceStore. Nothing records the renderer, so the gate probes once, and RemoteJSRuntime's import is already faulted
    // during prerender (§11.3).
    private static async Task<FakeComponentStateStore> PrerenderAsync(int count, Action<DuckyBuilder> slices)
    {
        await using var side = new BunitContext();
        var js = new FakeJsRuntime { Respond = static (_, _) => Task.FromException<object?>(new InvalidOperationException("prerendering")) };
        AddDucky(side, js, _static, browser: false, slices);
        var cart = side.Services.GetRequiredService<CartStore>();
        for (var i = 0; i < count; i++)
        {
            cart.Add();
        }

        var persisted = new FakeComponentStateStore();
        await side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(persisted, side.Renderer);
        return persisted;
    }

    private static void AddDucky(BunitContext context, FakeJsRuntime js, RendererInfo renderer, bool browser, Action<DuckyBuilder> slices)
    {
        context.Services.RemoveAll<IJSRuntime>();
        context.Services.AddSingleton<IJSRuntime>(js);
        context.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        context.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        context.Services.AddDucky(d => slices(d.UseJson(SliceStoreJson.Default).AddSlice<CartStore>().AddBlazor(o => o.IsBrowser = browser)));
        context.Renderer.SetRendererInfo(renderer);
    }

    // The interactive side: a circuit, or the WASM host (browser) before RunAsync restored its persistent state.
    private void Configure(RendererInfo renderer, bool browser, Action<DuckyBuilder> slices)
    {
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)));
        AddDucky(this, _js, renderer, browser, slices);
    }

    // What the circuit or WASM's RunAsync does before the first render: restore the page's persistent state.
    private Task RestoreAsync(FakeComponentStateStore seed) =>
        Services.GetRequiredService<ComponentStatePersistenceManager>().RestoreStateAsync(seed);
}

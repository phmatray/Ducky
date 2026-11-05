using System.Text;
using System.Text.Json;
using Bunit;
using Ducky.Blazor.Tests.Components;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using SelectInOnInitialized = Ducky.Blazor.Tests.Interactivity.CounterView;

namespace Ducky.Blazor.Tests.DistributedCache.StaticSsr;

// SPEC §11.6 "NonInteractive stores" and §23 (the Post/Redirect/Get ceiling); INV-17. Pages render on the framework's real
// HtmlRenderer, whose quiescence wait covers component lifecycle tasks only, as a static SSR response does; every case runs
// over MemoryDistributedCache and over the wrapper that yields in GetAsync, as Redis effectively does.
public sealed class StaticSsrTests
{
    private const string Key = "app:alice:counter";
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaticSsr_AsyncDistributedCache_DuckyInitializer_HtmlShowsStoredValue(bool yield)
    {
        // DuckyInitializer awaits InitializeAsync in OnInitializedAsync, so the quiescence wait covers the read, however
        // long the cache takes, and the only output of the page holds the stored value.
        var cache = Stored(yield, value: 5);
        await using var request = Request(cache);

        var html = await RenderPageAsync(request);

        html.ShouldBe("5");
        cache.Calls.ShouldBe([$"get:{Key}"]);
        request.Js.Calls.ShouldBeEmpty();
        request.Services.GetRequiredService<PersistenceSlice>().Gate.Resolve(isBrowser: false, null!).ShouldBe(InteractivityMode.NonInteractive);
    }

    [Theory]
    [InlineData(false, "5")] // MemoryDistributedCache completes GetAsync synchronously: restored inline, before the render
    [InlineData(true, "0")]
    public async Task StaticSsr_AsyncDistributedCache_WithoutInitializer_HtmlShowsInitialState_Documented(bool yield, string expected)
    {
        // Documented (§11.6): a component that only calls Select in OnInitialized starts init but has no pending lifecycle
        // task, so nothing keeps the response open for the read: its HTML is the initial state, the only output of the page.
        // The store does hydrate, too late for the HTML.
        var cache = Stored(yield, value: 5);
        await using var request = Request(cache);

        var html = await RenderAsync<SelectInOnInitialized>(request, ParameterView.Empty);

        html.ShouldBe(expected);
        await request.InitializeAsync();
        request.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        cache.Calls.ShouldBe([$"get:{Key}"]);
        request.Js.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prerender_AsyncDistributedCache_WithoutInitializer_SeedCarriesStoredValue(bool yield)
    {
        // (non-normative) §11.6 "The prerender seed carries the hydrated values in any case": the page awaited nothing, but
        // OnPersisting waits for idle, which requires Ready and therefore the hydration terminal. The yielding read is held
        // past the framework's persist call, so the seed can't merely have raced ahead of it.
        var read = yield ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        var cache = Stored(yield, value: 5, read);
        await using var side = new BunitContext();
        side.Services.AddSingleton<TimeProvider>(new WatchedTime()); // never advanced: no deadline can fire while the read is held
        side.Services.RemoveAll<IJSRuntime>();
        side.Services.AddSingleton<IJSRuntime>(new FakeJsRuntime { Respond = Refuse });
        side.Services.AddSingleton(static sp => new ComponentStatePersistenceManager(NullLogger<ComponentStatePersistenceManager>.Instance, sp));
        side.Services.AddSingleton(static sp => sp.GetRequiredService<ComponentStatePersistenceManager>().State);
        side.Services.AddSingleton<IDistributedCache>(cache).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice"));
        side.Services.AddDucky(static d => d.UseJson(WriterJson.Default).AddSlice<CounterSlice>().AddBlazor(static o => o.KeyPrefix = "app").Prerender<CounterSlice>().Persist<CounterSlice>(Server));
        side.Renderer.SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        side.Render<CounterView>();

        var seed = new FakeComponentStateStore();
        var persisting = side.Services.GetRequiredService<ComponentStatePersistenceManager>().PersistStateAsync(seed, side.Renderer);
        if (read is not null)
        {
            persisting.IsCompleted.ShouldBeFalse();
            read.SetResult();
        }

        await persisting.WaitAsync(_bound, Ct);

        var envelope = JsonSerializer.Deserialize<byte[]>(seed.State["ducky:seed"]).ShouldNotBeNull();
        using var parsed = JsonDocument.Parse(Encoding.UTF8.GetString(envelope));
        parsed.RootElement.GetProperty("slices").GetProperty("counter").GetRawText().ShouldBe("""{"Value":5}""");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_StaticSsrPostRedirect_StaleReadDocumented(bool yield)
    {
        // Documented ceiling (§23): no public flush API in 2.0 (the ponytail note on the flush names the 2.1 upgrade path), so
        // the form post's pending write leaves with the request-scope flush, after the redirect response.
        var cache = Stored(yield, value: 5);
        var post = Request(cache, static o => o.Debounce = TimeSpan.FromSeconds(1));
        await using var _ = post;
        (await RenderPageAsync(post)).ShouldBe("5");
        post.Store.Dispatch(new Increment()); // the form handler: 6, then NavigateTo sends the redirect
        await post.Time.TimerAsync(TimeSpan.FromSeconds(1));
        post.Store.State.Get<Counter>().ShouldBe(new Counter(6));

        // The browser's immediate GET builds a new store, which reads the value from before the post.
        await using (var get = Request(cache))
        {
            (await RenderPageAsync(get)).ShouldBe("5");
        }

        cache.Calls.ShouldBe([$"get:{Key}", $"get:{Key}"]);

        // The POST's request scope ends: its flush lands now, with nothing left to show it, and the next GET sees it.
        await post.DisposeScopeAsync().AsTask().WaitAsync(_bound, Ct);
        Payload(cache.Stored(Key)).ShouldBe("""{"Value":6}""");
        await using var next = Request(cache);
        (await RenderPageAsync(next)).ShouldBe("6");
    }

    // A page whose root is DuckyInitializer around a CounterView.
    private static Task<string> RenderPageAsync(WriterHarness request) => RenderAsync<DuckyInitializer>(
        request,
        ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(DuckyInitializer.ChildContent)] = (RenderFragment)CounterPage }));

    private static void CounterPage(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CounterView>(0);
        builder.CloseComponent();
    }

    // One static SSR response: the framework takes the HTML in the dispatcher turn the quiescence wait ends in.
    private static async Task<string> RenderAsync<TComponent>(WriterHarness request, ParameterView parameters)
        where TComponent : IComponent
    {
        await using var renderer = new HtmlRenderer(request.Services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<TComponent>(parameters)).ToHtmlString());
    }

    private static TestCache Stored(bool yield, int value, TaskCompletionSource? read = null)
    {
        var cache = new TestCache(yield) { GetGate = read };
        cache.Store(Key, EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero)));
        return cache;
    }

    private static string Payload(string? envelope)
    {
        using var parsed = JsonDocument.Parse(envelope.ShouldNotBeNull());
        return parsed.RootElement.GetProperty("s").GetRawText();
    }

    private static void Server(PersistOptions options) => options.Storage = PersistStorage.Server;

    // One request's store of alice over `cache`, the counter in Server storage. JS refuses every call, as the prerender runtime does.
    private static WriterHarness Request(TestCache cache, Action<PersistOptions>? counter = null)
    {
        var harness = new WriterHarness(
            d => d.Persist<CounterSlice>(o =>
            {
                Server(o);
                counter?.Invoke(o);
            }).AddBlazor(static o => o.KeyPrefix = "app"),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(cache).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice")));
        harness.Js.Respond = Refuse;
        return harness;
    }

    private static object? Refuse(string identifier, object?[] args) => throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
}

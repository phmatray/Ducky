using Ducky.Blazor;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Docs.Tests.Blazor;

#region prerender-state
public sealed record DayForecast(string Day, int Celsius);

public sealed record ForecastState(IReadOnlyList<DayForecast>? Days, string? Error);

public sealed record ForecastLoaded(IReadOnlyList<DayForecast> Days);

public sealed record ForecastLoadFailed(string Error);

public sealed class ForecastSlice : Slice<ForecastState>
{
    public ForecastSlice()
    {
        On<ForecastLoaded>((_, action) => new ForecastState(action.Days, Error: null));
        On<ForecastLoadFailed>((state, action) => state with { Error = action.Error });
    }

    public override string Key => "forecast";

    protected override ForecastState Initial => new(Days: null, Error: null);
}

public interface IForecastApi
{
    Task<IReadOnlyList<DayForecast>> GetAsync(CancellationToken cancellationToken);
}
#endregion

// The snippets of docs/guides/blazor/prerender.md.
public sealed class PrerenderGuideTests
{
    // Private: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every accessible effect of this assembly.
    private static class App
    {
        #region prerender-effect
        // An Effect<T>, never a reactive effect: the seed waits for running effects, not for reactive pipelines.
        public sealed class LoadForecast(IForecastApi api) : Effect<StoreInitialized>
        {
            public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
            {
                if (context.State.WasRestored<ForecastState>())
                {
                    return;
                }

                try
                {
                    context.Dispatch(new ForecastLoaded(await api.GetAsync(cancellationToken)));
                }
                catch (HttpRequestException exception)
                {
                    context.Dispatch(new ForecastLoadFailed(exception.Message));
                }
            }
        }
        #endregion

        #region prerender-register
        public static DuckyBuilder AddForecast(DuckyBuilder ducky) => ducky
            .UseJson(DocsJson.Default)
            .AddSlice<ForecastSlice>()
            .AddEffect<LoadForecast>()
            // Seed only a loaded forecast: after a failed prerender load, the browser loads it again.
            .Prerender<ForecastSlice, ForecastState>(static state => state is { Days: not null, Error: null });
        #endregion
    }

    // Non-normative (M15-03): the prerender pass seeds a loaded forecast, so the interactive store doesn't load it again;
    // a failed load is left out of the seed, and so is a slice above the seed budget: the interactive store loads it.
    [Theory]
    [InlineData(false, 20 * 1024, 1)]
    [InlineData(true, 20 * 1024, 2)]
    [InlineData(false, 64, 2)]
    public async Task Prerender_SeedsOnlyLoadedForecast(bool prerenderFails, int budget, int loads)
    {
        var api = new CountingForecastApi { Fail = prerenderFails };
        var page = new PageState(); // the seed the prerendered page carries

        var manager = Manager();
        await using (var prerender = Create(api, manager, budget))
        {
            await prerender.InitializeAsync(TestContext.Current.CancellationToken);
            await prerender.Settled();
            await using var renderer = new PageRenderer();
            await manager.PersistStateAsync(page, renderer);
        }

        api.Fail = false;
        var restored = Manager();
        await restored.RestoreStateAsync(page); // before the store starts: the interactive side takes the seed at init
        await using var interactive = Create(api, restored, budget);
        await interactive.InitializeAsync(TestContext.Current.CancellationToken);
        await interactive.Settled();
        interactive.State.Get<ForecastState>().Days.ShouldBe(api.Days);
        interactive.State.Get<ForecastState>().Error.ShouldBeNull();
        interactive.State.WasRestored<ForecastState>().ShouldBe(loads == 1);
        api.Calls.ShouldBe(loads);
    }

    // A real ComponentStatePersistenceManager, registered as Blazor registers it.
    internal static ComponentStatePersistenceManager Manager() =>
        new(NullLogger<ComponentStatePersistenceManager>.Instance, new ServiceCollection().BuildServiceProvider());

    private static TestStore Create(IForecastApi api, ComponentStatePersistenceManager manager, int budget) =>
        TestStore.Create(
            d => App.AddForecast(d).AddBlazor(o => o.PrerenderSeedMaxWireBytes = budget),
            services => services.AddSingleton(api).AddSingleton(manager).AddSingleton(manager.State));

    private sealed class CountingForecastApi : IForecastApi
    {
        public IReadOnlyList<DayForecast> Days { get; } = [new("Mon", 21), new("Tue", 19)];

        public bool Fail { get; set; }

        public int Calls { get; private set; }

        public async Task<IReadOnlyList<DayForecast>> GetAsync(CancellationToken cancellationToken)
        {
            await Task.Yield(); // a real API answers later
            Calls++;
            return Fail ? throw new HttpRequestException("no route to /api/forecast during prerender") : Days;
        }
    }

    // The page's persisted component state: what the prerender pass writes and the interactive side restores.
    internal sealed class PageState : IPersistentComponentStateStore
    {
        private Dictionary<string, byte[]> _state = [];

        public Task<IDictionary<string, byte[]>> GetPersistedStateAsync() => Task.FromResult<IDictionary<string, byte[]>>(_state);

        public Task PersistStateAsync(IReadOnlyDictionary<string, byte[]> state)
        {
            _state = new(state);
            return Task.CompletedTask;
        }

        public bool SupportsRenderMode(IComponentRenderMode renderMode) => true;
    }

    // The prerender pass's renderer: persisting runs the callbacks on its dispatcher; nothing is rendered here.
#pragma warning disable BL0006 // justification: PersistStateAsync takes a Renderer, and the framework's concrete ones are internal
    internal sealed class PageRenderer() : Renderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance)
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        protected override void HandleException(Exception exception) => throw exception;

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
#pragma warning restore BL0006
}

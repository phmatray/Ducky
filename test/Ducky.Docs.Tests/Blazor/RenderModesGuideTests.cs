using Ducky.Blazor;
using Ducky.Testing;
using Microsoft.AspNetCore.Components.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Ducky.Docs.Tests.Blazor;

#region load-rule-state
public sealed record CatalogState(IReadOnlyList<string>? Products);

public sealed record CatalogLoaded(IReadOnlyList<string> Products);

public sealed class CatalogSlice : Slice<CatalogState>
{
    public CatalogSlice() => On<CatalogLoaded>((_, action) => new CatalogState(action.Products));

    public override string Key => "catalog";

    protected override CatalogState Initial => new(Products: null);
}

public interface ICatalogApi
{
    Task<IReadOnlyList<string>> GetProductsAsync(CancellationToken cancellationToken);
}
#endregion

// The snippets of docs/guides/blazor/render-modes.md.
public sealed class RenderModesGuideTests
{
    // Private: AddDuckyGenerated_Ducky_Docs_Tests (the quick start) registers every accessible effect of this assembly.
    private static class App
    {
        #region load-rule-effect
        public sealed class LoadCatalog(ICatalogApi api) : Effect<StoreInitialized>
        {
            public override async Task Handle(StoreInitialized action, EffectContext context, CancellationToken cancellationToken)
            {
                // Storage or the prerender seed already restored it: don't load it again.
                if (context.State.WasRestored<CatalogState>())
                {
                    return;
                }

                context.Dispatch(new CatalogLoaded(await api.GetProductsAsync(cancellationToken)));
            }
        }
        #endregion

        #region load-rule-register
        public static DuckyBuilder AddCatalog(DuckyBuilder ducky) => ducky
            .UseJson(DocsJson.Default)
            .AddSlice<CatalogSlice>()
            .AddEffect<LoadCatalog>()
            .Persist<CatalogSlice>() // restored from localStorage...
            .Prerender<CatalogSlice, CatalogState>(static state => state.Products is not null); // ...or from the prerendered page, once loaded
        #endregion
    }

    // Non-normative (M15-03): the first store loads and persists the catalog; the next one restores it from storage, and
    // the load rule skips the load.
    [Fact]
    public async Task LoadRule_RestoredFromStorage_LoadsOnce()
    {
        var browser = new FakeBrowser();
        var api = new CountingCatalogApi();

        await using (var first = Create(browser, api))
        {
            await first.InitializeAsync(TestContext.Current.CancellationToken);
            await first.Settled();
            first.State.WasRestored<CatalogState>().ShouldBeFalse();
            first.State.Get<CatalogState>().Products.ShouldBe(["duck-yellow", "duck-blue"]);
        } // the dispose flush writes the loaded catalog

        browser.Storage.Keys.ShouldBe([("local", "ducky:catalog")]);
        await using var second = Create(browser, api);
        await second.InitializeAsync(TestContext.Current.CancellationToken);
        await second.Settled();

        second.State.WasRestored<CatalogState>().ShouldBeTrue();
        second.State.Get<CatalogState>().Products.ShouldBe(["duck-yellow", "duck-blue"]);
        api.Calls.ShouldBe(1);
        second.Failures.ShouldBeEmpty();
    }

    // Non-normative (M15-03): the prerender pass seeds a loaded catalog, so the interactive store doesn't load it again;
    // after a failed prerender load the include predicate keeps the initial state out of the seed, and the interactive
    // store loads it. Each store gets its own browser: only the seed carries the catalog over.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task LoadRule_Prerendered_SeedsOnlyLoadedCatalog(bool prerenderFails, int loads)
    {
        var api = new CountingCatalogApi { Fail = prerenderFails };
        var page = new PrerenderGuideTests.PageState();

        var manager = PrerenderGuideTests.Manager();
        await using (var prerender = Create(new FakeBrowser(), api, manager))
        {
            await prerender.InitializeAsync(TestContext.Current.CancellationToken);
            await prerender.Settled();
            await using var renderer = new PrerenderGuideTests.PageRenderer();
            await manager.PersistStateAsync(page, renderer);
        }

        api.Fail = false;
        var restored = PrerenderGuideTests.Manager();
        await restored.RestoreStateAsync(page);
        await using var interactive = Create(new FakeBrowser(), api, restored);
        await interactive.InitializeAsync(TestContext.Current.CancellationToken);
        await interactive.Settled();

        interactive.State.Get<CatalogState>().Products.ShouldBe(["duck-yellow", "duck-blue"]);
        interactive.State.WasRestored<CatalogState>().ShouldBe(loads == 1);
        api.Calls.ShouldBe(loads);
        interactive.Failures.ShouldBeEmpty();
    }

    private static TestStore Create(FakeBrowser browser, ICatalogApi api, ComponentStatePersistenceManager? manager = null) =>
        TestStore.Create(d => App.AddCatalog(d), services =>
        {
            services.AddSingleton<IJSRuntime>(browser).AddSingleton(api);
            if (manager is not null)
            {
                services.AddSingleton(manager).AddSingleton(manager.State);
            }
        });

    private sealed class CountingCatalogApi : ICatalogApi
    {
        public bool Fail { get; set; }

        public int Calls { get; private set; }

        public async Task<IReadOnlyList<string>> GetProductsAsync(CancellationToken cancellationToken)
        {
            await Task.Yield(); // a real API answers later
            Calls++;
            return Fail ? throw new HttpRequestException("no route to /api/products during prerender") : ["duck-yellow", "duck-blue"];
        }
    }
}

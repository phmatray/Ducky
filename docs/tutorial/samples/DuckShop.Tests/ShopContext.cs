using Bunit;
using Ducky;
using Ducky.Generated;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DuckShop.Tests;

// A bUnit context with the shop's store, its fake servers and a fake clock: the components
// render as in the browser, and time moves only when a test advances it.
public abstract class ShopContext : BunitContext
{
    protected ShopContext(ICatalogueApi? catalogue = null)
    {
        // A store change re-renders a component a moment later, on the renderer: room for a
        // busy test machine, while a missed re-render still fails.
        DefaultWaitTimeout = TimeSpan.FromSeconds(10);
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddSingleton(catalogue ?? new FakeCatalogueApi(Time));
        Services.AddSingleton<ISearchApi>(new FakeSearchApi(Time));
        Services.AddDucky(ducky => ducky.AddDuckyGenerated_DuckShop());
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true));
    }

    protected FakeTimeProvider Time { get; } = new();

    protected IStore Store => Services.GetRequiredService<IStore>();

    protected static CancellationToken Cancellation => Xunit.TestContext.Current.CancellationToken;
}

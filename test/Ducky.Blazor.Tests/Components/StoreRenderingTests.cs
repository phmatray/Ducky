using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Tests.Components;

// WhenRenderedAsync (#297): the supported settle for a bUnit-rendered component, so a test asserts on markup without racing
// the change check SubscriptionCore posts to the renderer.
public sealed class StoreRenderingTests : BunitContext
{
    public StoreRenderingTests() => Services.AddDucky(d => d.AddSlice<ItemsSlice>());

    private IStore Store => Services.GetRequiredService<IStore>();

    [Fact]
    public async Task WhenRenderedAsync_AfterDispatch_MarkupShowsNewValue()
    {
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(0, "changed"));
        await Store.WhenRenderedAsync(Renderer.Dispatcher, Xunit.TestContext.Current.CancellationToken);

        cut.Markup.ShouldBe("changed");
        cut.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public async Task WhenRenderedAsync_RepeatedDispatches_AlwaysSettled()
    {
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));

        for (var i = 0; i < 50; i++)
        {
            Store.Dispatch(new SetItem(0, $"v{i}"));
            await Store.WhenRenderedAsync(Renderer.Dispatcher, Xunit.TestContext.Current.CancellationToken);

            cut.Markup.ShouldBe($"v{i}");
        }
    }

    [Fact]
    public async Task WhenRenderedAsync_NullArgument_Throws()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => StoreRenderingExtensions.WhenRenderedAsync(null!, Renderer.Dispatcher));
        await Should.ThrowAsync<ArgumentNullException>(() => Store.WhenRenderedAsync(null!));
    }
}

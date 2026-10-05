using System.Reflection;
using Bunit;
using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Tests.Components;

// SPEC §11.1, §11.2: DuckySelect and StoreSelectionExtensions.Select (CMP-05); INV-19.
public sealed class SelectTests : BunitContext
{
    public SelectTests() =>
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddSlice<ItemsSlice>());

    private IStore Store => Services.GetRequiredService<IStore>();

    private static object? Core<T>(DuckySelect<T> select) =>
        typeof(DuckySelect<T>).GetField("_core", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(select);

    [Fact]
    public async Task DuckySelect_OnlyFragmentRerenders()
    {
        var host = Render<SelectHost>();
        var select = host.FindComponent<DuckySelect<int>>();
        host.Markup.ShouldBe("count: 0");
        var (hostRenders, selectRenders) = (host.Instance.Renders, select.RenderCount);

        Store.Dispatch(new SetItem(0, "other slice"));
        await SettleAsync(host);
        select.RenderCount.ShouldBe(selectRenders);

        Store.Dispatch(new Increment());
        await SettleAsync(host);

        host.Markup.ShouldBe("count: 1");
        select.RenderCount.ShouldBe(selectRenders + 1);
        host.Instance.Renders.ShouldBe(hostRenders);
    }

    [Fact]
    public async Task DuckySelect_SelectorParameterChange_Reevaluates()
    {
        var cut = Render<DuckySelect<string>>(p => p
            .Add(c => c.Selector, static s => s.Get<Items>().Values[0])
            .Add(c => c.ChildContent, static value => value));
        cut.Markup.ShouldBe("a");

        cut.Render(p => p.Add(c => c.Selector, static s => s.Get<Items>().Values[1]));
        cut.Markup.ShouldBe("b");

        // The old selection is gone: a change to item 0 is not a change for what is on screen.
        var renders = cut.RenderCount;
        Store.Dispatch(new SetItem(0, "changed"));
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders);

        Store.Dispatch(new SetItem(1, "z"));
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("z");
    }

    [Fact]
    public async Task DuckySelect_ComparerParameterChange_Resubscribes()
    {
        // (non-normative) A new Comparer replaces the selection like a new Selector does.
        var cut = Render<DuckySelect<int>>(p => p
            .Add(c => c.Selector, static s => s.Get<Counter>().Value)
            .Add(c => c.Comparer, new AlwaysEqual<int>())
            .Add(c => c.ChildContent, static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var renders = cut.RenderCount;
        Store.Dispatch(new Increment());
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders);

        cut.Render(p => p.Add(c => c.Comparer, null));
        cut.Markup.ShouldBe("1");
        renders = cut.RenderCount;
        Store.Dispatch(new Increment());
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("2");
    }

    [Fact]
    public void DuckySelect_SameParameters_KeepsSelection()
    {
        // (non-normative) Re-rendering with the same Selector and Comparer keeps the selection and its store subscription;
        // a re-subscription would read the same, so only the subscription's identity tells them apart.
        Func<StateSnapshot, int> selector = static s => s.Get<Counter>().Value;
        var cut = Render<DuckySelect<int>>(p => p.Add(c => c.Selector, selector));
        var core = Core(cut.Instance);

        cut.Render(p => p.Add(c => c.Selector, selector));

        Core(cut.Instance).ShouldNotBeNull().ShouldBeSameAs(core);
    }

    [Fact]
    public void DuckySelect_NullSelector_Throws() =>
        Should.Throw<ArgumentNullException>(() => Render<DuckySelect<int>>()).ParamName.ShouldBe(nameof(DuckySelect<>.Selector));

    [Fact]
    public async Task DuckySelect_Dispose_NoStateHasChanged()
    {
        var cut = Render<DuckySelect<int>>(p => p.Add(c => c.Selector, static s => s.Get<Counter>().Value));
        var renders = cut.RenderCount;

        cut.Instance.Dispose();
        cut.Instance.Dispose();
        new DuckySelect<int>().Dispose(); // never rendered: nothing to dispose
        Store.Dispatch(new Increment());
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders);
    }

    [Fact]
    public async Task ForeignBase_StoreSelect_Works()
    {
        Renderer.SetRendererInfo(new RendererInfo("Server", isInteractive: true));
        var cut = Render<ForeignCounter>();
        cut.Markup.ShouldBe("0");
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(0, "other slice"));
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders);

        Store.Dispatch(new Increment());
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("1");

        cut.Instance.Dispose();
        Store.Dispatch(new Increment());
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public void StoreSelectionExtensions_NullArgument_Throws()
    {
        // (non-normative)
        var store = Store;
        Func<StateSnapshot, int> selector = static s => s.Get<Counter>().Value;
        Func<Func<Task>, Task> invokeAsync = static work => work();
        Action stateHasChanged = static () => { };
        var rendererInfo = new RendererInfo("Static", isInteractive: false);
        IServiceProvider services = Services;

        Should.Throw<ArgumentNullException>(() => StoreSelectionExtensions.Select(null!, selector, invokeAsync, stateHasChanged, rendererInfo, services)).ParamName.ShouldBe("store");
        Should.Throw<ArgumentNullException>(() => store.Select((Func<StateSnapshot, int>)null!, invokeAsync, stateHasChanged, rendererInfo, services)).ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => store.Select(selector, null!, stateHasChanged, rendererInfo, services)).ParamName.ShouldBe("invokeAsync");
        Should.Throw<ArgumentNullException>(() => store.Select(selector, invokeAsync, null!, rendererInfo, services)).ParamName.ShouldBe("stateHasChanged");
        Should.Throw<ArgumentNullException>(() => store.Select(selector, invokeAsync, stateHasChanged, null!, services)).ParamName.ShouldBe("rendererInfo");
        Should.Throw<ArgumentNullException>(() => store.Select(selector, invokeAsync, stateHasChanged, rendererInfo, null!)).ParamName.ShouldBe("services");
    }

    // The renderer runs work in order: a commit's check, posted before this, has run (and rendered) when it completes.
    private static Task SettleAsync<T>(IRenderedComponent<T> cut)
        where T : IComponent => cut.InvokeAsync(() => { });
}

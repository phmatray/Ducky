using Bunit;
using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Blazor.Tests.Components;

// SPEC §11.1 (components), §11.2 (the subscription core and the registration guard); INV-19, INV-20.
public sealed class DuckyComponentTests : BunitContext
{
    private const string Ducky352 = "DUCKY352";

    public DuckyComponentTests() =>
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddSlice<ItemsSlice>());

    private IStore Store => Services.GetRequiredService<IStore>();

    [Fact]
    public void Component_ParameterChangeWithoutDispatch_ShowsNewItem()
    {
        // The stale-ViewModel regression: the selection reads the parameter when the renderer reads its value.
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        cut.Markup.ShouldBe("a");

        cut.Render(p => p.Add(c => c.Index, 1));

        cut.Markup.ShouldBe("b");
    }

    [Fact]
    public async Task Component_ChangeToOtherItem_NoRerender()
    {
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(1, "changed"));
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders);
        cut.Markup.ShouldBe("a");
    }

    [Fact]
    public async Task Component_ChangeToSelectedItem_Rerenders()
    {
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(0, "changed"));
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("changed");
    }

    [Fact]
    public async Task Component_ParamChangeThenOtherItemChange_NoRerender()
    {
        // Each render read is the baseline ("b" after the parameter change), so a change to item 0 is not a change.
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        cut.Render(p => p.Add(c => c.Index, 1));
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(0, "changed"));
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders);
        cut.Markup.ShouldBe("b");
    }

    [Fact]
    public async Task Component_ParamChangeThenStoreChangeBackToOldValue_Rerenders()
    {
        // The renderer last observed "b" (index 1), not the "a" the drainer last saw at index 0: setting item 1 to "a"
        // is a change for what is on screen.
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));
        cut.Render(p => p.Add(c => c.Index, 1));
        cut.Markup.ShouldBe("b");
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(1, "a"));
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("a");
    }

    [Fact]
    public void Select_AfterFirstRender_Throws()
    {
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));

        var thrown = Should.Throw<InvalidOperationException>(() => cut.Instance.SelectNow());

        thrown.Message.ShouldBe(BlazorErrors.Format(BlazorErrors.SelectAfterFirstRender(typeof(ItemView))));
        thrown.Message.ShouldStartWith($"{Ducky352}: ");
    }

    [Fact]
    public void Select_AfterFirstRender_ThrowsEvenIfOverrideSkipsBase()
    {
        // Registration closes in the re-implemented IHandleAfterRender before either override runs, and each override
        // still receives firstRender.
        var cut = Render<SkipsBase>();
        cut.Render();

        cut.Instance.Seen.ShouldBe(
        [
            "sync True", Ducky352, "async True", Ducky352,
            "sync False", Ducky352, "async False", Ducky352,
        ]);
    }

    [Fact]
    public async Task Dispose_ThenStoreChange_NoStateHasChanged()
    {
        var cut = Render<DisposingView>();
        var renders = cut.RenderCount;

        cut.Instance.Dispose();
        cut.Instance.Dispose();
        Store.Dispatch(new Increment());
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders);
        cut.Instance.Disposals.ShouldBe(2);
    }

    [Fact]
    public async Task Dispose_NotDisposing_KeepsSubscription()
    {
        // Dispose(false) is the finalizer path: it releases no managed state, so the component still re-renders.
        var cut = Render<DisposingView>();
        var renders = cut.RenderCount;

        cut.Instance.DisposeFromFinalizer();
        Store.Dispatch(new Increment());
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public async Task Dispose_AsyncDisposableSubclassCallingDispose_NoStateHasChanged()
    {
        // The renderer calls only DisposeAsync on a component that implements IAsyncDisposable; a subclass that calls
        // Dispose from it releases the subscription, so a later commit runs none of its selectors.
        var host = Render<AsyncDisposableHost>(p => p.Add(c => c.Show, true));
        var view = host.FindComponent<AsyncDisposableView>().Instance;
        var evaluations = view.Evaluations;

        host.Render(p => p.Add(c => c.Show, false));
        Store.Dispatch(new Increment());
        await SettleAsync(host);

        view.DisposedAsync.ShouldBeTrue();
        view.Evaluations.ShouldBe(evaluations);
    }

    [Theory]
    [InlineData("#sync")]
    [InlineData("#async")]
    public async Task Click_DispatchInHandler_RendersOnce(string button)
    {
        // INV-19: ComponentBase renders after the handler, and that render's read is the baseline, so the commit's check
        // finds nothing new: one render per click, for Dispatch and for await DispatchAsync alike.
        var cut = Render<ClickCounter>();
        var renders = cut.RenderCount;

        await cut.Find(button).ClickAsync(new());
        await SettleAsync(cut);

        cut.Find("span").TextContent.ShouldBe("1");
        cut.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public async Task ManyChangedSelections_OneRender()
    {
        // Three commits while the renderer is busy, each changing all four selections: one check, one render.
        var cut = Render<FourCounts>();
        cut.Markup.ShouldBe("0,0,0,1");
        var renders = cut.RenderCount;
        var evaluations = cut.Instance.Evaluations;

        await cut.InvokeAsync(() =>
        {
            Store.Dispatch(new Increment());
            Store.Dispatch(new Increment());
            Store.Dispatch(new Increment());
        });
        await SettleAsync(cut);

        cut.Instance.Evaluations.ShouldBe(evaluations + 2); // the one check, then the render
        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("3,6,-3,4");
    }

    [Fact]
    public async Task DuckyComponentOfT_ConditionalStateAccessAfterFirstRender_Works()
    {
        // State is first read after the first render: its selection was created just before registration closed.
        var cut = Render<ConditionalCounter>(p => p.Add(c => c.Show, false));
        cut.Markup.ShouldBeEmpty();

        // Its baseline is the value at creation: a commit to another slice does not re-render.
        var hidden = cut.RenderCount;
        Store.Dispatch(new SetItem(0, "other slice"));
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(hidden);

        cut.Render(p => p.Add(c => c.Show, true));
        cut.Markup.ShouldBe("0");
        var renders = cut.RenderCount;

        Store.Dispatch(new Increment());
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
        cut.Markup.ShouldBe("1");
    }

    [Fact]
    public async Task DuckyComponentOfT_CommitBeforeAfterRenderHook_StillRerenders()
    {
        // The view rendered 0; the commit lands before its after-render hook, which must not move the baseline past 0.
        var cut = Render(builder =>
        {
            builder.OpenComponent<IncrementsAfterFirstRender>(0);
            builder.CloseComponent();
            builder.OpenComponent<CounterView>(1);
            builder.CloseComponent();
        });
        var view = cut.FindComponent<CounterView>();

        await SettleAsync(view);

        view.Markup.ShouldBe("1");
    }

    [Fact]
    public async Task DuckyComponentOfT_State_IsOneWholeSliceSelection()
    {
        // Dispatch and DispatchAsync go to the injected store; State re-renders on a slice change and not on another one.
        var cut = Render<CounterView>();
        cut.Instance.Injected.ShouldBeSameAs(Store);
        var renders = cut.RenderCount;

        Store.Dispatch(new SetItem(0, "other slice"));
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders);

        (await DispatchBothWhileRendererBusyAsync(cut, cut.Instance.Increment, cut.Instance.IncrementAsync)).ShouldBe(DispatchResult.Reduced);

        cut.Markup.ShouldBe("2");
        cut.RenderCount.ShouldBe(renders + 1);
    }

    [Fact]
    public async Task Layout_SameMembersAsComponent()
    {
        // DuckyLayout: both Select overloads, Dispatch, DispatchAsync, the registration guard and idempotent Dispose.
        var cut = Render<CounterLayout>(p => p.Add(c => c.Body, (RenderFragment)(_ => { })));
        cut.Markup.ShouldBe("0a");
        cut.Instance.Injected.ShouldBeSameAs(Store);

        (await DispatchBothWhileRendererBusyAsync(cut, cut.Instance.Increment, cut.Instance.IncrementAsync)).ShouldBe(DispatchResult.Reduced);
        cut.Markup.ShouldBe("2a");
        Store.Dispatch(new SetItem(0, "z"));
        await SettleAsync(cut);
        cut.Markup.ShouldBe("2z");

        cut.Instance.FirstRenders.ShouldBe([true, true, false, false, false, false]);
        Should.Throw<InvalidOperationException>(() => cut.Instance.SelectNow()).Message
            .ShouldBe(BlazorErrors.Format(BlazorErrors.SelectAfterFirstRender(typeof(CounterLayout))));

        cut.Instance.DisposeFromFinalizer();
        Store.Dispatch(new SetItem(0, "y"));
        await SettleAsync(cut);
        cut.Markup.ShouldBe("2y");

        var renders = cut.RenderCount;
        cut.Instance.Dispose();
        cut.Instance.Dispose();
        Store.Dispatch(new Increment());
        await SettleAsync(cut);
        cut.RenderCount.ShouldBe(renders);
    }

    [Fact]
    public void Select_NullSelector_Throws()
    {
        var component = Render<NullSelectors>().Instance;
        var layout = Render<NullSelectorsLayout>().Instance;

        Should.Throw<ArgumentNullException>(component.SelectSnapshot).ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(component.SelectState).ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(layout.SelectSnapshot).ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(layout.SelectState).ParamName.ShouldBe("selector");
    }

    [Fact]
    public void Select_AfterFirstRender_NamesGenericComponent()
    {
        var cut = Render<Outer.Generic<Dictionary<string, int>>>();

        Should.Throw<InvalidOperationException>(() => cut.Instance.SelectNow()).Message.ShouldContain(
            "Ducky.Blazor.Tests.Components.Outer.Generic<System.Collections.Generic.Dictionary<System.String, System.Int32>>",
            Case.Sensitive);
    }

    [Fact]
    public async Task Component_SelectorThrows_LoggedThroughContainerLogger()
    {
        // The component's subscription core logs through the container's ILogger: a selector that throws in the change
        // check is logged (EventId 2002) and treated as changed, so the component renders again.
        var collector = new FakeLogCollector();
        Services.AddSingleton<ILoggerFactory>(new LoggerFactory([new FakeLoggerProvider(collector)]));
        Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var cut = Render<ArmedView>();
        var renders = cut.RenderCount;

        cut.Instance.Armed = true;
        Store.Dispatch(new SetItem(0, "other slice"));
        await SettleAsync(cut);

        cut.RenderCount.ShouldBe(renders + 1);
        var log = collector.GetSnapshot().Where(record => record.Category == typeof(SubscriptionCore).FullName).ShouldHaveSingleItem();
        (log.Id.Id, log.Level, log.Exception?.Message).ShouldBe((2002, LogLevel.Warning, "selector"));
    }

    [Fact]
    public async Task Component_WithoutLogging_SelectsAndRerenders()
    {
        Services.RemoveAll(typeof(ILogger<>));
        var cut = Render<ItemView>(p => p.Add(c => c.Index, 0));

        Store.Dispatch(new SetItem(0, "changed"));
        await SettleAsync(cut);

        cut.Markup.ShouldBe("changed");
    }

    // Both commits land while the renderer is busy, so they share one check; the result is awaited after the check ran.
    private static async Task<DispatchResult> DispatchBothWhileRendererBusyAsync<T>(
        IRenderedComponent<T> cut, Action dispatch, Func<Task<DispatchResult>> dispatchAsync)
        where T : IComponent
    {
        Task<DispatchResult> result = null!;
        await cut.InvokeAsync(() =>
        {
            dispatch();
            result = dispatchAsync();
        });
        await SettleAsync(cut);
        return await result;
    }

    // The renderer runs work in order: a commit's check, posted before this, has run (and rendered) when it completes.
    private static Task SettleAsync<T>(IRenderedComponent<T> cut)
        where T : IComponent => cut.InvokeAsync(() => { });
}

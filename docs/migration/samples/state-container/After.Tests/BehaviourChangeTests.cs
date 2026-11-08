using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace StateContainerSample.After.Tests;

// The behaviour changes of docs/migration/state-container.md, one test each (SPEC §22.9), on the After app.
public sealed class BehaviourChangeTests
{
    #region no-lost-updates
    [Fact]
    public async Task ConcurrentIncrements_NoneIsLost()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterStore>());
        var counter = store.GetSlice<CounterStore>();

        // Count += amount on a plain property is a read and a write: two threads can both read 0 and both write 1.
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 250; i++)
            {
                counter.Increment(1);
            }
        })));
        await store.Settled();

        counter.State.Count.ShouldBe(2000); // each mutator ran at reduce time, one at a time, against the latest state
    }
    #endregion

    #region load-rule
    [Fact]
    public async Task RestoredSlice_IsNotLoadedAgain()
    {
        var api = new FakeCounterApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterStore>().AddEffect<LoadCountOnStart>().AddEffect<LoadCountEffect>(),
            services => services.AddSingleton<ICounterApi>(api));

        store.Seed(new CounterState(Count: 12, IsLoading: false, Error: null)); // as the prerender seed restores it
        await store.Settled();

        api.Calls.ShouldBe(0);
        store.State.Get<CounterState>().Count.ShouldBe(12);
    }
    #endregion
}

public sealed class RenderBehaviourTests : BunitContext
{
    private readonly FakeCounterApi _api = new();

    public RenderBehaviourTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Registration.AddApp(Services, _api);
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    #region off-thread-change
    [Fact]
    public async Task ChangeFromAnotherThread_RerendersWithoutInvokeAsync()
    {
        var view = Render<CounterView>();
        await _api.Called(0);
        _api.Complete(0, 1);
        await Services.GetRequiredService<IStore>().WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
        view.WaitForAssertion(() => view.Markup.ShouldBe("1"));

        var counter = Services.GetRequiredService<CounterStore>();
        await Task.Run(() => counter.Increment(1), Xunit.TestContext.Current.CancellationToken); // a thread-pool thread

        view.WaitForAssertion(() => view.Markup.ShouldBe("2")); // DuckyComponent marshalled its own re-render
    }
    #endregion

    #region selected-rerender
    [Fact]
    public async Task Selection_RerendersOnlyWhenTheSelectedValueChanges()
    {
        var badge = Render<LoadingBadge>();
        await _api.Called(0);
        _api.Complete(0, 1);
        await Services.GetRequiredService<IStore>().WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
        badge.WaitForAssertion(() => badge.Markup.ShouldBe("idle"));
        var renders = badge.Instance.Renders;

        await badge.InvokeAsync(() => Services.GetRequiredService<CounterStore>().Increment(5));
        await Services.GetRequiredService<IStore>().WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);

        badge.Instance.Renders.ShouldBe(renders); // OnChange would have re-rendered every subscriber

        await badge.InvokeAsync(() => Services.GetRequiredService<IStore>().Dispatch(new LoadCount()));
        badge.WaitForAssertion(() => badge.Markup.ShouldBe("busy")); // IsLoading changed: re-rendered
        badge.Instance.Renders.ShouldBe(renders + 1); // once, for IsLoading: the Increment above added no render
    }
    #endregion
}

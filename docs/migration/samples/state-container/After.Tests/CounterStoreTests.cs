using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace StateContainerSample.After.Tests;

// The migrated app keeps the container app's behaviour (SPEC §22.9).
public sealed class CounterStoreTests
{
    #region after-test
    [Fact]
    public async Task Increment_AddsAmount()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterStore>());
        var counter = store.GetSlice<CounterStore>();

        counter.Increment(2);
        await store.Settled();

        counter.State.Count.ShouldBe(2);
    }

    [Fact]
    public async Task LoadCountEffect_ApiAnswers_SetsCount()
    {
        var api = new FakeCounterApi();
        CounterStore? counter = null;
        var running = EffectTest.Run<LoadCountEffect, LoadCount>(
            new LoadCount(),
            ducky => ducky.AddSlice<CounterStore>(),
            store => counter = store.GetSlice<CounterStore>(),
            services: s => s.AddSingleton<ICounterApi>(api));
        await api.Called(0);
        api.Complete(0, 42);

        await running;

        counter!.State.ShouldBe(new CounterState(Count: 42, IsLoading: false, Error: null));
    }
    #endregion

    [Fact]
    public async Task LoadCountEffect_HttpFailure_SetsError()
    {
        var api = new FakeCounterApi();
        CounterStore? counter = null;
        var running = EffectTest.Run<LoadCountEffect, LoadCount>(
            new LoadCount(),
            ducky => ducky.AddSlice<CounterStore>(),
            store => counter = store.GetSlice<CounterStore>(),
            services: s => s.AddSingleton<ICounterApi>(api));
        await api.Called(0);
        api.Fail(0, new HttpRequestException("offline"));

        await running;

        counter!.State.ShouldBe(new CounterState(Count: 0, IsLoading: false, Error: "offline"));
        api.Token(0).CanBeCanceled.ShouldBeTrue(); // the effect passed its run token
    }

    [Fact]
    public async Task Start_LoadsOnce()
    {
        var api = new FakeCounterApi();
        await using var provider = Registration.AddApp(new ServiceCollection(), api).BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        await store.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
        await api.Called(0);
        api.Complete(0, 3);
        await store.WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);

        store.State.Get<CounterState>().ShouldBe(new CounterState(Count: 3, IsLoading: false, Error: null));
        api.Calls.ShouldBe(1);
    }
}

public sealed class CounterViewTests : BunitContext
{
    private readonly FakeCounterApi _api = new();

    public CounterViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Registration.AddApp(Services, _api);
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    private IStore Store => Services.GetRequiredService<IStore>();

    [Fact]
    public async Task Load_Increment_Refresh_Rerender()
    {
        var view = Render<CounterView>();
        await _api.Called(0);
        view.WaitForAssertion(() => view.Markup.ShouldBe("loading"));

        _api.Complete(0, 2);
        await Store.WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
        view.WaitForAssertion(() => view.Markup.ShouldBe("2"));

        await view.InvokeAsync(view.Instance.Increment);
        view.WaitForAssertion(() => view.Markup.ShouldBe("3"));

        await view.InvokeAsync(view.Instance.Refresh);
        view.WaitForAssertion(() => view.Markup.ShouldBe("loading"));
        await _api.Called(1);
        _api.Complete(1, 8);
        await Store.WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
        view.WaitForAssertion(() => view.Markup.ShouldBe("8"));
    }
}

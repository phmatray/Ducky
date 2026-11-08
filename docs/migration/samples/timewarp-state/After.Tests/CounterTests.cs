using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace TimeWarpSample.After.Tests;

// The migrated app keeps the TimeWarp.State app's behaviour (SPEC §22.8).
public sealed class CounterTests
{
    #region after-test
    [Fact]
    public void IncrementCount_AddsAmount() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 3, IsLoading: false, Error: null))
            .When(new IncrementCount(2))
            .Then(new CounterState(Count: 5, IsLoading: false, Error: null));

    [Fact]
    public async Task LoadCountEffect_ApiAnswers_DispatchesCountLoaded()
    {
        var api = new FakeCounterApi();
        var running = EffectTest.Run<LoadCountEffect, LoadCount>(new LoadCount(), services: s => s.AddSingleton<ICounterApi>(api));
        api.Complete(0, 42);

        var run = await running;

        run.Dispatched.ShouldBe([new CountLoaded(42)]);
    }
    #endregion

    [Fact]
    public void LoadCount_SetsLoading_ClearsError() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 1, IsLoading: false, Error: "offline"))
            .When(new LoadCount())
            .Then(new CounterState(Count: 1, IsLoading: true, Error: null));

    [Fact]
    public void CountLoaded_SetsCount() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 1, IsLoading: true, Error: null))
            .When(new CountLoaded(9))
            .Then(new CounterState(Count: 9, IsLoading: false, Error: null));

    [Fact]
    public void CountLoadFailed_KeepsCount_SetsError() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 1, IsLoading: true, Error: null))
            .When(new CountLoadFailed("offline"))
            .Then(new CounterState(Count: 1, IsLoading: false, Error: "offline"));

    [Fact]
    public async Task LoadCountEffect_HttpFailure_DispatchesCountLoadFailed()
    {
        var api = new FakeCounterApi();
        var running = EffectTest.Run<LoadCountEffect, LoadCount>(new LoadCount(), services: s => s.AddSingleton<ICounterApi>(api));
        api.Fail(0, new HttpRequestException("offline"));

        var run = await running;

        run.Dispatched.ShouldBe([new CountLoadFailed("offline")]);
        api.Token(0).CanBeCanceled.ShouldBeTrue(); // the effect passed its run token
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

    [Fact]
    public async Task Load_ThenIncrement_Rerenders()
    {
        var view = Render<CounterView>();
        view.WaitForAssertion(() => view.Markup.ShouldBe("loading"));
        await _api.Called(0);

        _api.Complete(0, 2);
        await Services.GetRequiredService<IStore>().WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
        view.WaitForAssertion(() => view.Markup.ShouldBe("2"));

        var result = await view.InvokeAsync(view.Instance.IncrementAsync);

        result.ShouldBe(DispatchResult.Reduced);
        view.WaitForAssertion(() => view.Markup.ShouldBe("3"));
    }
}

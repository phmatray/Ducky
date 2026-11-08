using Bunit;
using Ducky;
using Ducky.Generated;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace FluxorSample.After.Tests;

// The migrated app keeps the Fluxor app's behaviour (SPEC §22.7).
public sealed class CounterTests
{
    [Fact]
    public void Increment_AddsAmount() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 3, IsLoading: false, Error: null))
            .When(new IncrementCounterAction(2))
            .Then(new CounterState(Count: 5, IsLoading: false, Error: null));

    [Fact]
    public void Load_SetsLoading_ClearsError() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 0, IsLoading: false, Error: "offline"))
            .When(new LoadCounterAction())
            .Then(new CounterState(Count: 0, IsLoading: true, Error: null));

    [Fact]
    public void LoadResult_SetsCount() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 0, IsLoading: true, Error: null))
            .When(new LoadCounterResultAction(7))
            .Then(new CounterState(Count: 7, IsLoading: false, Error: null));

    [Fact]
    public void LoadFailed_KeepsCount_SetsError() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(Count: 4, IsLoading: true, Error: null))
            .When(new LoadCounterFailedAction("offline"))
            .Then(new CounterState(Count: 4, IsLoading: false, Error: "offline"));

    [Fact]
    public async Task LoadEffect_ApiAnswers_DispatchesResult()
    {
        var api = new FakeCounterApi();
        var running = EffectTest.Run<LoadCounterEffect, LoadCounterAction>(
            new LoadCounterAction(),
            services: s => s.AddSingleton<ICounterApi>(api));
        api.Complete(0, 42);

        var run = await running;

        run.Dispatched.ShouldBe([new LoadCounterResultAction(42)]);
        run.Failures.ShouldBeEmpty();
        api.Token(0).CanBeCanceled.ShouldBeTrue(); // the effect passed its run token
    }

    [Fact]
    public async Task LoadEffect_HttpFailure_DispatchesFailed()
    {
        var api = new FakeCounterApi();
        var running = EffectTest.Run<LoadCounterEffect, LoadCounterAction>(
            new LoadCounterAction(),
            services: s => s.AddSingleton<ICounterApi>(api));
        api.Fail(0, new HttpRequestException("offline"));

        var run = await running;

        run.Dispatched.ShouldBe([new LoadCounterFailedAction("offline")]);
    }

    [Fact]
    public async Task Start_LoadsCount_LogsActions()
    {
        var api = new FakeCounterApi();
        await using var provider = Registration.AddApp(new ServiceCollection(), api).BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        await store.InitializeAsync(Xunit.TestContext.Current.CancellationToken); // StoreInitialized starts the first load
        await api.Called(0);
        store.State.Get<CounterState>().IsLoading.ShouldBeTrue();
        api.Complete(0, 9);
        await store.WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);

        store.State.Get<CounterState>().ShouldBe(new CounterState(Count: 9, IsLoading: false, Error: null));
        provider.GetRequiredService<ActionLog>().Entries.ShouldContain(nameof(LoadCounterResultAction));
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
    public void Load_ThenIncrement_Rerenders()
    {
        var view = Render<CounterView>();
        var badge = Render<LoadingBadge>();
        view.WaitForAssertion(() => view.Markup.ShouldBe("loading"));
        badge.Markup.ShouldBe("busy");

        _api.Complete(0, 2);
        view.WaitForAssertion(() => view.Markup.ShouldBe("2"));
        badge.WaitForAssertion(() => badge.Markup.ShouldBe("idle"));

        view.InvokeAsync(view.Instance.Increment);
        view.WaitForAssertion(() => view.Markup.ShouldBe("3"));
    }

    [Fact]
    public async Task Refresh_ReturnsReducedOnceLoadingIsCommitted()
    {
        var view = Render<CounterView>();
        _api.Complete(0, 2);
        view.WaitForAssertion(() => view.Markup.ShouldBe("2"));

        var result = await view.InvokeAsync(view.Instance.RefreshAsync);

        result.ShouldBe(DispatchResult.Reduced);
        view.WaitForAssertion(() => view.Markup.ShouldBe("loading"));
        _api.Complete(1, 8);
        await Services.GetRequiredService<IStore>().WhenIdleAsync(Xunit.TestContext.Current.CancellationToken); // the effect resumes on the renderer
        view.WaitForAssertion(() => view.Markup.ShouldBe("8"));
    }
}

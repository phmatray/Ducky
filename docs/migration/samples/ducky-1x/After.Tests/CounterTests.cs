using Bunit;
using Ducky;
using Ducky.Generated;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;

namespace Ducky1x.After.Tests;

// The migrated app keeps the Before app's behaviour.
public sealed class CounterTests
{
    [Fact]
    public void Increment_AddsAmount() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(3))
            .When(new Increment(2))
            .Then(new CounterState(5));

    [Fact]
    public void Reset_RestoresInitial() =>
        SliceTest.For<CounterSlice, CounterState>()
            .Given(new CounterState(7))
            .When(new Reset())
            .Then(new CounterState(0));

    [Theory]
    [InlineData(10, 10)]
    [InlineData(11, 0)]
    public async Task Increment_AboveTen_EffectResets(int amount, int expected)
    {
        await using var store = TestStore.Create(ducky => ducky.AddDuckyGenerated_Ducky1x_After().UseJson(AppJson.Default));

        store.Increment(amount);
        await store.Settled();

        Registration.ReadCount(store).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public async Task IsPositive_FollowsValue(int value, bool expected)
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>());
        store.Seed(new CounterState(value));

        CounterSelectors.IsPositive.Create()(store.State).ShouldBe(expected);
    }
}

public sealed class CounterViewTests : BunitContext
{
    public CounterViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Registration.AddApp(Services);
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    [Fact]
    public async Task Increment_RendersNewValue_InitializedOnce()
    {
        var view = Render<CounterView>();
        view.Markup.ShouldBe("0");
        view.Instance.IsPositive.ShouldBeFalse();

        await view.InvokeAsync(view.Instance.Increment);

        view.WaitForAssertion(() => view.Markup.ShouldBe("2")); // re-rendered: State alone already reads 2
        view.Instance.IsPositive.ShouldBeTrue();
        view.Instance.Initializations.ShouldBe(1);
    }
}

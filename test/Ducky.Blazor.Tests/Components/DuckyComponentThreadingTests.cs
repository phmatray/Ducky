using Bunit;
using Ducky.Blazor.Tests.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Blazor.Tests.Components;

// SPEC §11.2, INV-19: a store change re-renders a component through the renderer's real dispatcher (bUnit uses Blazor's
// Dispatcher.CreateDefault(), as Blazor Server does), and leaves every caller off the renderer. A check posted with no
// ExecutionContext ran inline on the thread that completed the renderer's work item and left it on the renderer's
// context: the test thread then passed CheckAccess() outside the renderer's queue, so bUnit's calls and an effect it
// resumed ran concurrently with renders on other threads, a wait missed the render it waited for, and two renders at once
// could drop one for good (the docs tutorial's flaky badge, BLAZOR-RERENDER).
public sealed class DuckyComponentThreadingTests : BunitContext
{
    public DuckyComponentThreadingTests()
    {
        Services.AddSingleton<TimeProvider>(Time);
        Services.AddDucky(d => d.AddSlice<CounterSlice>().AddSlice<TimerSlice>().AddEffect<TimerEffect>());
    }

    private FakeTimeProvider Time { get; } = new();

    [Fact]
    public async Task Click_DispatchInHandler_TestThreadLeftOffRenderer()
    {
        var badge = Render<CounterView>();
        var buttons = Render<ClickCounter>();

        await buttons.Find("#sync").ClickAsync(new());

        badge.Markup.ShouldBe("1");
        SynchronizationContext.Current.ShouldBeNull();
        Renderer.Dispatcher.CheckAccess().ShouldBeFalse();
    }

    [Fact]
    public void BlockingClick_DispatchingTwice_OtherComponentRerendersEachTime()
    {
        // The tutorial's badge: bUnit's blocking Click() on a button whose handler dispatches, twice, and another
        // component that selects the count re-renders once for each.
        var badge = Render<CounterView>();
        var buttons = Render<ClickCounter>();
        var renders = badge.RenderCount;

        buttons.Find("#sync").Click();
        badge.WaitForAssertion(() => badge.Markup.ShouldBe("1"));
        buttons.Find("#sync").Click();
        badge.WaitForAssertion(() => badge.Markup.ShouldBe("2"));

        badge.RenderCount.ShouldBe(renders + 2);
    }

    [Fact]
    public async Task Effect_ClockAdvancedOnTestThreadAfterClick_Rerenders()
    {
        // The tutorial's search box: the click starts an effect that waits on the fake clock (its await captured the
        // renderer's context), and the test thread moves the clock: the effect resumes on the renderer, dispatches, and
        // the component re-renders, seen by bUnit's wait.
        var cut = Render<TimerView>();
        await cut.Find("button").ClickAsync(new());
        await cut.WaitForAssertionAsync(() => cut.Find("span").TextContent.ShouldBe("running"));

        Time.Advance(TimerEffect.Delay);

        await cut.WaitForAssertionAsync(() => cut.Find("span").TextContent.ShouldBe("1"));
    }

    [Fact]
    public async Task Effect_ClockAdvancedInsideRenderer_Rerenders()
    {
        var cut = Render<TimerView>();
        await cut.Find("button").ClickAsync(new());

        await cut.InvokeAsync(() => Time.Advance(TimerEffect.Delay));

        await cut.WaitForAssertionAsync(() => cut.Find("span").TextContent.ShouldBe("1"));
    }
}

internal sealed record StartTimer;

internal sealed record TimerFired;

internal sealed record TimerState(bool Running, int Fired);

internal sealed class TimerSlice : Slice<TimerState>
{
    public TimerSlice()
    {
        On<StartTimer>(state => state with { Running = true });
        On<TimerFired>(state => new(Running: false, state.Fired + 1));
    }

    protected override TimerState Initial => new(Running: false, Fired: 0);
}

// Fires once the clock has moved by Delay after StartTimer.
internal sealed class TimerEffect : Effect<StartTimer>
{
    public static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    public override async Task Handle(StartTimer action, EffectContext context, CancellationToken cancellationToken)
    {
        await Task.Delay(Delay, context.Time, cancellationToken);
        context.Dispatch(new TimerFired());
    }
}

// A button that starts the timer, and the timer's state.
internal sealed class TimerView : DuckyComponent<TimerState>
{
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "button");
        builder.AddAttribute(1, "onclick", EventCallback.Factory.Create(this, () => Dispatch(new StartTimer())));
        builder.CloseElement();
        builder.OpenElement(2, "span");
        builder.AddContent(3, State.Running ? "running" : State.Fired.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.CloseElement();
    }
}

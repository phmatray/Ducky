using Bunit;
using Ducky;
using Ducky.Testing;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorMvu.After.Tests;

internal sealed class FakeCounterApi(Func<int> count) : ICounterApi
{
    public Task<int> GetCountAsync(CancellationToken cancellationToken) => Task.FromResult(count());
}

// The first call waits on First (or its token); the second returns 5 at once.
internal sealed class GatedCounterApi : ICounterApi
{
    private int _calls;

    public TaskCompletionSource<int> First { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource FirstCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource SecondCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public CancellationToken FirstToken { get; private set; }

    public Task<int> GetCountAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Increment(ref _calls) == 1)
        {
            FirstToken = cancellationToken;
            FirstCalled.SetResult();
            return First.Task.WaitAsync(cancellationToken);
        }

        SecondCalled.SetResult();
        return Task.FromResult(5);
    }
}

// The migrated app keeps the BlazorMVU component's behaviour: each Update case is one On<Msg> (SPEC §22.4).
public sealed class CounterTests
{
    private static readonly CounterState _zero = new(Count: 0, IsLoading: false, Error: null, Seconds: 0);

    public static TheoryData<object, CounterState, CounterState> Messages => new()
    {
        { new Increment(), _zero with { Count = 4 }, _zero with { Count = 5 } },
        { new Decrement(), _zero with { Count = 4 }, _zero with { Count = 3 } },
        { new CountLoading(), _zero with { Error = "old" }, _zero with { IsLoading = true } },
        { new CountLoaded(9), _zero with { IsLoading = true }, _zero with { Count = 9 } },
        { new CountFailed("down"), _zero with { IsLoading = true, Count = 4 }, _zero with { Count = 4, Error = "down" } },
        { new Tick(), _zero with { Seconds = 2 }, _zero with { Seconds = 3 } },
    };

    [Theory]
    [MemberData(nameof(Messages))]
    public void Update_EachMessage_OneReducer(object message, CounterState given, CounterState expected) =>
        SliceTest.For<CounterSlice, CounterState>().Given(given).When(message).Then(expected);

    [Fact]
    public void Initial_IsZero() => SliceTest.For<CounterSlice, CounterState>().When().Then(_zero);

    [Fact]
    public async Task LoadCount_Succeeds_StartedThenLoaded()
    {
        var run = await EffectTest.Run<LoadCountEffect, LoadCount>(
            new LoadCount(), services: s => s.AddSingleton<ICounterApi>(new FakeCounterApi(() => 5)));

        run.Dispatched.ShouldBe([new CountLoading(), new CountLoaded(5)]);
    }

    [Fact]
    public async Task LoadCount_Throws_StartedThenFailed()
    {
        var run = await EffectTest.Run<LoadCountEffect, LoadCount>(
            new LoadCount(), services: s => s.AddSingleton<ICounterApi>(new FakeCounterApi(() => throw new HttpRequestException("down"))));

        run.Dispatched.ShouldBe([new CountLoading(), new CountFailed("down")]);
        run.Failures.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadCount_Twice_SwitchCancelsTheFirst()
    {
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        var api = new GatedCounterApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<LoadCountEffect>(), s => s.AddSingleton<ICounterApi>(api));
        await store.InitializeAsync(cancellationToken);

        // Per-step progress bounds: the second LoadCount arrives while the first run waits on its call.
        store.Dispatch(new LoadCount());
        await api.FirstCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        store.Dispatch(new LoadCount());
        await api.SecondCalled.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        api.First.SetResult(9); // a first run that was not superseded would now load 9
        await store.Settled();

        api.FirstToken.IsCancellationRequested.ShouldBeTrue();
        store.Processed.Select(context => context.Action).OfType<CountLoaded>().ShouldBe([new CountLoaded(5)]);
        store.State.Get<CounterState>().ShouldBe(_zero with { Count = 5 });
    }

    [Fact]
    public void Clock_Interval_IsTheSubEveryPeriod() => ClockEffect.Interval.ShouldBe(TimeSpan.FromSeconds(1));

    [Fact]
    public async Task Clock_TicksOncePerInterval()
    {
        const int intervals = 3;
        var cancellationToken = Xunit.TestContext.Current.CancellationToken;
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>().AddEffect<ClockEffect>());
        using var reduced = new SemaphoreSlim(0);
        using var seconds = store.Select(state => state.Get<CounterState>().Seconds, _ => reduced.Release());
        await store.InitializeAsync(cancellationToken);
        await store.Settled(); // the drain that ran StoreInitialized has exited, so the clock's timer is armed

        for (var advanced = 1; advanced <= intervals; advanced++)
        {
            store.Time.Advance(ClockEffect.Interval);

            // A per-step progress bound: the Tick is dispatched on whichever thread resumes the timer's wait.
            (await reduced.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)).ShouldBeTrue($"Tick {advanced} was never reduced.");
        }

        // One tick per full interval, never one more: the barrier is the next on-time tick, not Settled (which does not
        // wait for a LongRunning effect), so an extra tick shows up as a second release or a count past intervals + 1.
        store.Time.Advance(ClockEffect.Interval - TimeSpan.FromTicks(1));
        store.Time.Advance(TimeSpan.FromTicks(1));
        (await reduced.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken)).ShouldBeTrue("The on-time tick was never reduced.");
        seconds.Value.ShouldBe(intervals + 1);
        reduced.CurrentCount.ShouldBe(0);
        store.Failures.ShouldBeEmpty();
    }
}

public sealed class CounterViewTests : BunitContext
{
    public CounterViewTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<ICounterApi>(new FakeCounterApi(() => 41));
        Registration.AddApp(Services);
        Renderer.SetRendererInfo(new RendererInfo("WebAssembly", isInteractive: true)); // AddBlazor reads the render mode
    }

    [Fact]
    public async Task ReloadThenIncrement_RendersCount()
    {
        var view = Render<Counter>();
        view.Markup.ShouldBe("0");

        await view.InvokeAsync(view.Instance.Reload);
        view.WaitForAssertion(() => view.Markup.ShouldBe("41"));
        await view.InvokeAsync(view.Instance.Increment);

        view.WaitForAssertion(() => view.Markup.ShouldBe("42"));
    }
}

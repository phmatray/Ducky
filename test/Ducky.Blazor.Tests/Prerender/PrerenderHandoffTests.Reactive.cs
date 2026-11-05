using Bunit;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Interactivity;
using Ducky.Reactive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using R3;

namespace Ducky.Blazor.Tests.Prerender;

// SPEC §11.4, §14 (INV-15, INV-27): quiescence excludes Ducky.Reactive work, so the seed never waits for a reactive load.
public sealed partial class PrerenderHandoffTests
{
    [Fact]
    public async Task Prerender_ReactiveLoadInFlight_Documented()
    {
        // Why a Prerender<T> slice must be loaded by an Effect<T>. The idle timeout runs on fake time, which does not advance
        // before the persist completes: a seed that waited for the pipeline would never complete.
        static void Setup(DuckyBuilder d) => d.Prerender<CounterSlice>().AddReactiveEffect<ReactiveLoadCounter>();
        var time = new FakeTimeProvider();
        await using var side = new BunitContext();
        Configure(side, _static, Setup, _loads, time: time);
        side.Render<CounterView>().Markup.ShouldBe("0");

        // The persist completes with fake time frozen, so nothing waited; the seed captures the loading state of the load in flight.
        var seed = new FakeComponentStateStore();
        await Persist(side, seed).WaitAsync(TimeSpan.FromSeconds(10), Ct);
        _loads.Count.ShouldBe(1);
        Slices(seed).GetProperty(new CounterSlice().Key).GetProperty("value").GetInt32().ShouldBe(0);

        // The load lands, too late for the seed.
        var prerendered = side.Services.GetRequiredService<IStore>();
        time.Advance(ReactiveLoadCounter.Latency);
        await prerendered.WhenIdleAsync(Ct);
        prerendered.State.Get<Counter>().Value.ShouldBe(1);

        // The interactive side restores the loading state as loaded, and its load rule never loads it again: stuck.
        await InteractiveAsync(seed, Setup);
        Render<CounterView>().Markup.ShouldBe("0");
        Store.State.WasRestored<Counter>().ShouldBeTrue();
        await Store.WhenIdleAsync(Ct);
        _loads.Count.ShouldBe(1);
    }
}

// The load rule of SlowLoadCounter as a pipeline; Count is the loads started, each in flight for an hour of the TimeProvider.
internal sealed class ReactiveLoadCounter(Loads loads, TimeProvider time) : ReactiveEffect
{
    public static readonly TimeSpan Latency = TimeSpan.FromHours(1);

    public override Observable<object> Handle(Observable<object> actions, Observable<StateSnapshot> state) =>
        actions.OfActionType<StoreInitialized>()
            .WithLatestFrom(state, static (_, snapshot) => snapshot)
            .Where(static snapshot => !snapshot.WasRestored<Counter>())
            .SelectMany(_ =>
            {
                loads.Count++;
                return Observable.Timer(Latency, time);
            })
            .Select(static object (_) => new Increment());
}

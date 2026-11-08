using Ducky;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TimeWarpSample.After.Tests;

// The behaviour changes of docs/migration/timewarp-state.md, one test each (SPEC §22.8), on the After app's slice and
// effect and fixtures of their own.
public sealed class BehaviourChangeTests
{
    private static class Fixtures
    {
        public sealed record Explode;

        public sealed record Fragile(int Count);

        public sealed class FragileSlice : Slice<Fragile>
        {
            public FragileSlice() => On<IncrementCount>((_, _) => throw new InvalidOperationException("boom"));

            protected override Fragile Initial => new(0);
        }

        public sealed class ExplodeEffect : Effect<Explode>
        {
            public override Task Handle(Explode action, EffectContext context, CancellationToken cancellationToken) =>
                throw new InvalidOperationException("effect boom");
        }
    }

    #region immutable-state
    [Fact]
    public async Task Reducer_ReturnsANewState_EarlierSnapshotsNeverChange()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>());
        var snapshot = store.State; // the whole store's state, as a component or an effect reads it

        await store.IncrementCountAsync(5);

        // The store committed a new snapshot instead of changing the one held: nothing to clone, nothing to roll back.
        snapshot.Get<CounterState>().Count.ShouldBe(0);
        store.State.ShouldNotBeSameAs(snapshot);
        store.State.Version.ShouldBe(snapshot.Version + 1);
        store.State.Get<CounterState>().Count.ShouldBe(5);
    }
    #endregion

    #region intermediate-state
    [Fact]
    public async Task LoadCount_IsReducedBeforeTheApiAnswers()
    {
        var api = new FakeCounterApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<LoadCountEffect>(),
            services => services.AddSingleton<ICounterApi>(api));

        var seen = new List<bool>();
        using var isLoading = store.Select(s => s.Get<CounterState>().IsLoading, seen.Add);

        var result = await store.LoadCountAsync(); // completes after the reduce, not after the effect

        result.ShouldBe(DispatchResult.Reduced);
        store.State.Get<CounterState>().IsLoading.ShouldBeTrue();
        seen.ShouldBe([true]); // the subscriber was notified while the API call is still pending
        api.Complete(0, 7);
        await store.Settled(); // wait for the effect too
        store.State.Get<CounterState>().ShouldBe(new CounterState(Count: 7, IsLoading: false, Error: null));
    }
    #endregion

    #region effect-switch
    [Fact]
    public async Task SwitchEffect_NewerLoad_CancelsAndDropsTheOlderRun()
    {
        var api = new FakeCounterApi(honourCancellation: false); // an API that ignores the token and answers late
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<LoadCountEffect>(),
            services => services.AddSingleton<ICounterApi>(api));

        store.LoadCount();
        store.LoadCount();
        api.Token(0).IsCancellationRequested.ShouldBeTrue();

        api.Complete(1, 5);
        api.Complete(0, 99); // the superseded run answers late: its dispatch is dropped
        await store.Settled();

        store.State.Get<CounterState>().Count.ShouldBe(5);
    }
    #endregion

    #region failures
    [Fact]
    public async Task Failures_AreActions_NeverSwallowed()
    {
        await using var store = TestStore.Create(ducky => ducky
            .AddSlice<CounterSlice>()
            .AddSlice<Fixtures.FragileSlice>()
            .AddEffect<Fixtures.ExplodeEffect>());

        var result = await store.IncrementCountAsync(5); // FragileSlice throws for IncrementCount

        result.ShouldBe(DispatchResult.Failed);
        store.State.Get<CounterState>().Count.ShouldBe(0); // nothing committed, in any slice
        store.Failures.ShouldHaveSingleItem().ShouldBeOfType<ReducerFailed>().Exception.Message.ShouldBe("boom");

        store.Dispatch(new Fixtures.Explode());
        await store.Settled();
        store.Failures[^1].ShouldBeOfType<EffectFailed>().Exception.Message.ShouldBe("effect boom");
    }
    #endregion
}

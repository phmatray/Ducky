using Ducky;
using Ducky.Generated;
using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FluxorSample.After.Tests;

// The behaviour changes of docs/migration/fluxor.md, one test each (SPEC §22.7), on the After app's slice and effects and
// fixtures of their own.
public sealed class BehaviourChangeTests
{
    private static class Fixtures
    {
        public record Notify(string Text);

        public sealed record UrgentNotify(string Text) : Notify(Text);

        public sealed record Inbox(string? Last);

        public sealed class InboxSlice : Slice<Inbox>
        {
            public InboxSlice() => On<Notify>((_, action) => new Inbox(action.Text));

            protected override Inbox Initial => new(Last: null);
        }
    }

    #region dispatch-result
    [Fact]
    public async Task DispatchAsync_CompletesOnceReduced_WithItsResult()
    {
        await using var store = TestStore.Create(ducky => ducky.AddSlice<CounterSlice>());

        var result = await store.DispatchAsync(new IncrementCounterAction(2)); // Fluxor's Dispatch returns void

        result.ShouldBe(DispatchResult.Reduced);
        store.State.Get<CounterState>().Count.ShouldBe(2); // read-your-writes, before any effect finished
    }
    #endregion

    #region effect-switch
    [Fact]
    public async Task SwitchEffect_NewerLoad_CancelsAndDropsTheOlderRun()
    {
        var api = new FakeCounterApi(honourCancellation: false); // an API that ignores the token and answers late
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<LoadCounterEffect>(),
            services => services.AddSingleton<ICounterApi>(api));

        store.Dispatch(new LoadCounterAction());
        store.Dispatch(new LoadCounterAction()); // Fluxor would run both loads to completion
        api.Token(0).IsCancellationRequested.ShouldBeTrue();

        api.Complete(1, 5);
        api.Complete(0, 99); // the superseded run answers late: its dispatch is dropped
        await store.Settled();

        store.State.Get<CounterState>().Count.ShouldBe(5);
    }
    #endregion

    #region exact-type
    [Fact]
    public async Task Reducer_MatchesTheExactActionType_NotSubtypes()
    {
        await using var store = TestStore.Create(ducky =>
        {
            ducky.ThrowOnUnhandledAction = false; // UrgentNotify has no handler of its own
            ducky.AddSlice<Fixtures.InboxSlice>();
        });

        await store.DispatchAsync(new Fixtures.UrgentNotify("fire")); // a Fluxor [ReducerMethod] for Notify would run
        store.State.Get<Fixtures.Inbox>().Last.ShouldBeNull();

        await store.DispatchAsync(new Fixtures.Notify("mail"));
        store.State.Get<Fixtures.Inbox>().Last.ShouldBe("mail");
    }
    #endregion

    #region load-rule
    [Fact]
    public async Task RestoredSlice_IsNotLoadedAgain()
    {
        var api = new FakeCounterApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddSlice<CounterSlice>().AddEffect<LoadCounterOnStart>().AddEffect<LoadCounterEffect>(),
            services => services.AddSingleton<ICounterApi>(api));

        store.Seed(new CounterState(Count: 12, IsLoading: false, Error: null)); // as the prerender seed restores it
        await store.Settled();

        api.Calls.ShouldBe(0);
        store.State.Get<CounterState>().Count.ShouldBe(12);
    }
    #endregion

    #region no-initializer
    [Fact]
    public async Task Store_InitializesOnFirstUse_WithoutAnInitializerComponent()
    {
        var api = new FakeCounterApi();
        await using var provider = Registration.AddApp(new ServiceCollection(), api).BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        // No component has rendered: the first dispatch initializes the store, then the action is reduced.
        var result = await store.DispatchAsync(new IncrementCounterAction(1));

        result.ShouldBe(DispatchResult.Reduced);
        store.State.Get<CounterState>().Count.ShouldBe(1);
        await api.Called(0); // and StoreInitialized started the first load
        api.Complete(0, 0); // let the load finish before disposal
        await store.WhenIdleAsync(Xunit.TestContext.Current.CancellationToken);
    }
    #endregion

    #region generated-registration
    [Fact]
    public async Task GeneratedRegistration_ListsTheSliceAndEffects_WithoutAScan()
    {
        var api = new FakeCounterApi();
        await using var store = TestStore.Create(
            ducky => ducky.AddDuckyGenerated_FluxorSample_After(), // emitted at build time: no reflection at startup
            services => services.AddSingleton<ICounterApi>(api).AddSingleton<ActionLog>());

        store.Slices.ShouldHaveSingleItem().ShouldBeOfType<CounterSlice>();
        await store.InitializeAsync(Xunit.TestContext.Current.CancellationToken);
        await api.Called(0); // LoadCounterOnStart and LoadCounterEffect were registered too
        api.Complete(0, 1);
        await store.Settled();
        store.State.Get<CounterState>().Count.ShouldBe(1);
    }
    #endregion
}

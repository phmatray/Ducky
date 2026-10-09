using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Ducky.Tests.SliceStoreFixtures;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using TestMeterFactory = Ducky.Tests.Diagnostics.TelemetryTests.TestMeterFactory;

namespace Ducky.Tests;

// SPEC §6.4 step 2, §6.5, §6.6 and §12 (origin and run scope): a SliceStore Set/SetAsync reads the owning store's own
// effect-run and causal scopes. INV-11 (a Set from a superseded run is dropped), INV-30 (only the run's own flow is
// scoped), INV-06 (another store's run scopes nothing). Gates are plain TaskCompletionSources, so SetResult resumes the
// run inline on the test thread.
public sealed class SliceStoreRunTests
{
    private const int RunDropped = 1050;

    // A superseded Switch run that ignores its token and calls cart.Set/SetAsync after its await is dropped at call time,
    // logged at Debug and counted with reason run, while the live run's Set from its prefix is reduced.
    [Fact]
    public async Task Switch_SupersededRunCallingSliceStoreSet_IsDropped()
    {
        using var meters = new TestMeterFactory();
        using var dropped = new MetricCollector<long>(meters, "Ducky", "ducky.dispatch.dropped");
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        var cart = new CartStore();
        Task<DispatchResult>? stale = null;
        var effect = new SwitchHandler<Load>(async (load, _, _) =>
        {
            if (load.Id == 2)
            {
                cart.Add("live");
                return;
            }

            await gate.Task.ConfigureAwait(false);
            cart.Add("stale");
            stale = cart.AddAsync("stale!");
        });
        var store = new DuckyStore([cart, new SeenSlice()], logger, effects: () => [(effect, false)], meterFactory: meters);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        gate.SetResult();

        (await stale.ShouldNotBeNull()).ShouldBe(DispatchResult.Dropped);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        cart.State.Items.ShouldBe(["live"]);
        var drops = logger.Collector.GetSnapshot().Where(r => r.Id.Id == RunDropped).ToList();
        drops.Select(r => r.Message).ShouldBe([
            $"{typeof(SetState<CartState>)} dropped: the effect run that dispatched it was superseded",
            $"{typeof(SetState<CartState>)} dropped: the effect run that dispatched it was superseded",
        ]);
        drops.ShouldAllBe(r => r.Level == LogLevel.Debug);
        dropped.GetMeasurementSnapshot().Select(m => (m.Value, m.Tags["ducky.drop.reason"])).ShouldBe([(1L, (object?)"run"), (1L, "run")]);
    }

    // The run's continuation dispatches Loaded and so becomes the drainer; the subscriber it notifies while processing
    // Loaded dispatches Load(2), which supersedes the run when it is processed, then calls cart.Set and SetAsync. Process
    // cleared the run scope, so both are Local and reduced after the supersession; once Process returns the scope is the
    // run's again, so the run's own Set after its dispatch is dropped.
    [Fact]
    public async Task SliceStore_SetFromSubscriberDuringDrainOwnedByEffectContinuation_IsLocal()
    {
        var recorder = new Recorder("r", []);
        var gate = new TaskCompletionSource();
        var cart = new CartStore();
        Task<DispatchResult>? fromSubscriber = null;
        var effect = new SwitchHandler<Load>(async (load, context, _) =>
        {
            if (load.Id == 1)
            {
                await gate.Task.ConfigureAwait(false);
                context.Dispatch(new Loaded(1));
                cart.Add("run");
            }
        });
        var store = new DuckyStore([cart, new SeenSlice()], NullLogger.Instance, middleware: () => [recorder], effects: () => [(effect, false)]);
        using var selection = store.Select(s => s.Get<Seen>(), seen =>
        {
            if (seen.Actions[^1] is Loaded)
            {
                store.Dispatch(new Load(2));
                cart.Add("subscriber");
                fromSubscriber = cart.AddAsync("subscriber!");
            }
        });
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatch(new Load(1));
        recorder.Contexts.Clear();

        gate.SetResult();

        (await fromSubscriber.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        cart.State.Items.ShouldBe(["subscriber", "subscriber!"]);
        recorder.Contexts.DistinctBy(c => c.Id).Select(c => (Describe(c), c.Origin)).ShouldBe(
        [
            (new Loaded(1), Origin.Effect),
            (new Load(2), Origin.Local),
            ("cart/Add", Origin.Local),
            ("cart/AddAsync", Origin.Local),
        ]);
    }

    // A Switch run of store A calls B's SliceStore Set, B.Dispatch and B.WhenIdleAsync from its synchronous prefix: B sees
    // Origin.Local at depth 0 on a new chain, and WhenIdleAsync does not throw. After A's run is superseded, its calls into
    // B are still reduced, never dropped, and its B.DisposeAsync() does not complete the DisposeCalled of A's run.
    [Fact]
    public async Task TwoStores_EffectRunOfA_DoesNotScopeDispatchSetOrWhenIdleOfB()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var recorderA = new Recorder("a", []);
        var recorderB = new Recorder("b", []);
        var cartB = new CartStore();
        var storeB = new DuckyStore([cartB, new SeenSlice()], NullLogger.Instance, middleware: () => [recorderB]);
        var gate = new TaskCompletionSource();
        Task? idleB = null;
        Task<DispatchResult>? setAfterSupersession = null;
        Task<DispatchResult>? dispatchAfterSupersession = null;
        Task? disposeB = null;
        EffectRunToken? runOfA = null;
        var effect = new SwitchHandler<Load>(async (load, context, _) =>
        {
            if (load.Id == 2)
            {
                return;
            }

            cartB.Add("prefix");
            storeB.Dispatch(new Loaded(1));
            idleB = storeB.WhenIdleAsync(CancellationToken.None);
            await gate.Task.ConfigureAwait(false);
            setAfterSupersession = cartB.AddAsync("late");
            dispatchAfterSupersession = storeB.DispatchAsync(new Loaded(2));
            runOfA = context.Run;
            disposeB = storeB.DisposeAsync().AsTask();
        });
        var storeA = new DuckyStore([new SeenSlice()], NullLogger.Instance, middleware: () => [recorderA], effects: () => [(effect, false)]);
        await storeB.InitializeAsync(cancellationToken);
        recorderB.Contexts.Clear();

        storeA.Dispatch(new Load(1));
        storeA.Dispatch(new Load(2));
        gate.SetResult();

        idleB.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        (await setAfterSupersession.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        (await dispatchAfterSupersession.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        await disposeB.ShouldNotBeNull();
        runOfA.ShouldNotBeNull().DisposeCalled.Task.IsCompleted.ShouldBeFalse();
        await storeA.WhenIdleAsync(cancellationToken);
        cartB.State.Items.ShouldBe(["prefix", "late"]);
        var load1 = recorderA.Contexts.First(c => c.Action is Load { Id: 1 });
        var seenByB = recorderB.Contexts.DistinctBy(c => c.Id).ToList();
        seenByB.Select(c => (Describe(c), c.Origin, c.Depth)).ShouldBe(
        [
            ("cart/Add", Origin.Local, 0),
            (new Loaded(1), Origin.Local, 0),
            ("cart/AddAsync", Origin.Local, 0),
            (new Loaded(2), Origin.Local, 0),
        ]);
        seenByB.ShouldAllBe(c => c.CorrelationId == c.Id);
        seenByB.ShouldAllBe(c => c.CorrelationId != load1.CorrelationId);
        await storeA.DisposeAsync();
    }

    // A SetState action by its {key}/{name} type (its mutator has no value equality), any other action by itself.
    private static object Describe(ActionContext context) => context.Action is SetState ? context.ActionType : context.Action;
}

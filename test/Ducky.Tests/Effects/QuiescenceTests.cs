using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests;

// SPEC §6.4 step 2 (the effect-run scope is cleared for every action), §6.5 (per-store _effectRun), §6.6 (_runningEffects,
// CountedForIdle, the finally order), §7.4 (WhenIdleAsync from a non-LongRunning run throws); INV-10, INV-11.
// A wait without a token is the waiters' own task, so whether it has completed is observable synchronously.
public sealed class QuiescenceTests
{
    // The run is counted from its start to the end of its finally, and the action it dispatches is enqueued before the
    // run ends: here its continuation runs inline inside a subscriber, so Loaded is queued behind the running drain when
    // the run's count reaches zero. Idle fires only once that drain has processed it and exited.
    [Fact]
    public void WhenIdle_NeverCompletesWhileEffectCausedActionPending()
    {
        var gate = new TaskCompletionSource(); // SetResult resumes the run inline
        var effect = new Handler<Load>(async (_, context, _) =>
        {
            await gate.Task.ConfigureAwait(false);
            context.Dispatch(new Loaded(1));
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);
        Task? idle = null;
        bool? idleWhenLoadedReduced = null;
        using var selection = store.Select(s => s.Get<Seen>(), seen =>
        {
            switch (seen.Actions[^1])
            {
                case Base:
                    gate.SetResult();
                    break;
                case Loaded:
                    idleWhenLoadedReduced = idle!.IsCompleted;
                    break;
            }
        });

        store.Dispatch(new Load(1));
        idle = store.WhenIdleAsync(CancellationToken.None);
        idle.IsCompleted.ShouldBeFalse();

        store.Dispatch(new Base());

        idleWhenLoadedReduced.ShouldBe(false);
        idle.IsCompletedSuccessfully.ShouldBeTrue();
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Base(), new Loaded(1)]);
    }

    // A LongRunning run is never counted: the store is idle while it is still running, and it may dispatch later.
    [Fact]
    public async Task WhenIdle_IgnoresLongRunningEffects()
    {
        var gate = new TaskCompletionSource();
        var effect = new LongRunningHandler<Load>(async (_, context, _) =>
        {
            await gate.Task.ConfigureAwait(false);
            context.Dispatch(new Loaded(1));
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        store.WhenIdleAsync(CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
        gate.SetResult();
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Loaded(1)]);
        store.WhenIdleAsync(CancellationToken.None).IsCompletedSuccessfully.ShouldBeTrue();
    }

    // A non-LongRunning run that awaited idle would wait for itself forever, so WhenIdleAsync throws synchronously, before
    // and after the run's first await. A LongRunning run may wait. CountedForIdle ends with the run's idle decrement: a
    // flow that inherited the run's scope (here its captured ExecutionContext) waits normally once the run has ended.
    // TestStore.Settled (Ducky.Testing, stage 9) routes through WhenIdleAsync and gets this exception with it.
    [Fact]
    public void WhenIdle_FromNonLongRunningEffectRun_ThrowsNotHangs()
    {
        DuckyStore store = null!;
        var gate = new TaskCompletionSource();
        List<Exception> thrown = [];
        ExecutionContext? runContext = null;
        var merge = new Handler<Load>(async (_, _, _) =>
        {
            thrown.Add(Should.Throw<InvalidOperationException>(() => { _ = store.WhenIdleAsync(CancellationToken.None); }));
            runContext = ExecutionContext.Capture();
            await gate.Task.ConfigureAwait(false);
            thrown.Add(Should.Throw<InvalidOperationException>(() => { _ = store.WhenIdleAsync(CancellationToken.None); }));
        });
        Task? longRunningWait = null;
        var longRunning = new LongRunningHandler<Loaded>(async (_, _, _) =>
        {
            longRunningWait = store.WhenIdleAsync(CancellationToken.None);
            await longRunningWait.ConfigureAwait(false);
        });
        store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(merge, false), (longRunning, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Loaded(1));
        longRunningWait.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        gate.SetResult();

        thrown.Count.ShouldBe(2);
        thrown.ShouldAllBe(e => e.Message == thrown[0].Message);
        thrown[0].Message.ShouldBe(
            "WhenIdleAsync was called from a non-LongRunning effect run of this store, which would wait for itself " +
            "forever. Do not wait for idle inside an effect, or mark the effect LongRunning.");
        longRunningWait.IsCompletedSuccessfully.ShouldBeTrue();
        Task? afterRun = null;
        ExecutionContext.Run(runContext!, _ => afterRun = store.WhenIdleAsync(CancellationToken.None), null);
        afterRun.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
    }

    // Non-normative: Process clears the run scope for every action (§6.4 step 2), so a subscriber running on a drain that
    // an effect continuation owns is not that run and may wait for idle; the scope is restored when Process returns, so
    // the run itself still cannot.
    [Fact]
    public void WhenIdle_FromDrainOwnedByEffectContinuation_NotTheRun()
    {
        DuckyStore store = null!;
        var gate = new TaskCompletionSource();
        Task? subscriberWait = null;
        Exception? afterDispatch = null;
        var effect = new Handler<Load>(async (_, context, _) =>
        {
            await gate.Task.ConfigureAwait(false);
            context.Dispatch(new Loaded(1));
            afterDispatch = Should.Throw<InvalidOperationException>(() => { _ = store.WhenIdleAsync(CancellationToken.None); });
        });
        store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);
        using var selection = store.Select(s => s.Get<Seen>(), seen =>
        {
            if (seen.Actions[^1] is Loaded)
            {
                subscriberWait = store.WhenIdleAsync(CancellationToken.None);
            }
        });
        store.Dispatch(new Load(1));

        gate.SetResult();

        afterDispatch.ShouldNotBeNull();
        subscriberWait.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
    }
}

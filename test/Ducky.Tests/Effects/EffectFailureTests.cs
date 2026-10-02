using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.6 (observation), §8.1 (effect throws: action + log), INV-11, INV-12: a run's fault is logged (Error 1003) and
// routed through FailureRouter, as EffectFailed at depth 0, or logged only (1001) when the run's trigger was a failure.
public sealed class EffectFailureTests
{
    // An OperationCanceledException for a token that is not the run's, while the run's token is not cancelled (the shape
    // of an HttpClient timeout), is a failure, not cancellation.
    [Fact]
    public async Task Effect_ForeignOce_IsEffectFailed()
    {
        var logger = new FakeLogger();
        using var foreign = new CancellationTokenSource();
        await foreign.CancelAsync();
        var thrown = new TaskCanceledException("timeout", null, foreign.Token);
        var release = new TaskCompletionSource(); // SetResult resumes the run inline, on the test thread
        var effect = new Handler<Load>(async (_, _, _) =>
        {
            await release.Task.ConfigureAwait(false);
            throw thrown;
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);

        release.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeFalse();
        var failure = new EffectFailed(typeof(Handler<Load>).ToString(), typeof(Load).ToString(), thrown);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), failure]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level).ShouldBe((1003, LogLevel.Error));
        record.Message.ShouldBe($"Effect {typeof(Handler<Load>)} threw while handling {typeof(Load)}");
        record.Exception.ShouldBeSameAs(thrown);
    }

    // The 1.x lost-fault bug: a run that faults with a non-cancellation exception after the store's token was cancelled is
    // still observed. The fault is logged; its EffectFailed reaches a disposed store and is ignored (Debug 1002).
    [Fact]
    public async Task Effect_FaultDuringDispose_StillObserved()
    {
        var logger = new FakeLogger();
        var thrown = new InvalidOperationException("cleanup");
        var release = new TaskCompletionSource();
        CancellationToken runToken = default;
        var effect = new Handler<Load>(async (_, _, token) =>
        {
            runToken = token;
            await release.Task.ConfigureAwait(false);
            throw thrown;
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        store.Dispatch(new Load(1));

        var disposing = store.DisposeAsync();
        runToken.IsCancellationRequested.ShouldBeTrue();
        release.SetResult();
        await disposing;

        logger.Collector.GetSnapshot().Select(r => (r.Id.Id, r.Level, r.Exception)).ShouldBe(
        [
            (1003, LogLevel.Error, thrown),
            (1002, LogLevel.Debug, null),
        ]);
        logger.Collector.LatestRecord.Message.ShouldBe($"{typeof(EffectFailed)} ignored: the store is disposed");
    }

    // An effect triggered by EffectFailed that throws before its first await is logged only: no second EffectFailed, so
    // the failure can't loop.
    [Fact]
    public async Task EffectOnEffectFailed_ThrowsSync_LogsOnly_NoLoop()
    {
        var logger = new FakeLogger();
        var first = new InvalidOperationException("load");
        var second = new InvalidOperationException("on failure");
        List<EffectFailed> handled = [];
        var failing = new Handler<Load>((_, _, _) => throw first);
        var onFailed = new Handler<EffectFailed>((failed, _, _) =>
        {
            handled.Add(failed);
            throw second;
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(failing, false), (onFailed, false)]);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        AssertLoggedOnly(store, logger, handled, first, second);
    }

    // The same after the run's first real await: the run keeps its trigger's failure flag, whatever thread resumes it.
    [Fact]
    public async Task EffectOnEffectFailed_ThrowsAfterAwait_LogsOnly()
    {
        var logger = new FakeLogger();
        var first = new InvalidOperationException("load");
        var second = new InvalidOperationException("on failure");
        var release = new TaskCompletionSource(); // SetResult resumes the run inline, on the test thread
        List<EffectFailed> handled = [];
        var failing = new Handler<Load>((_, _, _) => throw first);
        var onFailed = new Handler<EffectFailed>(async (failed, _, _) =>
        {
            handled.Add(failed);
            await release.Task.ConfigureAwait(false);
            throw second;
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(failing, false), (onFailed, false)]);
        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        release.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);

        AssertLoggedOnly(store, logger, handled, first, second);
    }

    private static void AssertLoggedOnly(DuckyStore store, FakeLogger logger, List<EffectFailed> handled, Exception first, Exception second)
    {
        var failure = new EffectFailed(typeof(Handler<Load>).ToString(), typeof(Load).ToString(), first);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), failure]);
        handled.ShouldBe([failure]);
        logger.Collector.GetSnapshot().Select(r => (r.Id.Id, r.Level, r.Exception)).ShouldBe(
        [
            (1003, LogLevel.Error, first),
            (1003, LogLevel.Error, second),
            (1001, LogLevel.Error, second),
        ]);
        logger.Collector.LatestRecord.Message.ShouldBe(
            $"{typeof(EffectFailed)} for {typeof(EffectFailed)} raised while handling a failure action, logged and not dispatched");
    }
}

using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 16 against DisposeAsync steps 3-5 (§6.11): INV-29.
[Collection(nameof(Interleaving))]
public sealed class DisposeInterleavingTests
{
    // An effect continuation on a pool thread disposes the store while another thread's drain is parked in a reducer.
    // There is no "am I the drainer?" test, so disposal waits for that drain to exit before it disposes middleware, and
    // the disposing run's own DisposeCalled keeps the run wait from waiting for the run. DisposeTimeout is infinite, so
    // only those rules, never a bound, can complete the disposal.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispose_FromRacingEffectContinuation_WaitsForDrain(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogSlice();
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); // resumes on the pool
        var disposing = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        DuckyStore store = null!;
        bool? drainExitedAtMiddlewareDispose = null;
        var effect = new Handler<Add>(async (_, _, _) =>
        {
            await resume.Task.ConfigureAwait(false);
            var disposal = store.DisposeAsync().AsTask();
            disposing.SetResult(disposal);
            await disposal.ConfigureAwait(false);
        });
        var probe = new DisposeProbe(() => drainExitedAtMiddlewareDispose = store.Dispatcher.DrainExited?.IsCompleted);
        store = new DuckyStore(
            [log],
            NullLogger.Instance,
            disposeTimeout: Timeout.InfiniteTimeSpan,
            middleware: () => [probe],
            effects: () => [(effect, false)]);
        store.Dispatch(new Add(0, 0));
        log.OnBlock = () =>
        {
            parked.SetResult();
            release.Task.Wait(cancellationToken);
        };
        var drainer = Task.Factory.StartNew(
            () => store.Dispatch(new Block()),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await Interleaving.Within(parked.Task);

        resume.SetResult();
        var disposal = await Interleaving.Within(disposing.Task);

        disposal.IsCompleted.ShouldBeFalse();
        drainExitedAtMiddlewareDispose.ShouldBeNull();
        release.SetResult();
        await Interleaving.Within(disposal);
        await Interleaving.Within(drainer);
        drainExitedAtMiddlewareDispose.ShouldBe(true);
    }
}

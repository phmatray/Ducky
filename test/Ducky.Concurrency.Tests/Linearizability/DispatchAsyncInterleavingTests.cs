namespace Ducky.Concurrency.Tests;

// SPEC §17.3 rows 11-12: a DispatchAsync queued behind another thread's drain (§7.3). Its task completes only once its
// action is reduced (INV-10), on a continuation that never runs inline on the drainer (INV-10). Completion after the
// commit (INV-07) is owned by Snapshot_AfterConcurrentReduces_IsNeverStale and the read-your-writes check of
// Linearizability_DispatchVsModel: the state read here only races the drainer's commit.
public sealed class DispatchAsyncInterleavingTests
{
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task DispatchAsync_QueuedBehindOtherThreadsDrain_CompletesAfterReduce(int repeat)
    {
        _ = repeat;
        var log = new LogSlice();
        var store = Interleaving.Store(log);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var (drainer, _) = await HoldDrain(store, log, release.Task);

            // The test thread finds a drainer, so it only enqueues: nothing is reduced until the drainer is released.
            var mine = new Add(1, 0);
            var task = store.DispatchAsync(mine);
            bool? completedAtReduce = null;
            log.OnReduce = () => completedAtReduce = task.IsCompleted;
            task.IsCompleted.ShouldBeFalse("completed before the drainer reduced it");

            release.SetResult();
            (await Interleaving.Within(task)).ShouldBe(DispatchResult.Reduced);

            // Still pending while its reducer ran; the state read below is a sanity check, not the INV-07 ordering.
            completedAtReduce.ShouldBe(false);
            store.State.Get<Log>().Entries.ShouldBe([mine]);
            store.State.Version.ShouldBe(1);
            await Interleaving.Within(drainer);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task DispatchAsync_ContinuationNeverRunsInlineOnDrainer(int repeat)
    {
        _ = repeat;
        var log = new LogSlice();
        var store = Interleaving.Store(log);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            var (drainer, drainerThread) = await HoldDrain(store, log, release.Task);
            var drainExited = store.Dispatcher.DrainExited.ShouldNotBeNull();
            var task = store.DispatchAsync(new Add(1, 0));

            // ConfigureAwait(false) drops xunit's context, so only the task's own completion options decide where the
            // continuation runs: completed without RunContinuationsAsynchronously, it would run right here on the
            // drainer, inside the drain that has not exited yet.
            async Task<bool> RanInlineOnDrainer()
            {
                await task.ConfigureAwait(false);
                return Environment.CurrentManagedThreadId == drainerThread && !drainExited.IsCompleted;
            }

            var continuation = RanInlineOnDrainer();
            release.SetResult();

            (await Interleaving.Within(continuation)).ShouldBeFalse("the continuation ran inline on the drainer");
            await Interleaving.Within(drainer);
        }
        finally
        {
            release.TrySetResult();
        }
    }

    // Another thread dispatches Block, becomes the drainer and parks inside the Block reducer until release completes.
    // Returns that thread's dispatch and its managed thread id once it is parked.
    private static async Task<(Task Drainer, int Thread)> HoldDrain(DuckyStore store, LogSlice log, Task release)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var parked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        log.OnBlock = () =>
        {
            parked.SetResult(Environment.CurrentManagedThreadId);
            release.Wait(cancellationToken);
        };
        var drainer = Task.Run(() => store.Dispatch(new Block()), cancellationToken);
        return (drainer, await Interleaving.Within(parked.Task));
    }
}

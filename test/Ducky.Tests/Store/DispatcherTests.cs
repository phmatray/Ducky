using Ducky.Tests.DispatcherFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.3 (single drainer), §6.4 steps 6-8, §7; INV-01, INV-03, INV-07, INV-08, INV-10.
public sealed class DispatcherTests
{
    [Fact]
    public async Task ReducerThrow_InOneSlice_CommitsNoSliceForThatAction()
    {
        var logger = new FakeLogger();
        var boom = new BoomSlice();
        var store = new DuckyStore([new CountSlice(), boom], logger);
        boom.KeyThrows = true;
        store.Dispatch(new Bump());
        var before = store.State;

        // CountSlice reduces Boom first; BoomSlice then throws, so CountSlice's result is discarded too.
        (await store.DispatchAsync(new Boom())).ShouldBe(DispatchResult.Failed);

        store.State.ShouldBeSameAs(before);
        store.State.Get<Count>().Value.ShouldBe(1);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1000);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
        record.Message.ShouldBe($"Reducer of slice 'boom' threw while reducing {typeof(Boom)}");

        // The failed action left nothing behind: the next action commits only its own slice.
        (await store.DispatchAsync(new Ping())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Count>().Value.ShouldBe(1);
        store.State.Get<Flag>().On.ShouldBeTrue();
        store.State.Version.ShouldBe(before.Version + 1);
    }

    // Non-normative: the task completes after the commit, and never inline on the drainer (INV-10).
    [Fact]
    public async Task DispatchAsync_Reduced_CompletesAfterCommit()
    {
        var count = new CountSlice();
        var store = new DuckyStore([count], NullLogger.Instance);
        var drainer = Environment.CurrentManagedThreadId;
        var insideDispatch = true;
        Task<DispatchResult>? queued = null;
        Task<(bool Inline, int Seen)>? continuation = null;
        count.OnProbe = () =>
        {
            queued = store.DispatchAsync(new Bump());
            continuation = queued.ContinueWith(
                _ => (Volatile.Read(ref insideDispatch) && Environment.CurrentManagedThreadId == drainer,
                    store.State.Get<Count>().Value),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        };

        store.Dispatch(new Probe());
        Volatile.Write(ref insideDispatch, false);

        (await queued.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        var (inline, seen) = await continuation.ShouldNotBeNull();
        inline.ShouldBeFalse();
        seen.ShouldBe(1);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Count>().Value.ShouldBe(2);
    }

    // Non-normative: with no drain active, the caller drains inline before Dispatch returns (§7 rule 3).
    [Fact]
    public void Dispatch_CallerBecomesDrainer_ReadYourWrites()
    {
        var count = new CountSlice();
        var flag = new BoomSlice();
        var store = new DuckyStore([count, flag], NullLogger.Instance);

        store.State.ShouldBeSameAs(store.InitialState);
        store.InitialState.Version.ShouldBe(0);
        store.Slices.ShouldBe([count, flag]);

        store.Dispatch(new Bump());
        store.State.Get<Count>().Value.ShouldBe(1);
        store.State.Version.ShouldBe(1);

        store.Dispatch(new Bump());
        store.State.Get<Count>().Value.ShouldBe(2);
        store.State.Version.ShouldBe(2);

        // An action no slice handles commits nothing.
        store.Dispatch(new Count(0));
        store.State.Version.ShouldBe(2);
        store.InitialState.Get<Count>().Value.ShouldBe(0);
    }

    [Fact]
    public void Reducers_DispatchFromReducer_QueuedNotNested()
    {
        var count = new CountSlice();
        var store = new DuckyStore([count], NullLogger.Instance);
        var seenInsideReducer = -1;
        count.OnProbe = () =>
        {
            store.Dispatch(new Bump());
            seenInsideReducer = store.State.Get<Count>().Value;
        };

        store.Dispatch(new Probe());

        count.Reduced.ShouldBe(["Probe start", "Probe end", "Bump"]);
        seenInsideReducer.ShouldBe(0);
        store.State.Get<Count>().Value.ShouldBe(1);
    }

    [Fact]
    public async Task Snapshot_DispatchAsyncReduced_StateVersionAtLeastCommitVersion()
    {
        var count = new CountSlice();
        var store = new DuckyStore([count], NullLogger.Instance);
        var queued = new List<Task<DispatchResult>>();
        var seen = new List<long>();
        count.OnProbe = () =>
        {
            for (var i = 0; i < 2; i++)
            {
                // No RunContinuationsAsynchronously, on purpose (DispatchAsync always sets it, INV-10): the continuation
                // runs inside the completion, so it reads State before the drainer's next statement.
                var completion = new TaskCompletionSource<DispatchResult>();
                _ = completion.Task.ContinueWith(
                    _ => seen.Add(store.State.Version),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                store.Dispatcher.Enqueue(new Pending(new Bump(), Origin.Local, 0, 0, false, false, completion));
                queued.Add(completion.Task);
            }
        };

        store.Dispatch(new Probe());

        // Every Bump changes the count, so the k-th Bump commits Version k, and completes only after that commit.
        seen.ShouldBe([1L, 2L]);
        foreach (var task in queued)
        {
            (await task).ShouldBe(DispatchResult.Reduced);
        }

        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        store.State.Version.ShouldBe(3);
    }

    // Non-normative: Ids are assigned under _gate in enqueue order, starting at 1; _drainExited is created when a drain
    // starts and completed when it releases (§6.3).
    [Fact]
    public async Task Dispatcher_IdsAndDrainExited_FollowEnqueueAndRelease()
    {
        var count = new CountSlice();
        var store = new DuckyStore([count], NullLogger.Instance);
        var dispatcher = store.Dispatcher;
        dispatcher.DrainExited.ShouldBeNull();
        var nested = new Pending(new Bump(), Origin.Local, 0, 0, false, false, null);
        var drainer = Environment.CurrentManagedThreadId;
        var insideEnqueue = true;
        Task? duringDrain = null;
        Task<bool>? continuation = null;
        var completedDuringDrain = true;
        count.OnProbe = () =>
        {
            dispatcher.Enqueue(nested);
            duringDrain = dispatcher.DrainExited;
            completedDuringDrain = duringDrain?.IsCompleted ?? true;
            continuation = duringDrain?.ContinueWith(
                _ => Volatile.Read(ref insideEnqueue) && Environment.CurrentManagedThreadId == drainer,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        };
        var first = new Pending(new Probe(), Origin.Local, 0, 0, false, false, null);

        dispatcher.Enqueue(first);
        Volatile.Write(ref insideEnqueue, false);

        first.Id.ShouldBe(1);
        nested.Id.ShouldBe(2);
        completedDuringDrain.ShouldBeFalse();
        var exited = duringDrain.ShouldNotBeNull();
        exited.IsCompletedSuccessfully.ShouldBeTrue();
        dispatcher.DrainExited.ShouldBeSameAs(exited);
        (await continuation.ShouldNotBeNull()).ShouldBeFalse();

        var third = new Pending(new Bump(), Origin.Local, 0, 0, false, false, null);
        dispatcher.Enqueue(third);
        third.Id.ShouldBe(3);
        dispatcher.DrainExited.ShouldNotBeSameAs(exited);
        dispatcher.DrainExited.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
    }

    // Non-normative: a null action is a programmer error, thrown synchronously (§7.4).
    [Fact]
    public void Dispatch_NullAction_ThrowsArgumentNullException()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);

        Should.Throw<ArgumentNullException>(() => store.Dispatch(null!)).ParamName.ShouldBe("action");
        Should.Throw<ArgumentNullException>(() => store.DispatchAsync(null!)).ParamName.ShouldBe("action");
    }
}

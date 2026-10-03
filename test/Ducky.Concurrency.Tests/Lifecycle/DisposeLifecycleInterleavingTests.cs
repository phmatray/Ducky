using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 rows 8 and 16 against DisposeAsync steps 1-5a and the sync Dispose (§6.11): INV-02, INV-10, INV-29.
[Collection(nameof(Interleaving))]
public sealed class DisposeLifecycleInterleavingTests
{
    private const int Producers = 6;
    private const int PerProducer = 100;

    // Producers dispatch (Dispatch and DispatchAsync) while another thread disposes, all released by one barrier. No call
    // throws, every DispatchAsync task completes Reduced or Disposed (never faulted, never stranded), a Reduced action is in
    // the final state exactly once and a Disposed one never, each producer's Reduced actions are a prefix of its sequence
    // (disposal detaches the queue at once and rejects everything after), and disposal and idle complete.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispose_DuringConcurrentDispatch_NoExceptionNoLeak_AllTasksComplete(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var disposals = 0;
        var store = new DuckyStore(
            [new LogSlice()],
            NullLogger.Instance,
            disposeTimeout: Timeout.InfiniteTimeSpan,
            middleware: () => [new HookProbe { OnDispose = () => Interlocked.Increment(ref disposals) }]);

        // Materialized up front: a dispose that wins the race with the first use is M4-05b's test, not this one.
        store.Dispatcher.Materialize();
        using var start = new Barrier(Producers + 1);
        var results = new Task<DispatchResult>[Producers][];
        var producers = Enumerable.Range(0, Producers).Select(p => Dedicated(
            () =>
            {
                start.SignalAndWait(cancellationToken);

                // Odd Seq: DispatchAsync, whose result is checked; even Seq: fire-and-forget Dispatch.
                results[p] = [.. Enumerable.Range(0, PerProducer).Select(i =>
                {
                    if (i % 2 == 0)
                    {
                        store.Dispatch(new Add(p, i));
                        return Task.FromResult(DispatchResult.Reduced);
                    }

                    return store.DispatchAsync(new Add(p, i));
                })];
            },
            cancellationToken));
        var disposer = Dedicated(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                return store.DisposeAsync().AsTask();
            },
            cancellationToken);

        await Interleaving.Within(Task.WhenAll(producers));
        await Interleaving.Within(await Interleaving.Within(disposer));
        await Interleaving.Within(store.Dispatcher.DisposalSteps.ShouldNotBeNull());
        await Interleaving.Within(store.WhenIdleAsync(cancellationToken));

        var entries = store.State.Get<Log>().Entries;
        entries.Distinct().Count().ShouldBe(entries.Count);
        for (var p = 0; p < Producers; p++)
        {
            var reducedSeqs = entries.Where(e => e.Producer == p).Select(e => e.Seq).ToList();
            reducedSeqs.ShouldBe(Enumerable.Range(0, reducedSeqs.Count), $"producer {p}");
            for (var i = 1; i < PerProducer; i += 2)
            {
                var result = results[p][i];
                result.IsCompletedSuccessfully.ShouldBeTrue();
                (await result).ShouldBe(i < reducedSeqs.Count ? DispatchResult.Reduced : DispatchResult.Disposed, $"producer {p} seq {i}");
            }
        }

        Volatile.Read(ref disposals).ShouldBe(1);
    }

    // A drain on another thread is parked in a reducer, with actions queued behind it, when this thread disposes. Step 3
    // awaits that drain's exit (DisposeTimeout is infinite, so only the wait, never a bound, completes disposal): the
    // middleware is not disposed while the drain is in flight, the queued actions complete Disposed, the parked action's
    // AfterReduce still sees a live middleware, and the middleware is disposed once, after the drain exited.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispose_WaitsForInFlightDrainBeforeMiddlewareDispose(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, log, probe) = await ParkedStore(disposeTimeout: Timeout.InfiniteTimeSpan);
        var drainer = Dedicated(() => store.Dispatch(new Block()), cancellationToken);
        await Interleaving.Within(log.Parked);
        try
        {
            var queued = await Interleaving.Within(Dedicated(() => store.DispatchAsync(new Add(0, 0)), cancellationToken));

            var disposal = store.DisposeAsync().AsTask();

            disposal.IsCompleted.ShouldBeFalse();
            probe.DisposedWithDrainExited.ShouldBeNull();
            (await Interleaving.Within(queued)).ShouldBe(DispatchResult.Disposed);
            log.Release();
            await Interleaving.Within(disposal);
            await Interleaving.Within(drainer);
            probe.DisposedWithDrainExited.ShouldBe(true);
            probe.Disposals.ShouldBe(1);
            probe.AfterReduceOnDisposed.ShouldBe(0);
            probe.BlockAfterReduced.ShouldBeTrue();
        }
        finally
        {
            log.Release(); // a failed assertion must not leave the drainer thread parked
        }
    }

    // The sync Dispose is DisposeAsync plus a warning, so it neither blocks on the parked drain nor disposes the middleware
    // under it. Producers keep dispatching from other threads across the disposal: no AfterReduce ever reaches a disposed
    // middleware, whatever was queued or arrives later completes Disposed, and the middleware is disposed once the drain exited.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task SyncDispose_DuringDrain_NoAfterReduceOnDisposedMiddleware(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var (store, log, probe) = await ParkedStore(disposeTimeout: Timeout.InfiniteTimeSpan);
        var drainer = Dedicated(() => store.Dispatch(new Block()), cancellationToken);
        await Interleaving.Within(log.Parked);
        try
        {
            using var start = new Barrier(Producers + 1);
            var producers = Enumerable.Range(0, Producers).Select(p => Dedicated(
                () =>
                {
                    start.SignalAndWait(cancellationToken);
                    return Enumerable.Range(0, 20).Select(i => store.DispatchAsync(new Add(p, i))).ToArray();
                },
                cancellationToken)).ToArray();

            await Interleaving.Within(Dedicated(
                () =>
                {
                    start.SignalAndWait(cancellationToken);
                    store.Dispose();
                },
                cancellationToken));

            probe.DisposedWithDrainExited.ShouldBeNull();
            var results = await Interleaving.Within(Task.WhenAll((await Interleaving.Within(Task.WhenAll(producers))).SelectMany(r => r)));
            results.ShouldAllBe(r => r == DispatchResult.Disposed);
            log.Release();
            await Interleaving.Within(store.DisposeAsync().AsTask());
            await Interleaving.Within(drainer);
            probe.DisposedWithDrainExited.ShouldBe(true);
            probe.Disposals.ShouldBe(1);
            probe.AfterReduceOnDisposed.ShouldBe(0);
            probe.BlockAfterReduced.ShouldBeTrue();
            store.State.Get<Log>().Entries.ShouldBeEmpty();
        }
        finally
        {
            log.Release(); // a failed assertion must not leave the drainer thread parked
        }
    }

    // A Ready store whose Block reducer parks until Release, and a middleware that records how AfterReduce and its own
    // disposal interleave with the drain.
    private static async Task<(DuckyStore Store, ParkingLog Log, DisposeRecorder Probe)> ParkedStore(TimeSpan disposeTimeout)
    {
        var log = new ParkingLog();
        DuckyStore store = null!;
        var probe = new DisposeRecorder(() => store.Dispatcher.DrainExited?.IsCompleted);
        store = new DuckyStore([log.Slice], NullLogger.Instance, disposeTimeout: disposeTimeout, middleware: () => [probe.Middleware]);
        await Interleaving.Within(store.InitializeAsync(TestContext.Current.CancellationToken));
        return (store, log, probe);
    }

    private static Task Dedicated(Action body, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(body, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private static Task<T> Dedicated<T>(Func<T> body, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(body, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private sealed class ParkingLog
    {
        private readonly TaskCompletionSource _parked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ParkingLog() => Slice.OnBlock = () =>
        {
            _parked.SetResult();
            _release.Task.Wait(TestContext.Current.CancellationToken);
        };

        public LogSlice Slice { get; } = new();

        public Task Parked => _parked.Task;

        public void Release() => _release.TrySetResult();
    }

    private sealed class DisposeRecorder
    {
        private int _disposals;
        private int _afterReduceOnDisposed;

        public DisposeRecorder(Func<bool?> drainExited) => Middleware = new HookProbe
        {
            After = context =>
            {
                if (Volatile.Read(ref _disposals) > 0)
                {
                    Interlocked.Increment(ref _afterReduceOnDisposed);
                }

                BlockAfterReduced |= context.Action is Block;
            },
            OnDispose = () =>
            {
                DisposedWithDrainExited = drainExited();
                Interlocked.Increment(ref _disposals);
            },
        };

        public HookProbe Middleware { get; }

        public bool? DisposedWithDrainExited { get; private set; }

        public bool BlockAfterReduced { get; private set; }

        public int Disposals => Volatile.Read(ref _disposals);

        public int AfterReduceOnDisposed => Volatile.Read(ref _afterReduceOnDisposed);
    }
}

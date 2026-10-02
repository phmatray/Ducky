using System.Collections.Concurrent;
using CsCheck;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 10 and the Select half of row 18 against SubscriberList and Select (§6.8): INV-05, INV-09.
[Collection(nameof(Interleaving))]
public sealed class SubscriberInterleavingTests
{
    private const int MaxSequentialOperations = 10;
    private const int MaxParallelOperations = 6;

    // The NotRedux regression: a store that notifies under its lock deadlocks once a subscriber hands work needing the
    // store to another thread and waits for it. Notify holds no lock (INV-05, INV-09), so the drainer's onChange reads
    // State and dispatches (queued behind the current action, not nested), and meanwhile another thread reads State,
    // dispatches, dispatches async and subscribes, all returning while the drainer is parked on the barrier.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Subscriber_ReadsStateAndDispatchesDuringNotify_NoDeadlock(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = Interleaving.Store(new LogSlice());
        using var handoff = new Barrier(2);
        List<int> seen = [];
        List<int> lateSeen = [];
        int? readInside = null;
        int? readOutside = null;
        Task? crossThread = null;
        Task<DispatchResult>? queuedAsync = null;
        Selection<int>? late = null;

        using var selection = store.Select(Count, count =>
        {
            seen.Add(count);
            if (count != 1)
            {
                return;
            }

            readInside = Count(store.State);
            store.Dispatch(new Add(0, 1));

            // Dedicated threads: both barrier parties block, and a cold 2-vCPU pool must not be what releases them.
            crossThread = Task.Factory.StartNew(
                () =>
                {
                    // finally: a throw here must release the drainer, so Within(crossThread) reports it, not a deadlock.
                    try
                    {
                        readOutside = Count(store.State);
                        store.Dispatch(new Add(1, 0));
                        queuedAsync = store.DispatchAsync(new Add(1, 1));
                        late = store.Select(Count, lateSeen.Add);
                    }
                    finally
                    {
                        handoff.SignalAndWait(cancellationToken);
                    }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            handoff.SignalAndWait(cancellationToken);
        });

        // The dispatching thread becomes the drainer and drains all four actions before Dispatch returns.
        await Interleaving.Within(Task.Factory.StartNew(
            () => store.Dispatch(new Add(0, 0)),
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));
        await Interleaving.Within(crossThread.ShouldNotBeNull());
        (await Interleaving.Within(queuedAsync.ShouldNotBeNull())).ShouldBe(DispatchResult.Reduced);
        using var subscribedDuringNotify = late.ShouldNotBeNull();

        // Both reads saw the commit being notified, and nothing past it: the drainer, the only writer, was parked.
        readInside.ShouldBe(1);
        readOutside.ShouldBe(1);
        store.State.Get<Log>().Entries.ShouldBe([new Add(0, 0), new Add(0, 1), new Add(1, 0), new Add(1, 1)]);
        seen.ShouldBe([1, 2, 3, 4]);

        // Subscribed during notification at count 1 (installed from the committed snapshot), then told of every later one.
        lateSeen.ShouldBe([2, 3, 4]);
        subscribedDuringNotify.Value.ShouldBe(4);
    }

    // Select races awaited commits (CsCheck SampleParallel). Whatever the interleaving, each subscription's `last` ends on
    // the final value: either the drainer saw it and installed or compared, or its step-3 read saw the commit (§6.8).
    // A lost commit leaves `last` stale, which a probe after the run exposes: it commits a change the selector doesn't
    // see, so a stale subscription calls onChange while an up-to-date one stays silent.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Select_ConcurrentWithCommit_NoLostOnChange(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var select = Gen.Int.Operation<Actual, Model>(_ => "Select", (a, _) => a.Subscribe(), (_, _) => { });
        var tick = Gen.Int.Operation<Actual, Model>(_ => "DispatchAsync(Tick)", (a, _) => a.Tick(), (m, _) => m.Ticks++);

        // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
        await Interleaving.WithinProperty(Task.Run(
            () => Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                select,
                tick,
                equal: (a, m) => a.Ticks == m.Ticks && a.LateCallbacks == 0,
                maxSequentialOperations: MaxSequentialOperations,
                maxParallelOperations: MaxParallelOperations),
            cancellationToken));
    }

    private static int Count(StateSnapshot state) => state.Get<Log>().Entries.Count;

    private sealed class Actual
    {
        private readonly ConcurrentQueue<Selection<long>> _selections = new();
        private int _callbacks;
        private int? _lateCallbacks;

        public DuckyStore Store { get; } = Interleaving.Store(new LeftSlice(), new LogSlice());

        public long Ticks => Store.State.Get<Left>().Ticks;

        // Once, after the run (equal may be called once per linearization tried): the callbacks a Log-only commit
        // causes. Settled waits for the probe's notification, which runs after its DispatchAsync completes (§6.4).
        public int LateCallbacks => _lateCallbacks ??= Probe();

        public void Subscribe() => _selections.Enqueue(Store.Select(Selector, _ => Interlocked.Increment(ref _callbacks)));

        public void Tick() => Interleaving.Wait(Store.DispatchAsync(new Tick())).ShouldBe(DispatchResult.Reduced);

        public override string ToString() => $"ticks {Ticks}, {_selections.Count} selections";

        // The spin widens the window between Select's State read and its install, where a commit can slip in.
        private static long Selector(StateSnapshot state)
        {
            Thread.SpinWait(20);
            return state.Get<Left>().Ticks;
        }

        private int Probe()
        {
            var before = Volatile.Read(ref _callbacks);
            Interleaving.Wait(Store.DispatchAsync(new Add(0, 0))).ShouldBe(DispatchResult.Reduced);
            Interleaving.Wait(Interleaving.Settled(Store));
            return Volatile.Read(ref _callbacks) - before;
        }
    }

    private sealed class Model
    {
        public long Ticks { get; set; }

        public override string ToString() => $"ticks {Ticks}";
    }
}

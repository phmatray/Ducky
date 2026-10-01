namespace Ducky.Concurrency.Tests;

// SPEC §17.3 rows 1-5 against the single drainer (§6.3, §7): INV-01, INV-02, INV-03, INV-04, INV-05.
public sealed class DispatcherInterleavingTests
{
    private const int Producers = 8;
    private const int PerProducer = 250;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispatch_FromNThreads_AppliesEveryActionExactlyOnce(int repeat)
    {
        _ = repeat;
        var store = Interleaving.Store(new LogSlice());

        // Even producers use Dispatch, odd ones DispatchAsync without awaiting between calls.
        var results = await Interleaving.Within(Produce((p, i) => p % 2 == 0
            ? Fire(store, new Add(p, i))
            : store.DispatchAsync(new Add(p, i))));
        await Interleaving.Settled(store);

        results.ShouldAllBe(r => r == DispatchResult.Reduced);
        store.State.Get<Log>().Entries
            .Select(e => (e.Producer * PerProducer) + e.Seq)
            .Order()
            .ShouldBe(Enumerable.Range(0, Producers * PerProducer));
    }

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Reducers_NeverRunConcurrently(int repeat)
    {
        _ = repeat;
        var inside = 0;
        var overlaps = 0;
        var log = new LogSlice
        {
            OnReduce = () =>
            {
                if (Interlocked.Increment(ref inside) > 1)
                {
                    Interlocked.Increment(ref overlaps);
                }

                // Widens the window a second drainer would have to overlap in (a spin, not a sleep).
                Thread.SpinWait(50);
                Interlocked.Decrement(ref inside);
            },
        };
        var store = Interleaving.Store(log);

        await Interleaving.Within(Produce((p, i) => Fire(store, new Add(p, i))));
        await Interleaving.Settled(store);

        Volatile.Read(ref overlaps).ShouldBe(0);
        store.State.Get<Log>().Entries.Count.ShouldBe(Producers * PerProducer);
    }

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispatch_PreservesPerProducerOrder(int repeat)
    {
        _ = repeat;
        var store = Interleaving.Store(new LogSlice());

        await Interleaving.Within(Produce((p, i) => Fire(store, new Add(p, i))));
        await Interleaving.Settled(store);

        var entries = store.State.Get<Log>().Entries;
        for (var p = 0; p < Producers; p++)
        {
            entries.Where(e => e.Producer == p).Select(e => e.Seq).ShouldBe(Enumerable.Range(0, PerProducer), $"producer {p}");
        }
    }

    // Two threads dispatch at the same instant, round after round, so one of them often enqueues exactly while the
    // other, the drainer, finds the queue empty and releases the drain. A stranded action never completes its
    // DispatchAsync, and the round waits on it forever: the 10 s bound fails the test.
    // Probabilistic, not a proof: the release window is a few instructions wide and only preemption opens it. A
    // split-lock mutant (_draining = false moved out of the empty-check lock) fails about 2% of repetitions; with a
    // Thread.Yield widening that window it fails every time. A deterministic check needs a seam inside Drain's
    // release path.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispatch_AfterDrainRelease_NoStrandedAction(int repeat)
    {
        _ = repeat;
        const int rounds = 200;
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = Interleaving.Store(new LogSlice());
        using var start = new Barrier(2);

        var producers = Enumerable.Range(0, 2).Select(p => Task.Run(
            async () =>
            {
                try
                {
                    for (var round = 0; round < rounds; round++)
                    {
                        start.SignalAndWait(cancellationToken);
                        (await store.DispatchAsync(new Add(p, round))).ShouldBe(DispatchResult.Reduced);
                    }
                }
                catch
                {
                    // Leave the barrier, so a failed assertion surfaces as itself, not as the other producer's timeout.
                    start.RemoveParticipant();
                    throw;
                }
            },
            cancellationToken));
        await Interleaving.Within(Task.WhenAll(producers));

        store.State.Get<Log>().Entries.Count.ShouldBe(2 * rounds);
    }

    // The 1.x deadlock: the thread that owns the drain blocks, inside a reducer, until another thread's Dispatch has
    // returned. Dispatch on a non-drainer only enqueues and returns (§7), so the owner is released and the queued action
    // is reduced right after Block.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Dispatch_OwnerBlocksOnCrossThreadDispatch_DoesNotDeadlock(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogSlice();
        var store = Interleaving.Store(log);
        using var handoff = new Barrier(2);
        Task? crossThread = null;
        log.OnBlock = () =>
        {
            crossThread = Task.Run(
                () =>
                {
                    store.Dispatch(new Add(1, 0));
                    handoff.SignalAndWait(cancellationToken);
                },
                cancellationToken);
            handoff.SignalAndWait(cancellationToken);
        };

        await Interleaving.Within(Task.Run(() => store.Dispatch(new Block()), cancellationToken));
        await Interleaving.Within(crossThread.ShouldNotBeNull());
        await Interleaving.Settled(store);

        store.State.Get<Log>().Entries.ShouldBe([new Add(1, 0)]);
        store.State.Version.ShouldBe(1);
    }

    // Producers threads start together on a barrier; produce(p, i) dispatches the i-th action of producer p and
    // returns its result task. Completes with every result once every producer has returned from its last dispatch.
    private static async Task<DispatchResult[]> Produce(Func<int, int, Task<DispatchResult>> produce)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var start = new Barrier(Producers);
        var producers = Enumerable.Range(0, Producers).Select(p => Task.Run(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                var results = new Task<DispatchResult>[PerProducer];
                for (var i = 0; i < PerProducer; i++)
                {
                    results[i] = produce(p, i);
                }

                return results;
            },
            cancellationToken));
        var all = await Task.WhenAll(producers);
        return await Task.WhenAll(all.SelectMany(r => r));
    }

    // Sync Dispatch has no result: it counts as Reduced here, and the final state checks it.
    private static Task<DispatchResult> Fire(DuckyStore store, object action)
    {
        store.Dispatch(action);
        return Task.FromResult(DispatchResult.Reduced);
    }
}

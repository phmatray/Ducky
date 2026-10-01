using System.Collections.Concurrent;
using System.Collections.Immutable;
using CsCheck;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 13 and the S-4 regression (docs/spec/spikes.md): CsCheck SampleParallel over the linearizable
// operations, awaited DispatchAsync (effective at reduce) and ReadState, against a sequential reducer model. The final
// state and every read must match one linearization. Selection.Value joins the operations when Selection<T> exists
// (stage 5).
public sealed class LinearizabilityTests
{
    // The S-4 counts, re-measured on the real dispatcher (spikes.md). iter stays CsCheck's (100, or CsCheck_Iter), so
    // PropertyLong can lengthen the run.
    private const int MaxSequentialOperations = 10;
    private const int MaxParallelOperations = 6;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Linearizability_DispatchVsModel(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;

        // Add is a record: a wide Seq range keeps two operations of one iteration from being equal, so a misplaced or
        // missing entry can't hide behind its twin.
        var dispatch = Gen.Int.Select(i => new Add(0, i)).Operation<Actual, Model>(
            a => $"DispatchAsync({a.Seq})", (a, add) => a.DispatchAndWait(add), (m, add) => m.State.Add(add));

        // Each ReadState carries a fresh key, so the snapshot the store returned is compared with the model's read of the
        // same operation in the linearization being tried.
        var read = Gen.Int.Select(_ => new object()).Operation<Actual, Model>(
            _ => "ReadState", (a, k) => a.Reads[k] = a.Store.State.Get<Log>().Entries, (m, k) => m.Reads[k] = [.. m.State]);

        // One bound over both CsCheck runs (Interleaving.WithinProperty); inside, each iteration blocks at most the 10 s of
        // Interleaving.Wait, so a deadlock fails with a thread dump within 10 s even under PropertyLong.
        await Interleaving.WithinProperty(Task.Run(
            () =>
            {
                Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                dispatch,
                read,
                equal: (a, m) => a.Store.State.Get<Log>().Entries.SequenceEqual(m.State)
                    && a.Reads.Count == m.Reads.Count
                    && m.Reads.All(r => a.Reads.TryGetValue(r.Key, out var seen) && seen.SequenceEqual(r.Value)),
                maxSequentialOperations: MaxSequentialOperations,
                maxParallelOperations: MaxParallelOperations);

                // Sync Dispatch is not linearizable (a non-drainer returns before its action is reduced, §7.3), so it gets
                // the final-state check alone: 2 to 6 producers fire 1 to 50 actions each, and once every producer has
                // returned and the last drain has exited, each action is in the log exactly once and each producer's are
                // in dispatch order.
                Gen.Int[1, 50].Array[2, 6].Sample(counts => FireAndSettle(counts, cancellationToken), threads: 1);
            },
            cancellationToken));
    }

    private static void FireAndSettle(int[] counts, CancellationToken cancellationToken)
    {
        var store = Interleaving.Store(new LogSlice());
        using var start = new Barrier(counts.Length);

        // Dedicated threads: up to six producers park on the barrier, more than a 4-core pool has idle.
        var producers = counts.Select((count, p) => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                for (var i = 0; i < count; i++)
                {
                    store.Dispatch(new Add(p, i));
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();
        Interleaving.Wait(Task.WhenAll(producers));

        // Nothing enqueues any more, so the last drain's exit is the moment the queue is empty (INV-03). It stands in
        // for WhenIdleAsync, which arrives with effects (stage 4).
        Interleaving.Wait(Interleaving.Settled(store));

        var entries = store.State.Get<Log>().Entries;
        for (var p = 0; p < counts.Length; p++)
        {
            entries.Where(e => e.Producer == p).Select(e => e.Seq).ShouldBe(Enumerable.Range(0, counts[p]), $"producer {p}");
        }

        entries.Count.ShouldBe(counts.Sum());
    }

    private sealed class Actual
    {
        public DuckyStore Store { get; } = Interleaving.Store(new LogSlice());

        public ConcurrentDictionary<object, ImmutableList<Add>> Reads { get; } = new();

        // A CsCheck operation is synchronous, so the awaited DispatchAsync blocks here until its action is reduced. Then
        // read-your-writes (INV-07): the committed snapshot already holds it. Without this check a task completed before
        // its commit shows only if another operation starts in that nanosecond gap.
        public void DispatchAndWait(Add action)
        {
            Interleaving.Wait(Store.DispatchAsync(action)).ShouldBe(DispatchResult.Reduced);
            Store.State.Get<Log>().Entries.ShouldContain(action);
        }

        public override string ToString() => $"[{string.Join(',', Store.State.Get<Log>().Entries.Select(e => e.Seq))}]";
    }

    private sealed class Model
    {
        public List<Add> State { get; } = [];

        public Dictionary<object, ImmutableList<Add>> Reads { get; } = [];

        public override string ToString() => $"[{string.Join(',', State.Select(e => e.Seq))}]";
    }
}

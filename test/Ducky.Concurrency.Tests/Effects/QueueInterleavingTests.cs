using System.Collections.Concurrent;
using ConcurrencyFixtures;
using CsCheck;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 18 and §6.6 (the Switch/Queue install through AddOrUpdate, a completing run's compare-and-remove of its
// own slot): INV-11. Producers on several threads dispatch to a Queue effect over two keys while earlier runs complete on
// pool threads (each handler yields first), so installs on the drainer race the completing runs' removals. Whatever the
// interleaving, every action's handler runs exactly once, runs of one key never overlap, and no slot is left at idle.
[Collection(nameof(Interleaving))]
public sealed class QueueInterleavingTests
{
    private const int MaxSequentialOperations = 10;
    private const int MaxParallelOperations = 6;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Queue_InstallRacesCompletionRemove_HandlerRunsOncePerAction(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var dispatch = Gen.Int[0, 1].Operation<Actual, Model>(k => $"Dispatch(key {k})", (a, k) => a.Dispatch(k), (m, _) => m.Dispatched++);
        var dispatchAsync = Gen.Int[0, 1].Operation<Actual, Model>(
            k => $"DispatchAsync(key {k})", (a, k) => a.DispatchAndWait(k), (m, _) => m.Dispatched++);

        // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
        await Interleaving.WithinProperty(Task.Run(
            () => Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                dispatch,
                dispatchAsync,
                equal: (a, m) => a.Settled() && a.Dispatched == m.Dispatched,
                maxSequentialOperations: MaxSequentialOperations,
                maxParallelOperations: MaxParallelOperations),
            cancellationToken));
    }

    private sealed class Actual
    {
        private readonly ConcurrentDictionary<int, int> _calls = new();
        private readonly int[] _active = new int[2];
        private int _overlaps;
        private int _lastSeq;
        private bool? _settled;

        public Actual() => Store = new DuckyStore([new LogSlice()], NullLogger.Instance, effects: () => [(new QueueEffect<Add>(add => add.Producer, Handle), false)]);

        public DuckyStore Store { get; }

        public int Dispatched => Volatile.Read(ref _lastSeq);

        public void Dispatch(int key) => Store.Dispatch(Next(key));

        public void DispatchAndWait(int key) => Interleaving.Wait(Store.DispatchAsync(Next(key))).ShouldBe(DispatchResult.Reduced);

        // Once, after the run (equal may be called once per linearization tried): idle means every run has ended, so each
        // dispatched action's handler ran exactly once, with no overlap per key, and every slot was removed.
        public bool Settled() => _settled ??= Settle();

        public override string ToString() =>
            $"{Dispatched} dispatched, {_calls.Count} handled, {_overlaps} overlaps, {Store.Dispatcher.SlotCount} slots";

        private Add Next(int key) => new(key, Interlocked.Increment(ref _lastSeq));

        private async Task Handle(Add add)
        {
            if (Interlocked.Increment(ref _active[add.Producer]) > 1)
            {
                Interlocked.Increment(ref _overlaps);
            }

            _calls.AddOrUpdate(add.Seq, 1, (_, n) => n + 1);

            // The rest of the run, its finally (compare-and-remove, then Done) included, continues on a pool thread.
            await Task.Yield();
            Interlocked.Decrement(ref _active[add.Producer]);
        }

        private bool Settle()
        {
            Interleaving.Wait(Store.WhenIdleAsync(TestContext.Current.CancellationToken));
            return Store.Dispatcher.SlotCount == 0
                && Volatile.Read(ref _overlaps) == 0
                && _calls.Count == Dispatched
                && _calls.Values.All(n => n == 1);
        }
    }

    private sealed class Model
    {
        public int Dispatched { get; set; }

        public override string ToString() => $"{Dispatched} dispatched";
    }
}

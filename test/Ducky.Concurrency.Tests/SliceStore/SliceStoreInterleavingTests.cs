namespace Ducky.Concurrency.Tests;

// SPEC §12 and §17.3 row 17 (INV-30): a SliceStore mutator runs at reduce time, on the drainer, against the latest state,
// so concurrent Set and SetAsync calls from many threads all apply (Bustand's lost updates read the state at call time).
public sealed class SliceStoreInterleavingTests
{
    private const int Producers = 8;
    private const int PerProducer = 250;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task SliceStore_ConcurrentSetFromNThreads_AppliesAll(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogStore();
        var store = Interleaving.Store(log);
        using var start = new Barrier(Producers);

        // Even producers use Set, odd ones SetAsync without awaiting between calls. Dedicated threads: a party parked on
        // the barrier must not hold a pool thread.
        var producers = Enumerable.Range(0, Producers).Select(p => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                var results = new List<Task<DispatchResult>>();
                for (var i = 0; i < PerProducer; i++)
                {
                    var entry = new Add(p, i);
                    if (p % 2 == 0)
                    {
                        log.Append(entry);
                    }
                    else
                    {
                        results.Add(log.AppendAsync(entry));
                    }
                }

                return results;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));
        var all = await Interleaving.Within(Task.WhenAll(producers));
        var results = await Interleaving.Within(Task.WhenAll(all.SelectMany(r => r)));
        await Interleaving.Settled(store);

        results.ShouldAllBe(r => r == DispatchResult.Reduced);
        log.State.Entries
            .Select(e => (e.Producer * PerProducer) + e.Seq)
            .Order()
            .ShouldBe(Enumerable.Range(0, Producers * PerProducer));
        store.State.Version.ShouldBe(Producers * PerProducer);
    }

    private sealed class LogStore : SliceStore<Log>
    {
        protected override Log Initial => new([]);

        public void Append(Add entry) => Set(s => s with { Entries = s.Entries.Add(entry) });

        public Task<DispatchResult> AppendAsync(Add entry) => SetAsync(s => s with { Entries = s.Entries.Add(entry) });
    }
}

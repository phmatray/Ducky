namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 7: the single writer publishes each commit with Volatile.Write (§6.2, §6.4 step 7), INV-07.
public sealed class SnapshotInterleavingTests
{
    private const int Writers = 4;
    private const int Readers = 4;
    private const int TicksPerWriter = 5000;
    private const int AwaitedPerWriter = 100;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Snapshot_VersionStrictlyMonotonic(int repeat)
    {
        _ = repeat;
        var store = Interleaving.Store(new LeftSlice(), new RightSlice());

        // Each reader sees the same snapshot again or a strictly newer one, never an older or a different one with the
        // same version.
        await ReadWhileTicking(store, () =>
        {
            var previous = store.InitialState;
            return snapshot =>
            {
                if (!ReferenceEquals(snapshot, previous))
                {
                    snapshot.Version.ShouldBeGreaterThan(previous.Version);
                }

                previous = snapshot;
            };
        });

        // Every Tick changes both slices: one commit each, one version step each.
        store.State.Version.ShouldBe(Writers * TicksPerWriter);
    }

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Snapshot_NeverTorn(int repeat)
    {
        _ = repeat;
        var store = Interleaving.Store(new LeftSlice(), new RightSlice());

        // Both slices and the version come from one commit.
        await ReadWhileTicking(store, () => snapshot =>
        {
            var left = snapshot.Get<Left>().Ticks;
            snapshot.Get<Right>().Ticks.ShouldBe(left, $"torn snapshot at version {snapshot.Version}");
            snapshot.Version.ShouldBe(left);
        });

        store.State.Get<Left>().Ticks.ShouldBe(Writers * TicksPerWriter);
    }

    // Each producer awaits its DispatchAsync and then reads the state: it must contain its own action, at a version no
    // lower than that action's commit, however many other producers reduced meanwhile.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Snapshot_AfterConcurrentReduces_IsNeverStale(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var store = Interleaving.Store(new LogSlice());
        using var start = new Barrier(Writers);

        // Dedicated threads, as in FireAndSettle: a party parked on the barrier must not hold a pool thread.
        var producers = Enumerable.Range(0, Writers).Select(p => Task.Factory.StartNew(
            async () =>
            {
                start.SignalAndWait(cancellationToken);
                for (var i = 0; i < AwaitedPerWriter; i++)
                {
                    var mine = new Add(p, i);
                    (await store.DispatchAsync(mine)).ShouldBe(DispatchResult.Reduced);
                    var snapshot = store.State;

                    // Containment is the INV-07 check: every Add commits exactly once and appends itself, so a snapshot
                    // holding mine is at or after mine's commit version (State.Version >= commit version).
                    snapshot.Get<Log>().Entries.ShouldContain(
                        mine,
                        $"stale snapshot at version {snapshot.Version} after {mine}");
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap());
        await Interleaving.Within(Task.WhenAll(producers));

        store.State.Version.ShouldBe(Writers * AwaitedPerWriter);
    }

    // Writers dispatch Ticks while readers, each with its own check from newReader, read State until the writers are
    // done, then once more. All start on one barrier.
    private static async Task ReadWhileTicking(DuckyStore store, Func<Action<StateSnapshot>> newReader)
    {
        // Cancelled once the choreography ends or times out, so a deadlocked writer does not leave readers spinning
        // (or anyone parked on the barrier) into the next repetitions.
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var cancellationToken = stop.Token;
        using var start = new Barrier(Writers + Readers);
        // Dedicated threads, as in FireAndSettle: Writers + Readers parties parked on the barrier must not hold pool
        // threads, which a cold 2-vCPU pool injects at about one a second.
        var writers = Task.WhenAll(Enumerable.Range(0, Writers).Select(_ => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                for (var i = 0; i < TicksPerWriter; i++)
                {
                    store.Dispatch(new Tick());
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)));
        var readers = Enumerable.Range(0, Readers).Select(_ => Task.Factory.StartNew(
            () =>
            {
                var check = newReader();
                start.SignalAndWait(cancellationToken);
                while (!writers.IsCompleted && !cancellationToken.IsCancellationRequested)
                {
                    check(store.State);
                }

                check(store.State);
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));

        try
        {
            await Interleaving.Within(Task.WhenAll([writers, .. readers]));
        }
        finally
        {
            await stop.CancelAsync();
        }

        await Interleaving.Settled(store);
    }
}

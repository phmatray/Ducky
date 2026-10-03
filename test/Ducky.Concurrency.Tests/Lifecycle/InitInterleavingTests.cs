using CsCheck;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 14 against the init buffer and InitCoordinator (§6.3, §6.7): INV-04, INV-05, INV-13.
[Collection(nameof(Interleaving))]
public sealed class InitInterleavingTests
{
    // Producers on dedicated threads dispatch System actions (main queue) and Local ones (init buffer before Ready), and
    // one of them opens the init gate after a random number of dispatches, so Ready lands anywhere in the stream, on a
    // producer that may become the drainer. Whatever the interleaving, the processing order is INV-04's: the System actions
    // queued before Ready, StoreInitialized, the buffered user actions in enqueue (Id) order, then everything later in Id
    // order; each action exactly once.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task InitBuffer_PreservesUserActionOrderAcrossGate(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;

        // 2 to 5 producers of 1 to 20 actions each (true: System), and the dispatch count that opens the gate (0: before any).
        var runs = Gen.Bool.Array[1, 20].Array[2, 5]
            .SelectMany(plan => Gen.Int[0, plan.Sum(origins => origins.Length)].Select(open => (plan, open)));

        await Interleaving.WithinProperty(Task.Run(
            () => runs.Sample(run => AcrossGate(run.plan, run.open, cancellationToken), threads: 1),
            cancellationToken));
    }

    // INV-05: no init start runs under _gate, whichever INV-13 trigger starts it (Enqueue flags startInit under _gate and
    // calls Start after releasing it; State, Select, InitializeAsync and WhenIdleAsync start before taking it). The six
    // triggers race, and the init's synchronous part, on whichever thread won, waits for another thread's Dispatch, which
    // takes _gate: were Start running under _gate, that Dispatch could never enter it.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task InitStart_NeverRunsUnderGate(int repeat)
    {
        const int Triggers = 6;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogSlice();
        DuckyStore store = null!;
        var starts = 0;
        var crossThreadEntered = false;
        var probe = new HookProbe
        {
            Init = _ =>
            {
                Interlocked.Increment(ref starts);
                Interleaving.Wait(Dedicated(() => store.Dispatch(new Add(-1, 0)), cancellationToken));
                crossThreadEntered = true; // not reached when the Wait times out
                return ValueTask.CompletedTask;
            },
        };
        store = new DuckyStore([log], NullLogger.Instance, initTimeout: Timeout.InfiniteTimeSpan, middleware: () => [probe]);

        // One INV-13 trigger per thread, rotated by repeat so a scheduler that favours one thread slot still lets every
        // trigger win the start across the repeats.
        static int TriggerOf(int p, int repeat) => (p + repeat) % Triggers;
        Task Fire(int p)
        {
            switch (TriggerOf(p, repeat))
            {
                case 0:
                    store.Dispatch(new Add(p, 0));
                    return Task.CompletedTask;
                case 1:
                    return store.DispatchAsync(new Add(p, 0));
                case 2:
                    return store.InitializeAsync(cancellationToken);
                case 3:
                    _ = store.State;
                    return Task.CompletedTask;
                case 4:
                    store.Select(s => s).Dispose();
                    return Task.CompletedTask;
                default:
                    return store.WhenIdleAsync(cancellationToken);
            }
        }

        var pending = new Task[Triggers];
        using var start = new Barrier(Triggers);
        await Interleaving.Within(Task.WhenAll(Enumerable.Range(0, Triggers).Select(p => Dedicated(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                pending[p] = Fire(p);
            },
            cancellationToken))));
        await Interleaving.Within(Task.WhenAll(pending));
        await Interleaving.Within(store.WhenIdleAsync(cancellationToken));

        // A Wait that timed out inside Init would be a faulted init, which the store only logs: assert the step itself.
        Volatile.Read(ref starts).ShouldBe(1);
        crossThreadEntered.ShouldBeTrue();
        var dispatched = Enumerable.Range(0, Triggers).Where(p => TriggerOf(p, repeat) <= 1);
        store.State.Get<Log>().Entries.Select(e => e.Producer).Order().ShouldBe(dispatched.Prepend(-1));
    }

    // INV-13: an overflow before Running (between the thread that flagged startInit leaving _gate and Start, or while
    // Starting) is a no-op that leaves the abort flag unset, and Start step 3 re-checks. Every producer dispatches one user
    // action into a buffer of capacity 1; the first init's synchronous part holds Starting until every other producer has
    // returned, so no overflow can land after Running. Both middleware inits still start before Ready, and the overflow
    // abort, not the (infinite) InitTimeout, makes the store Ready; nothing is dropped.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task Init_OverflowBetweenFlagAndStart_MiddlewareInitStillRuns(int repeat)
    {
        _ = repeat;
        const int Producers = 6;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogSlice();
        var othersReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var returned = 0;
        var started = 0;
        var aborted = 0;
        var readies = 0;
        var startedAtReady = -1;
        var heldStarting = false;
        ValueTask Init(CancellationToken token)
        {
            Interlocked.Increment(ref started);
            var abort = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _ = token.Register(() =>
            {
                Interlocked.Increment(ref aborted);
                abort.TrySetResult();
            });
            return new(abort.Task);
        }

        var first = new HookProbe
        {
            Init = token =>
            {
                Interleaving.Wait(othersReturned.Task);
                heldStarting = true; // not reached when the Wait times out
                return Init(token);
            },
            Before = context =>
            {
                if (context.Action is StoreInitialized)
                {
                    readies++;
                    startedAtReady = Volatile.Read(ref started);
                }
            },
        };
        var second = new HookProbe { Init = Init };
        var store = new DuckyStore(
            [log],
            NullLogger.Instance,
            initTimeout: Timeout.InfiniteTimeSpan,
            middleware: () => [first, second],
            initBufferCapacity: 1);
        using var start = new Barrier(Producers);

        await Interleaving.Within(Task.WhenAll(Enumerable.Range(0, Producers).Select(p => Dedicated(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                store.Dispatch(new Add(p, 0));
                if (Interlocked.Increment(ref returned) == Producers - 1)
                {
                    othersReturned.SetResult();
                }
            },
            cancellationToken))));
        await Interleaving.Within(store.InitializeAsync(cancellationToken));
        await Interleaving.Within(store.WhenIdleAsync(cancellationToken));

        heldStarting.ShouldBeTrue();
        readies.ShouldBe(1);
        startedAtReady.ShouldBe(2);
        Volatile.Read(ref aborted).ShouldBe(2);
        store.State.Get<Log>().Entries.Select(e => e.Producer).Order().ShouldBe(Enumerable.Range(0, Producers));
    }

    // INV-13: another thread is draining (parked in a System action's reducer) when init completes, so the hydration
    // restore and then StoreInitialized are only queued behind that drain. The gate is opened without
    // RunContinuationsAsynchronously, so the init's restore, its completion and MarkReady run inline on this thread while the
    // drainer is parked. The shared init task completes only once StoreInitialized is processed: InitializeAsync then
    // observes both the restore and StoreInitialized committed.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task InitializeAsync_WithConcurrentDrainer_CompletesAfterStoreInitializedReduced(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var log = new LogSlice();
        var gate = new TaskCompletionSource();
        var parked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var boot = new BootSlice();
        DuckyStore store = null!;
        var hydration = new HookProbe
        {
            Init = async _ =>
            {
                await gate.Task.ConfigureAwait(false);
                store.Restore(new Dictionary<string, object> { [boot.Key] = new Boot(7, false) }, Origin.Hydration);
            },
        };
        store = new DuckyStore([log, boot], NullLogger.Instance, initTimeout: Timeout.InfiniteTimeSpan, middleware: () => [hydration]);
        // No cancellable token, so this is the shared init task itself, not a WaitAsync that completes it asynchronously:
        // its IsCompleted below reads the shared task's state at that instant.
        var initialized = store.InitializeAsync(CancellationToken.None);
        log.OnBlock = () =>
        {
            parked.SetResult();
            release.Task.Wait(cancellationToken);
        };
        var drainer = Dedicated(() => store.Dispatcher.Dispatch(new Block(), Origin.System), cancellationToken);
        await Interleaving.Within(parked.Task);

        try
        {
            gate.SetResult();

            initialized.IsCompleted.ShouldBeFalse();
            release.SetResult();
            await Interleaving.Within(initialized);
            store.State.Get<Boot>().ShouldBe(new Boot(7, true));
            await Interleaving.Within(drainer);
        }
        finally
        {
            release.TrySetResult(); // a failed assertion must not leave the drainer thread parked
        }
    }

    // One run of the property: every blocking step goes through Interleaving.Wait, the progress WithinProperty watches.
    private static void AcrossGate(bool[][] plan, int open, CancellationToken cancellationToken)
    {
        // No RunContinuationsAsynchronously: the init resumes, reaches Ready and may drain inline on the opening producer.
        var gate = new TaskCompletionSource();
        List<ActionContext> processed = []; // written by the drainer only, read after idle
        var store = new DuckyStore(
            [new LogSlice()],
            NullLogger.Instance,
            initTimeout: Timeout.InfiniteTimeSpan,
            middleware: () => [new HookProbe { Init = async _ => await gate.Task.ConfigureAwait(false), Before = processed.Add }]);
        var dispatched = 0;
        if (open == 0)
        {
            gate.SetResult();
        }

        using var start = new Barrier(plan.Length);
        var producers = plan.Select((origins, p) => Dedicated(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                for (var i = 0; i < origins.Length; i++)
                {
                    if (origins[i])
                    {
                        store.Dispatcher.Dispatch(new Add(p, i), Origin.System);
                    }
                    else
                    {
                        store.Dispatch(new Add(p, i));
                    }

                    if (Interlocked.Increment(ref dispatched) == open)
                    {
                        gate.SetResult();
                    }
                }
            },
            cancellationToken)).ToArray();
        Interleaving.Wait(Task.WhenAll(producers));
        Interleaving.Wait(store.WhenIdleAsync(cancellationToken));

        processed.Where(c => c.Action is StoreInitialized).ShouldHaveSingleItem();
        processed.Select(c => c.Action).OfType<Add>().Select(a => (a.Producer, a.Seq)).Order()
            .ShouldBe(plan.SelectMany((origins, p) => origins.Select((_, i) => (p, i))));
        var ready = processed.Single(c => c.Action is StoreInitialized).Id;
        long[] expected =
        [
            .. processed.Where(c => c.Id < ready && c.Origin == Origin.System).Select(c => c.Id).Order(),
            ready,
            .. processed.Where(c => c.Id < ready && c.Origin == Origin.Local).Select(c => c.Id).Order(),
            .. processed.Where(c => c.Id > ready).Select(c => c.Id).Order(),
        ];
        processed.Select(c => c.Id).ShouldBe(expected);
    }

    // Blocking parties get their own thread, never a pool thread a cold 2-vCPU runner may not have (M1 CI incident).
    private static Task Dedicated(Action body, CancellationToken cancellationToken) =>
        Task.Factory.StartNew(body, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
}

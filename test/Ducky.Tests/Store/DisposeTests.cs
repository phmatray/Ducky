using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.11 steps 1-3, the step-4 init waits and phase 5a's per-middleware bounds, the after-disposal paths, §6.7 (Retire
// and the shared init task at disposal); INV-10, INV-13, INV-29. Effect and subscriber phases come with their features.
public sealed class DisposeTests
{
    private static string[] Steps(DuckyStore store) => [.. store.State.Get<Trail>().Steps];

    [Fact]
    public async Task Dispose_Twice_IsNoOp()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new CountSlice()], logger);

        var first = store.DisposeAsync().AsTask();
        var second = store.DisposeAsync().AsTask();

        // One disposal task for every caller; the steps ran once, and a second call starts nothing.
        second.ShouldBeSameAs(first);
        await first;
        await store.DisposeAsync();
        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeTrue();
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task SyncDispose_LogsWarningAndDelegates()
    {
        var logger = new FakeLogger();
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], logger, middleware: () => [gate]);
        var buffered = store.DispatchAsync(new Mark("a"));

        store.Dispose();

        // Dispose() is `_ = DisposeAsync();`: the disposal already started, and DisposeAsync hands back its task.
        (await buffered).ShouldBe(DispatchResult.Disposed);
        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeTrue();
        var disposal = store.DisposeAsync().AsTask();

        // The gate's init ignores its token: the middleware is disposed, and the disposal completes, once it ends.
        gate.Release();
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1010);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("prefer DisposeAsync; pending persistence writes may be lost");
    }

    [Fact]
    public async Task InitializeAsync_DisposedBeforeReady_Completes()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        var buffered = store.DispatchAsync(new Mark("a"));

        var disposal = store.DisposeAsync().AsTask();

        // Ready was never reached: dispose completes the shared init task itself, and the buffered action Disposed.
        await initialized;
        initialized.IsCompletedSuccessfully.ShouldBeTrue();
        (await buffered).ShouldBe(DispatchResult.Disposed);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // An init that ends after disposal makes nothing Ready: StoreInitialized is never reduced.
        gate.Release();
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        Steps(store).ShouldBeEmpty();
        store.State.Version.ShouldBe(0);
    }

    [Fact]
    public async Task Dispose_ReentrantFromLifetimeCallback_RunsOnce()
    {
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Task? reentrant = null;

        // Step 2's Cancel() runs this inline, before the first DisposeAsync call has returned.
        store.Dispatcher.Lifetime.Register(() =>
        {
            reentrant = store.DisposeAsync().AsTask();
        });

        var first = store.DisposeAsync().AsTask();

        reentrant.ShouldBeSameAs(first);
        await first;
    }

    // Non-normative: after disposal every entry point is inert and none throws (§6.11, §7 rule 10).
    [Fact]
    public async Task AfterDispose_EntryPoints_InertAndNoneThrows()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new CountSlice()], logger);
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Reduced);
        var last = store.State;
        await store.DisposeAsync();

        store.Dispatch(new Bump());
        (await store.DispatchAsync(new Bump())).ShouldBe(DispatchResult.Disposed);
        store.Restore(new Dictionary<string, object> { ["count"] = new Count(7) }, Origin.DevTools);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        store.State.ShouldBeSameAs(last);
        store.State.Get<Count>().Value.ShouldBe(1);
        var records = logger.Collector.GetSnapshot();
        records.Count.ShouldBe(3);
        records.ShouldAllBe(r => r.Id.Id == 1002 && r.Level == LogLevel.Debug);
        records[0].Message.ShouldBe($"{typeof(Bump)} ignored: the store is disposed");
        records[2].Message.ShouldBe($"{typeof(HydrateSlices)} ignored: the store is disposed");
    }

    // Non-normative: disposal retires an init that never started, so a later State read or InitializeAsync starts none
    // (§6.7 Retire, §6.11 step 1, INV-29) and InitializeAsync still completes.
    [Fact]
    public async Task AfterDispose_NeverInitialized_StateAndInitializeAsyncStartNoInit()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, middleware: () => [gate]);
        await store.DisposeAsync();

        _ = store.State;
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        gate.Started.ShouldBe(0);
    }

    // Non-normative: a dispose from inside the drain detaches what is queued behind it and completes once the drain has
    // exited, within DisposeTimeout (step 3, no Warning). The re-entrant caller gets an incomplete task, not a deadlock.
    [Fact]
    public async Task Dispose_FromReducer_DetachesQueuedAndCompletesAfterDrainExit()
    {
        var logger = new FakeLogger();
        var slice = new CountSlice();
        var store = new DuckyStore([slice], logger);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Task<DispatchResult>? queued = null;
        Task? disposal = null;
        var completedInDrain = true;
        slice.OnProbe = () =>
        {
            queued = store.DispatchAsync(new Bump());
            disposal = store.DisposeAsync().AsTask();
            completedInDrain = disposal.IsCompleted;
        };

        store.Dispatch(new Probe());

        completedInDrain.ShouldBeFalse();
        await disposal.ShouldNotBeNull();
        (await queued.ShouldNotBeNull()).ShouldBe(DispatchResult.Disposed);
        slice.Reduced.ShouldBe(["Probe start", "Probe end"]);
        logger.Collector.Count.ShouldBe(0);
    }

    // Non-normative: a drain that overruns DisposeTimeout (from DI, on the DI TimeProvider) logs Warning 1011, and
    // DisposeAsync completes at that bound rather than at the drain's exit (step 3).
    [Fact]
    public async Task Dispose_DrainOverrunsDisposeTimeout_CompletesAtBoundWithWarning()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<DuckyStore>();
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<ILogger<DuckyStore>>(logger)
            .AddDucky(d =>
            {
                d.Lifetime = ServiceLifetime.Singleton;
                d.DisposeTimeout = TimeSpan.FromSeconds(5);
                d.AddSlice<CountSlice>();
            });
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        logger.Collector.Clear();
        Task? disposal = null;
        var beforeBound = true;
        var atBound = false;
        provider.GetRequiredService<CountSlice>().OnProbe = () =>
        {
            disposal = store.DisposeAsync().AsTask();
            time.Advance(TimeSpan.FromSeconds(2));
            beforeBound = disposal.IsCompleted;
            time.Advance(TimeSpan.FromSeconds(3));
            atBound = logger.Collector.Count == 1 && disposal.IsCompleted;
        };

        store.Dispatch(new Probe());

        beforeBound.ShouldBeFalse();
        atBound.ShouldBeTrue();
        await disposal.ShouldNotBeNull();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1011);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("The drain did not exit within DisposeTimeout (00:00:05); DisposeAsync completes without it");
    }

    public static TheoryData<TimeSpan, bool> UnusualDisposeTimeouts => new()
    {
        { TimeSpan.FromSeconds(-5), true },
        { TimeSpan.MaxValue, false },
        { Timeout.InfiniteTimeSpan, false },
    };

    // Non-normative: a DisposeTimeout that Task.WaitAsync rejects is clamped, never thrown (INV-10, INV-29): a negative one
    // waits not at all (Warning 1011 at once), TimeSpan.MaxValue waits the longest bound, and InfiniteTimeSpan is kept.
    [Theory]
    [MemberData(nameof(UnusualDisposeTimeouts))]
    public async Task Dispose_UnusualDisposeTimeout_FromReducer_Completes(TimeSpan timeout, bool atOnce)
    {
        var logger = new FakeLogger();
        var slice = new CountSlice();
        var store = new DuckyStore([slice], logger, disposeTimeout: timeout);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Task? disposal = null;
        var completedInDrain = false;
        slice.OnProbe = () =>
        {
            disposal = store.DisposeAsync().AsTask();
            completedInDrain = disposal.IsCompleted;
        };

        store.Dispatch(new Probe());

        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        completedInDrain.ShouldBe(atOnce);
        logger.Collector.GetSnapshot().Select(r => r.Message).ShouldBe(
            atOnce ? ["The drain did not exit within DisposeTimeout (00:00:00); DisposeAsync completes without it"] : []);
    }

    // Non-normative: a throwing lifetime callback is logged (Error 1013) and never faults DisposeAsync (step 2).
    [Fact]
    public async Task Dispose_ThrowingLifetimeCallback_LogsErrorAndCompletes()
    {
        var logger = new FakeLogger();
        var store = new DuckyStore([new CountSlice()], logger);
        var thrown = new InvalidOperationException("callback");
        store.Dispatcher.Lifetime.Register(() => throw thrown);

        await store.DisposeAsync();

        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1013);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe("A cancellation callback threw");
        record.Exception.ShouldBeOfType<AggregateException>().InnerExceptions.ShouldHaveSingleItem().ShouldBeSameAs(thrown);
    }

    [Fact]
    public async Task Dispose_DuringInit_TimerNeverFires_NoAbortLog()
    {
        // Retire disposes the armed InitTimeout timer (§6.7): its callback never runs on a disposed store, so nothing logs
        // StoreInitAborted and only the disposal cancels the init token.
        var time = new FakeTimeProvider();
        var timers = new CountingTimeProvider(time);
        var logger = new FakeLogger();
        var journal = new List<string>();
        CancellationToken token = default;
        var r = new Recorder("r", journal)
        {
            OnInit = t =>
            {
                token = t;
                return new(Task.Delay(Timeout.InfiniteTimeSpan, time, t));
            },
        };
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(1),
            disposeTimeout: TimeSpan.FromHours(1), timeProvider: timers, middleware: () => [r]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);

        await store.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(5));

        timers.Fired.ShouldBe(0);
        await initialized;
        token.IsCancellationRequested.ShouldBeTrue();
        logger.Collector.Count.ShouldBe(0);
        journal.ShouldBe(["r.DisposeAsync"]);
        Steps(store).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_MiddlewareInitInFlight_DisposeAsyncAfterInitTaskEnds()
    {
        // Each middleware waits for its own init task only (§6.11 step 4): done (init complete) is disposed at once, slow
        // when its init ends within DisposeTimeout, and late, whose init outlasts the bound, gets its DisposeAsync chained on
        // that init alone while DisposeAsync completes at the bound. Init tasks here ignore the token.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var slowInit = new TaskCompletionSource();
        var lateInit = new TaskCompletionSource();
        var slowDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var late = new Recorder("late", journal)
        {
            OnInit = _ => new(lateInit.Task),
            OnDispose = () =>
            {
                lateDisposed.SetResult();
                return default;
            },
        };
        var slow = new Recorder("slow", journal)
        {
            OnInit = _ => new(slowInit.Task),
            OnDispose = () =>
            {
                slowDisposed.SetResult();
                return default;
            },
        };
        var done = new Recorder("done", journal);
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromHours(1),
            disposeTimeout: TimeSpan.FromSeconds(2), timeProvider: time, middleware: () => [late, slow, done]);
        _ = store.InitializeAsync(TestContext.Current.CancellationToken);

        var disposal = store.DisposeAsync().AsTask();

        journal.ShouldBe(["done.DisposeAsync"]);
        slowInit.SetResult();
        await slowDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["done.DisposeAsync", "slow.DisposeAsync"]);
        disposal.IsCompleted.ShouldBeFalse();

        time.Advance(TimeSpan.FromSeconds(2));

        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["done.DisposeAsync", "slow.DisposeAsync"]);
        lateInit.SetResult();
        await lateDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["done.DisposeAsync", "slow.DisposeAsync", "late.DisposeAsync"]);
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Dispose_InitNeverStarted_MiddlewareDisposedAtOnce()
    {
        // Restore materializes the store but never starts init: Retire replaces NotStarted, so there is no init to wait for
        // (InitTasks is empty) and the middleware is disposed synchronously, before any time passes.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var r = new Recorder("r", journal);
        var store = new DuckyStore([new TrailSlice()], logger, disposeTimeout: TimeSpan.FromSeconds(2), timeProvider: time,
            middleware: () => [r]);
        store.Restore(new Dictionary<string, object>(), Origin.Hydration);
        store.Dispatcher.Initializer.PrefixDone.IsCompleted.ShouldBeFalse();

        var disposal = store.DisposeAsync().AsTask();

        journal[^1].ShouldBe("r.DisposeAsync");
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Dispose_DuringStartSyncPrefix_AwaitsStartedInitsAndStartsNoMore()
    {
        // A dispose from first's synchronous part retires init (§6.7): later's init never starts, Start arms no timer, and
        // the disposal, which received an incomplete task, disposes later at once (nothing to wait for) and first only once
        // its own init ended.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var firstInit = new TaskCompletionSource();
        var laterDisposed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? disposal = null;
        var first = new Recorder("first", journal);
        first.OnInit = _ =>
        {
            disposal = first.AttachedStore.DisposeAsync().AsTask();
            return new(firstInit.Task);
        };
        var later = new Recorder("later", journal)
        {
            OnInit = _ =>
            {
                journal.Add("later.InitializeAsync");
                return default;
            },
            OnDispose = () =>
            {
                laterDisposed.SetResult();
                return default;
            },
        };
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(1), timeProvider: time,
            middleware: () => [first, later]);

        var a = store.DispatchAsync(new Mark("a"));

        // Step 2 still ran for the init that did start: InitTasks has no entry for the skipped middleware, and _prefixDone
        // completed although Retire won the CAS from Starting.
        var initializer = store.Dispatcher.Initializer;
        initializer.PrefixDone.IsCompleted.ShouldBeTrue();
        initializer.InitTasks.Length.ShouldBe(2);
        initializer.InitTasks[0].ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        initializer.InitTasks[1].ShouldBeNull();
        (await a).ShouldBe(DispatchResult.Disposed);
        await laterDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["later.DisposeAsync"]);
        disposal.ShouldNotBeNull().IsCompleted.ShouldBeFalse();

        firstInit.SetResult();

        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["later.DisposeAsync", "first.DisposeAsync"]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(5));
        logger.Collector.Count.ShouldBe(0);
        Steps(store).ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_InitAwaitingWhenIdle_MiddlewareStillDisposed()
    {
        // An init awaiting WhenIdleAsync before Ready ends at step 1, where the idle waiters complete, so the middleware's
        // own init wait ends and it is disposed after it, long before any bound (both bounds are an hour here).
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var r = new Recorder("r", journal);
        r.OnInit = async _ =>
        {
            await r.AttachedStore.WhenIdleAsync(CancellationToken.None);

            // Off the disposing thread, so only the init wait can order the DisposeAsync after this.
            await Task.Yield();
            journal.Add("r.InitializeAsync ended");
        };
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromHours(1),
            disposeTimeout: TimeSpan.FromHours(1), timeProvider: time, middleware: () => [r]);
        _ = store.InitializeAsync(TestContext.Current.CancellationToken);

        await store.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        journal.ShouldBe(["r.InitializeAsync ended", "r.DisposeAsync"]);
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Dispose_HungMiddlewareDispose_OthersDisposedAndDisposeAsyncCompletes()
    {
        // Each DisposeAsync is awaited with WaitAsync(DisposeTimeout): last's hangs, so at the bound phase 5a logs Warning
        // 1016 naming it and disposes first, and DisposeAsync completes.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var journal = new List<string>();
        var hung = new TaskCompletionSource();
        var first = new Recorder("first", journal);
        var last = new Recorder("last", journal) { OnDispose = () => new(hung.Task) };
        var store = new DuckyStore([new CountSlice()], logger, disposeTimeout: TimeSpan.FromSeconds(2), timeProvider: time,
            middleware: () => [first, last]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        journal.Clear();

        var disposal = store.DisposeAsync().AsTask();
        time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));

        journal.ShouldBe(["last.DisposeAsync"]);
        disposal.IsCompleted.ShouldBeFalse();
        logger.Collector.Count.ShouldBe(0);

        time.Advance(TimeSpan.FromTicks(1));

        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        journal.ShouldBe(["last.DisposeAsync", "first.DisposeAsync"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1016);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe(
            $"DisposeAsync of middleware {typeof(Recorder)} did not complete within DisposeTimeout (00:00:02); disposal goes on without it");
        hung.SetResult();
    }

    [Fact]
    public async Task Dispose_HungMiddlewareDisposeThrowsAfterBound_LoggedAsError1015()
    {
        // Past the bound the store leaves that DisposeAsync running but still observes it: a later throw is Error 1015
        // (§6.11 step 5), never an unobserved task exception. The observer resumes inline in SetException.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var hung = new TaskCompletionSource();
        var r = new Recorder("r", []) { OnDispose = () => new(hung.Task) };
        var store = new DuckyStore([new CountSlice()], logger, disposeTimeout: TimeSpan.FromSeconds(2), timeProvider: time,
            middleware: () => [r]);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        var disposal = store.DisposeAsync().AsTask();
        time.Advance(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        hung.SetException(new InvalidOperationException("late"));

        logger.Collector.GetSnapshot().Select(l => (l.Id.Id, l.Level))
            .ShouldBe([(1016, LogLevel.Warning), (1015, LogLevel.Error)]);
        logger.Collector.LatestRecord.Exception.ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("late");
    }
}

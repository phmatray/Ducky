using Ducky.Tests.InitFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.3 (init buffer, MarkReady), §6.7 (init lifecycle, InitTimeout abort), §5.2 (Restore); INV-04, INV-13. An InitGate
// middleware keeps the store before Ready until the test releases it; the timeout tests run on a FakeTimeProvider.
public sealed class InitTests
{
    private static string[] Steps(DuckyStore store) => [.. store.State.Get<Trail>().Steps];

    [Fact]
    public async Task Init_AutoStartsOnFirstDispatch()
    {
        // Dispatch: registry reads start nothing; the first dispatch starts init, before Ready its action is buffered.
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        _ = store.Slices;
        _ = store.InitialState;
        gate.Started.ShouldBe(0);
        store.Dispatch(new Mark("a"));
        gate.Started.ShouldBe(1);
        Steps(store).ShouldBeEmpty();
        gate.Release();
        Steps(store).ShouldBe(["init", "a"]);

        // DispatchAsync.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        var dispatched = store.DispatchAsync(new Mark("b"));
        gate.Started.ShouldBe(1);
        dispatched.IsCompleted.ShouldBeFalse();
        gate.Release();
        (await dispatched).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "b"]);

        // A State read: with no middleware, init completes synchronously, so the read already sees StoreInitialized.
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance);
        Steps(store).ShouldBe(["init"]);

        // InitializeAsync.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        gate.Started.ShouldBe(1);
        gate.Release();
        await initialized;
        Steps(store).ShouldBe(["init"]);
    }

    [Fact]
    public async Task Init_StoreInitializedExactlyOnce()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);

        // Every trigger while init runs, and again once it completed: init starts once, StoreInitialized is reduced once.
        var first = store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatch(new Mark("a"));
        var second = store.InitializeAsync(TestContext.Current.CancellationToken);
        _ = store.State;
        gate.Release();
        await first;
        await second;
        store.Dispatch(new Mark("b"));
        (await store.DispatchAsync(new Mark("c"))).ShouldBe(DispatchResult.Reduced);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        gate.Started.ShouldBe(1);
        Steps(store).ShouldBe(["init", "a", "b", "c"]);
    }

    [Fact]
    public async Task Init_StartAfterDisposalBegan_MaterializesNothing()
    {
        // Non-normative: a Start that races dispose (disposal published, Retire not yet run) builds no middleware, as
        // Dispatcher.Materialize does. A fresh coordinator on a disposed dispatcher stands for that window.
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance);
        await store.DisposeAsync();
        var materialized = new Lazy<Materialized>(() => throw new InvalidOperationException("built after disposal began"));
        var init = new InitCoordinator(materialized, new SafeLogger(NullLogger.Instance), TimeProvider.System, Timeout.InfiniteTimeSpan, CancellationToken.None);

        init.Start(store.Dispatcher);

        materialized.IsValueCreated.ShouldBeFalse();
        init.PrefixDone.IsCompletedSuccessfully.ShouldBeTrue();
        init.InitTasks.ShouldBeEmpty();
    }

    [Fact]
    public void Init_MiddlewareInitializeThrowsSynchronously_StoreStillReady()
    {
        // §6.7 step 1: a non-async override that throws (here Restore with a non-restore origin) counts as a finished init,
        // like a faulted task: it is logged at Error, the next middleware still starts, and nothing escapes into Dispatch.
        var logger = new FakeLogger();
        var started = 0;
        var store = new DuckyStore([new TrailSlice()], logger, middleware: () =>
        [
            new InitProbe((probe, _) =>
            {
                probe.AttachedStore.Restore(new Dictionary<string, object>(), Origin.System);
                return default;
            }),
            new InitProbe((_, _) =>
            {
                started++;
                return default;
            }),
        ]);

        store.Dispatch(new Mark("a"));

        started.ShouldBe(1);
        Steps(store).ShouldBe(["init", "a"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1032);
        record.Level.ShouldBe(LogLevel.Error);
        record.Exception.ShouldBeOfType<ArgumentOutOfRangeException>();
        record.Message.ShouldBe($"InitializeAsync of middleware {typeof(InitProbe)} failed; init counts it as finished");
    }

    [Fact]
    public void Init_MiddlewareSyncPrefixesAllRunBeforeStartReturns()
    {
        // Every InitializeAsync starts, in registration order, without awaiting the previous one: each synchronous part
        // (up to its first await) has run when the triggering Dispatch returns, and the asynchronous parts are still pending.
        List<string> journal = [];
        var first = new TaskCompletionSource();
        var second = new TaskCompletionSource();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () =>
        [
            new InitProbe(async (_, _) =>
            {
                journal.Add("first sync");
                await first.Task;
                journal.Add("first async");
            }),
            new InitProbe(async (_, _) =>
            {
                journal.Add("second sync");
                await second.Task;
                journal.Add("second async");
            }),
        ]);

        store.Dispatch(new Mark("a"));

        journal.ShouldBe(["first sync", "second sync"]);
        Steps(store).ShouldBeEmpty();

        // Init completes when the last one ends, whatever the order.
        second.SetResult();
        Steps(store).ShouldBeEmpty();
        first.SetResult();
        journal.ShouldBe(["first sync", "second sync", "second async", "first async"]);
        Steps(store).ShouldBe(["init", "a"]);
    }

    [Fact]
    public void Init_AllInitsCompleteSynchronously_FirstDispatchReducedInline()
    {
        // Every InitializeAsync returned a completed task: steps 4-5 run on the caller, MarkReady drains inline, and the
        // Dispatch that started init returns after its action is reduced.
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () =>
        [
            new InitProbe((_, _) => default),
            new InitProbe((probe, _) =>
            {
                probe.System(new Mark("system"));
                return ValueTask.CompletedTask;
            }),
            new Bare(),
        ]);

        store.Dispatch(new Mark("a"));

        store.Dispatcher.State.Get<Trail>().Steps.ShouldBe(["system", "init", "a"]);
    }

    [Fact]
    public async Task Init_HangingMiddleware_TimesOutAndReleasesBuffer()
    {
        // A middleware whose init ignores its token holds Ready for InitTimeout (from DI, on the DI TimeProvider), not
        // forever: the abort cancels the init token, logs StoreInitAborted once and releases the buffer in order.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger<DuckyStore>();
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<ILogger<DuckyStore>>(logger)
            .AddSingleton<InitTokens>()
            .AddDucky(d =>
            {
                d.InitTimeout = TimeSpan.FromSeconds(3);
                d.AddSlice<TrailSlice>();
                d.Use<Hanging>();
            });
        await using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IStore>();

        store.Dispatch(new Mark("a"));
        var b = store.DispatchAsync(new Mark("b"));
        var token = provider.GetRequiredService<InitTokens>().Tokens.ShouldHaveSingleItem();
        time.Advance(TimeSpan.FromSeconds(3) - TimeSpan.FromTicks(1));

        store.State.Get<Trail>().Steps.ShouldBeEmpty();
        token.IsCancellationRequested.ShouldBeFalse();
        logger.Collector.Count.ShouldBe(0);

        time.Advance(TimeSpan.FromTicks(1));

        token.IsCancellationRequested.ShouldBeTrue();
        (await b).ShouldBe(DispatchResult.Reduced);
        store.State.Get<Trail>().Steps.ShouldBe(["init", "a", "b"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1031);
        record.Level.ShouldBe(LogLevel.Error);
        record.Message.ShouldBe("Store init aborted (InitTimeout): the store is ready without waiting for the remaining middleware init");

        // Once aborted, the timer is spent: nothing more is logged, and StoreInitialized stays reduced once.
        time.Advance(TimeSpan.FromSeconds(10));
        logger.Collector.Count.ShouldBe(1);
        store.State.Get<Trail>().Steps.ShouldBe(["init", "a", "b"]);

        // The init never ends, so disposal stops waiting for it at DisposeTimeout (§6.11 step 4).
        var disposal = ((IAsyncDisposable)store).DisposeAsync();
        time.Advance(TimeSpan.FromSeconds(2));
        await disposal;
    }

    [Fact]
    public async Task Init_Completed_TimerNeverAborts()
    {
        // Complete wins the CAS and disposes the timer: InitTimeout elapsing later cancels nothing and logs nothing.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(5), timeProvider: time,
            middleware: () => [gate]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(4));

        gate.Release();
        await initialized;
        time.Advance(TimeSpan.FromSeconds(10));

        gate.Token.IsCancellationRequested.ShouldBeFalse();
        logger.Collector.Count.ShouldBe(0);
        Steps(store).ShouldBe(["init"]);
    }

    [Fact]
    public async Task Init_AbortCallbackThrows_StoreStillReady()
    {
        // Abort cancels the init token outside any lock: every callback runs although one throws, the AggregateException is
        // logged (Error 1013), then StoreInitAborted, and the store still becomes Ready.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var ran = false;
        var late = new TaskCompletionSource();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(1), timeProvider: time,
            middleware: () =>
            [
                new InitProbe((_, token) =>
                {
                    token.Register(() => ran = true);
                    token.Register(() => throw new InvalidOperationException("callback"));
                    return new(late.Task);
                }),
                new InitGate(),
            ]);
        var a = store.DispatchAsync(new Mark("a"));

        time.Advance(TimeSpan.FromSeconds(1));

        ran.ShouldBeTrue();
        (await a).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "a"]);
        var records = logger.Collector.GetSnapshot();
        records.Select(r => (r.Id.Id, r.Level)).ShouldBe([(1013, LogLevel.Error), (1031, LogLevel.Error)]);
        records[0].Exception.ShouldBeOfType<AggregateException>().InnerException.ShouldBeOfType<InvalidOperationException>();

        // The init that faults after the abort is observed and logged (§6.7) although the other init never ends, and Complete
        // loses its CAS: Ready is reached once.
        var failure = new InvalidOperationException("late");
        late.SetException(failure);
        Steps(store).ShouldBe(["init", "a"]);
        records = logger.Collector.GetSnapshot();
        records.Count.ShouldBe(3);
        (records[2].Id.Id, records[2].Level).ShouldBe((1032, LogLevel.Error));
        records[2].Exception.ShouldBeSameAs(failure);
    }

    [Fact]
    public void Init_AsyncFaultWhileAnotherInitHangs_LoggedBeforeTimeout()
    {
        // Each init is observed on its own (§6.7 step 4): one that faults asynchronously is logged at once, although another
        // init never ends, not only once InitTimeout aborts.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var faulting = new TaskCompletionSource();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(5), timeProvider: time,
            middleware: () => [new InitGate(), new InitProbe((_, _) => new(faulting.Task))]);
        store.Dispatch(new Mark("a"));
        time.Advance(TimeSpan.FromSeconds(1));

        var failure = new InvalidOperationException("async");
        faulting.SetException(failure);

        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level).ShouldBe((1032, LogLevel.Error));
        record.Exception.ShouldBeSameAs(failure);
        Steps(store).ShouldBeEmpty();

        time.Advance(TimeSpan.FromSeconds(4));

        logger.Collector.GetSnapshot().Select(r => r.Id.Id).ShouldBe([1032, 1031]);
        Steps(store).ShouldBe(["init", "a"]);
    }

    [Fact]
    public async Task Init_MiddlewareAwaitsDispatchAsync_TimesOutNotHangs()
    {
        // The user action is buffered until Ready, so the init waits for InitTimeout; the abort then makes the store Ready,
        // the action is reduced after StoreInitialized, and the init ends. DispatchSystem is the way (§5.6).
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var done = new TaskCompletionSource<DispatchResult>();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(2), timeProvider: time,
            middleware: () => [new InitProbe(async (probe, _) => done.SetResult(await probe.AttachedStore.DispatchAsync(new Mark("from-init"))))]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        initialized.IsCompleted.ShouldBeFalse();

        time.Advance(TimeSpan.FromTicks(1));

        // The abort drained inline on the advancing thread; the init resumes on the pool.
        store.Dispatcher.State.Get<Trail>().Steps.ShouldBe(["init", "from-init"]);
        await initialized;
        (await done.Task.WaitAsync(TestContext.Current.CancellationToken)).ShouldBe(DispatchResult.Reduced);
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);
    }

    [Fact]
    public async Task Init_MiddlewareAwaitsWhenIdle_TimesOutNotHangs()
    {
        // Idle requires Ready, so an init awaiting WhenIdleAsync waits for InitTimeout, then ends once the store is idle.
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var done = new TaskCompletionSource();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(2), timeProvider: time,
            middleware: () =>
            [
                new InitProbe(async (probe, _) =>
                {
                    await probe.AttachedStore.WhenIdleAsync(CancellationToken.None);
                    done.SetResult();
                }),
            ]);
        store.Dispatch(new Mark("a"));
        time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        done.Task.IsCompleted.ShouldBeFalse();

        time.Advance(TimeSpan.FromTicks(1));

        // The abort drained inline on the advancing thread; the idle waiter resumes the init on the pool.
        store.Dispatcher.State.Get<Trail>().Steps.ShouldBe(["init", "a"]);
        await done.Task.WaitAsync(TestContext.Current.CancellationToken);
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);
    }

    // Non-normative: an init-token callback that disposes the store runs inside Abort, before MarkReady, which then does
    // nothing: StoreInitialized is never reduced on a disposed store and the buffered action completes Disposed.
    [Fact]
    public async Task Init_AbortCallbackDisposesStore_MarkReadyDoesNothing()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        Task? disposal = null;
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(1),
            disposeTimeout: TimeSpan.FromSeconds(1), timeProvider: time, middleware: () =>
            [
                new InitProbe((probe, token) =>
                {
                    token.Register(() => disposal = probe.AttachedStore.DisposeAsync().AsTask());
                    return new(new TaskCompletionSource().Task);
                }),
            ]);
        var a = store.DispatchAsync(new Mark("a"));

        time.Advance(TimeSpan.FromSeconds(1));

        a.IsCompleted.ShouldBeTrue();
        (await a).ShouldBe(DispatchResult.Disposed);

        // The init ignores its token, so the disposal stops waiting for it at DisposeTimeout (§6.11 step 4).
        disposal.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        time.Advance(TimeSpan.FromSeconds(1));
        await disposal;
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);
        store.Dispatcher.State.Get<Trail>().Steps.ShouldBeEmpty();
    }

    // Non-normative: an init that honours its token ends cancelled, not faulted, so it is never logged as a failure (§6.7
    // step 4 logs a fault at Error): an InitTimeout abort logs StoreInitAborted alone, and a dispose during init nothing.
    [Fact]
    public async Task Init_MiddlewareHonoursInitToken_CancellationNotLoggedAsFailure()
    {
        var time = new FakeTimeProvider();
        var aborted = new FakeLogger();
        var store = new DuckyStore([new TrailSlice()], aborted, initTimeout: TimeSpan.FromSeconds(1), timeProvider: time,
            middleware: () => [new InitProbe((_, token) => new(new TaskCompletionSource().Task.WaitAsync(token)))]);
        var a = store.DispatchAsync(new Mark("a"));

        time.Advance(TimeSpan.FromSeconds(1));

        (await a).ShouldBe(DispatchResult.Reduced);
        aborted.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);

        // The init ends inside the abort's Cancel, so Complete runs first and loses the CAS: StoreInitialized reduced once.
        store.Dispatcher.State.Get<Trail>().Steps.ShouldBe(["init", "a"]);

        var disposed = new FakeLogger();
        var other = new DuckyStore([new TrailSlice()], disposed, timeProvider: time,
            middleware: () => [new InitProbe((_, token) => new(new TaskCompletionSource().Task.WaitAsync(token)))]);
        var b = other.DispatchAsync(new Mark("b"));

        await other.DisposeAsync();

        (await b).ShouldBe(DispatchResult.Disposed);
        disposed.Collector.Count.ShouldBe(0);
    }

    // Non-normative: with every init already complete there is nothing to bound, so the timer is never armed: a zero
    // InitTimeout cannot abort, and the triggering Dispatch still reduces inline.
    [Fact]
    public void Init_ZeroTimeout_AllInitsSynchronous_NoAbort()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.Zero, timeProvider: time,
            middleware: () => [new InitProbe((_, _) => default)]);

        store.Dispatch(new Mark("a"));

        store.Dispatcher.State.Get<Trail>().Steps.ShouldBe(["init", "a"]);
        logger.Collector.Count.ShouldBe(0);
    }

    // Non-normative: ITimer.Change rejects TimeSpan.MaxValue ("forever") and negative values other than -1 ms, so InitTimeout
    // is clamped before arming (MaxValue to the longest bound, uint.MaxValue - 1 ms, about 49.7 days); unclamped, Change would
    // throw inside the discarded init task and the store never be Ready.
    [Fact]
    public async Task Init_MaxValueTimeout_ClampsToLongestBound()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.MaxValue, timeProvider: time,
            middleware: () => [gate]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        time.Advance(TimeSpan.FromDays(49));

        gate.Release();

        Steps(store).ShouldBe(["init"]);
        await initialized;
        gate.Token.IsCancellationRequested.ShouldBeFalse();
        logger.Collector.Count.ShouldBe(0);
    }

    [Fact]
    public async Task Init_NegativeTimeout_ClampsToZeroAndAbortsAtOnce()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], logger, initTimeout: TimeSpan.FromSeconds(-1), timeProvider: time,
            middleware: () => [gate]);

        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);

        Steps(store).ShouldBe(["init"]);
        await initialized;
        gate.Token.IsCancellationRequested.ShouldBeTrue();
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);
    }

    // Non-normative: a middleware constructor that throws never strands the init buffer: init still starts, and every
    // action completes Failed in the drain (where the cached exception is rethrown), none stays buffered.
    [Fact]
    public async Task PreInitDispatches_AreBufferedAndReplayedInOrder()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);

        // Local and Effect origins are both buffered until Ready, in their enqueue order.
        store.Dispatch(new Mark("a"));
        var b = store.DispatchAsync(new Mark("b"));
        store.Dispatcher.Enqueue(store.Dispatcher.NewPending(new Mark("effect"), Origin.Effect, null));
        var d = store.DispatchAsync(new Mark("d"));
        Steps(store).ShouldBeEmpty();
        b.IsCompleted.ShouldBeFalse();

        gate.Release();

        (await b).ShouldBe(DispatchResult.Reduced);
        (await d).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "a", "b", "effect", "d"]);
    }

    [Fact]
    public async Task Restore_BypassesInitBuffer()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail, new OtherSlice()], NullLogger.Instance, middleware: () => [gate]);
        store.Dispatch(new Mark("a"));

        // Before Ready, a restore is reduced at once, ahead of StoreInitialized and the buffered user action.
        store.Restore(new Dictionary<string, object> { [trail.Key] = new Trail(["restored"]) }, Origin.Hydration);
        Steps(store).ShouldBe(["restored"]);
        store.State.WasRestored<Trail>().ShouldBeTrue();
        store.State.WasRestored<Other>().ShouldBeFalse();

        gate.Release();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        Steps(store).ShouldBe(["restored", "init", "a"]);

        // Restore never starts init: a store that only restored is still before Ready.
        gate = new InitGate();
        store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance, middleware: () => [gate]);
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(7) }, Origin.CrossTab);
        gate.Started.ShouldBe(0);
        store.State.Get<Other>().Value.ShouldBe(7);
        gate.Started.ShouldBe(1);
    }

    [Fact]
    public async Task InitBuffer_ActionsAfterReady_FollowBufferedActions()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail], NullLogger.Instance, middleware: () => [gate]);
        Task<DispatchResult>? fromInit = null;
        Cause? initScope = null;

        // Dispatched while StoreInitialized is reduced, so after Ready: it follows the buffered actions.
        trail.OnInit = () =>
        {
            initScope = store.Dispatcher.Causal;
            fromInit = store.DispatchAsync(new Mark("after-ready"));
        };
        store.Dispatch(new Mark("a"));
        store.Dispatch(new Mark("b"));

        gate.Release();
        (await store.DispatchAsync(new Mark("later"))).ShouldBe(DispatchResult.Reduced);

        (await fromInit.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        Steps(store).ShouldBe(["init", "a", "b", "after-ready", "later"]);

        // StoreInitialized took the next Id at MarkReady, after the buffered a (1) and b (2), on a chain of its own.
        initScope.ShouldBe(new Cause(3, 0, 3, false));
    }

    // Non-normative: MarkReady while another drain is active only queues; the shared init task completes when
    // StoreInitialized is processed, never from MarkReady (§6.7, the deterministic shape of
    // InitializeAsync_WithConcurrentDrainer_CompletesAfterStoreInitializedReduced).
    [Fact]
    public async Task InitializeAsync_MarkReadyDuringDrain_CompletesAfterStoreInitializedReduced()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance, middleware: () => [gate]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        var completedAtMarkReady = true;
        store.Dispatcher.BeforeProcessHook = _ =>
        {
            store.Dispatcher.BeforeProcessHook = null;
            gate.Release();
            completedAtMarkReady = initialized.IsCompleted;
        };

        // The restore's drain is active when the gate releases, so MarkReady only queues StoreInitialized behind it.
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(1) }, Origin.DevTools);

        completedAtMarkReady.ShouldBeFalse();
        await initialized;
        Steps(store).ShouldBe(["init"]);
        store.State.Get<Other>().Value.ShouldBe(1);
    }

    // Non-normative: the caller's token cancels only that caller's wait, never init (§5.2).
    [Fact]
    public async Task InitializeAsync_CallerTokenCancels_OnlyThatWait()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, middleware: () => [gate]);
        using var cts = new CancellationTokenSource();
        var cancelled = store.InitializeAsync(cts.Token);
        var other = store.InitializeAsync(TestContext.Current.CancellationToken);

        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(cancelled);
        other.IsCompleted.ShouldBeFalse();
        gate.Release();
        await other;
        Steps(store).ShouldBe(["init"]);
    }

    // Non-normative: WasRestored is set only by Hydration; CrossTab and DevTools restore the value without the flag.
    [Fact]
    public void Restore_OnlyHydrationSetsWasRestored()
    {
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance);

        store.Restore(new Dictionary<string, object> { ["other"] = new Other(1) }, Origin.CrossTab);
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(2) }, Origin.DevTools);

        store.State.Get<Other>().Value.ShouldBe(2);
        store.State.WasRestored<Other>().ShouldBeFalse();
        store.Restore(new Dictionary<string, object> { ["other"] = new Other(3) }, Origin.Hydration);
        store.State.WasRestored<Other>().ShouldBeTrue();

        // The first State read reduced StoreInitialized into the trail; no restore carried that over.
        store.State.WasRestored<Trail>().ShouldBeFalse();
    }

    // Non-normative: Local, Effect and System are not restore origins (programmer error, §5.2); nothing is enqueued.
    [Fact]
    public void Restore_NonRestoreOrigin_Throws()
    {
        var store = new DuckyStore([new TrailSlice(), new OtherSlice()], NullLogger.Instance);
        var values = new Dictionary<string, object> { ["other"] = new Other(1) };

        foreach (var origin in new[] { Origin.Local, Origin.Effect, Origin.System, (Origin)42 })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => store.Restore(values, origin)).ParamName.ShouldBe("origin");
        }

        Should.Throw<ArgumentNullException>(() => store.Restore(null!, Origin.Hydration)).ParamName.ShouldBe("values");
        store.State.Get<Other>().Value.ShouldBe(0);
    }

    // Non-normative: an unknown key or a value that is not the slice's state type restores nothing for that entry; the
    // rest of the batch still restores. The values are copied at the call, so a later change to the dictionary is ignored.
    [Fact]
    public async Task Restore_UnknownKeyOrWrongType_SkippedOthersRestored()
    {
        var gate = new InitGate();
        var trail = new TrailSlice();
        var store = new DuckyStore([trail, new OtherSlice()], NullLogger.Instance, middleware: () => [gate]);
        _ = store.InitializeAsync(TestContext.Current.CancellationToken);
        var values = new Dictionary<string, object>
        {
            ["missing"] = new Trail(["ghost"]),
            [trail.Key] = new Other(9),
            ["other"] = new Other(5),
        };
        store.Dispatcher.BeforeProcessHook = _ => values["other"] = new Other(6);

        store.Restore(values, Origin.Hydration);

        store.Dispatcher.BeforeProcessHook = null;
        store.State.Get<Other>().Value.ShouldBe(5);
        store.State.WasRestored<Other>().ShouldBeTrue();
        store.State.WasRestored<Trail>().ShouldBeFalse();
        Steps(store).ShouldBeEmpty();
        gate.Release();
        await store.InitializeAsync(TestContext.Current.CancellationToken);
    }
}

using System.Diagnostics.Metrics;
using Ducky.Blazor.Tests.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 "Failed writes are retried" and "Flush on DisposeAsync", §6.11 5a, §6.3 (SafeTelemetry), §8.1; INV-03, INV-16,
// INV-29: a failed write keeps its key dirty (baseline untouched) and is retried with a 1 s to 30 s backoff on the
// TimeProvider; the dispose flush skips debounce and backoff, makes one attempt per dirty key bounded by DisposeTimeout,
// and counts every key still dirty as ducky.persistence.lost.
public sealed class WriterRetryTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static readonly TimeSpan _serverDebounce = TimeSpan.FromMilliseconds(250);

    [Fact]
    public async Task ServerBrowserStorage_TransientDisconnect_WriteRetriedAfterReconnect()
    {
        // A server store writing browser storage: the circuit drops (a disconnect, then an interop timeout), so the key
        // stays dirty with its baseline untouched, Debug only, and is retried on the TimeProvider after 1 s, then 2 s (a
        // change meanwhile doesn't end the sleep); once the circuit is back, the retry writes the state current then.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(), browser: false);
        Exception[] drops = [new JSDisconnectedException("circuit gone"), new TaskCanceledException("interop timeout")];
        var calls = 0;
        List<DateTimeOffset> at = []; // the fake instant of each storageSet
        h.OnSet = _ =>
        {
            at.Add(h.Time.GetUtcNow());
            return ++calls <= drops.Length ? Task.FromException<object?>(drops[calls - 1]) : true;
        };
        await h.InitializeAsync();

        var start = h.Time.GetUtcNow();
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(_serverDebounce);
        h.Time.Advance(_serverDebounce);
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Middleware.LastKnownPayload.ShouldBeEmpty();

        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2));
        h.Store.Dispatch(new SetLevel(2));

        // A sleep the signal wrongly ended would write on a pool thread: a short real-time window lets it, before the clock
        // moves, so it shows as an early instant in `at` below. Correct code writes nothing here, whatever the timing.
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, Xunit.TestContext.Current.CancellationToken);
        h.Time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        h.Written.Count.ShouldBe(2);
        h.Middleware.LastKnownPayload.ShouldBeEmpty();

        h.Time.Advance(TimeSpan.FromTicks(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        at.ShouldBe([start + _serverDebounce, start + _serverDebounce + TimeSpan.FromSeconds(1), start + _serverDebounce + TimeSpan.FromSeconds(3)]);
        await h.IterationsAsync("level", 1);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":2}""");

        // The change raised during the backoff runs once more and finds storage current.
        await h.Time.TimerAsync(_serverDebounce);
        h.Time.Advance(_serverDebounce);
        await h.IterationsAsync("level", 2);
        h.Written.Count.ShouldBe(3);
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ServerBrowserStorage_ChangeInsideDebounceThenCircuitClose_IsLostAndCounted()
    {
        // The documented Server + Local/Session loss: a change still inside its 250 ms debounce when the circuit closes.
        // The flush skips the debounce and tries once, at once (no time passes), the dead circuit refuses it, and the key is
        // counted lost; a key already written is clean and not counted. Disconnects stay Debug only.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>().Persist<DialSlice>(), browser: false);
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new SetDial(1));
        await h.Time.TimerAsync(_serverDebounce);
        h.Time.Advance(_serverDebounce);
        (await h.NextWriteAsync()).Key.ShouldBe("ducky:dial");
        await h.IterationsAsync("dial", 1);

        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(_serverDebounce);
        h.OnSet = static _ => throw new JSDisconnectedException("circuit gone");
        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);

        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        h.Written.Count.ShouldBe(2);
        lost.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();
    }

    [Fact]
    public async Task ServerBrowserStorage_ImportFailsOnDisconnect_ReimportedAfterReconnect()
    {
        // The ducky.js import, pending at the probe, fails on a disconnect during circuit init, so the read fails. The
        // failed import is never cached: the first write imports afresh (still disconnected: not delivered, retried) and
        // the retry after the reconnect imports once more and writes.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(), browser: false);
        var probe = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var respond = h.Js.Respond;
        var imports = 0;
        h.Js.Respond = (identifier, args) => identifier != "import" ? respond(identifier, args) : ++imports switch
        {
            1 => probe.Task,
            2 => Task.FromException<object?>(new JSDisconnectedException("circuit gone")),
            _ => h.Js,
        };
        var init = h.InitializeAsync();
        probe.SetException(new JSDisconnectedException("circuit gone"));
        await init;
        h.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);

        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(_serverDebounce);
        h.Time.Advance(_serverDebounce);
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        imports.ShouldBe(2);
        h.Written.ShouldBeEmpty();

        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        imports.ShouldBe(3);
        h.Js.Calls.Select(static call => call.Identifier).ShouldBe(["import", "import", "import", "storageSet"]);
        await h.IterationsAsync("level", 1);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":1}""");
    }

    [Fact]
    public async Task Dispose_HungEffectRun_PersistenceStillFlushed()
    {
        // A run that ignores its token and never ends holds only 5b: phase 5a never waits for runs, so the flush ends the
        // one-minute debounce and writes at once, while DisposeAsync still waits (bounded) for the run.
        await using var h = new WriterHarness(static d => d
            .Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromMinutes(1))
            .AddEffect(new HungEffect()));
        await h.InitializeAsync();
        h.Store.Dispatch(new StartHung());
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromMinutes(1));

        var disposal = h.DisposeStoreAsync();
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        disposal.IsCompleted.ShouldBeFalse();

        h.Time.Advance(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_HungMiddlewareInit_PersistenceStillFlushed()
    {
        // A middleware registered after AddBlazor whose init ignores its token is disposed first: its init wait expires at
        // DisposeTimeout, its own disposal is chained on that init, and persistence (its own init long ended) still flushes
        // the change waiting in its one-minute debounce.
        await using var h = new WriterHarness(static d =>
        {
            d.InitTimeout = TimeSpan.FromSeconds(10);
            d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromMinutes(1)).Use<HangingInit>();
        });
        // InitTimeout is armed in Start's synchronous prefix: once it passes, the store is Ready without that init.
        var init = h.InitializeAsync();
        h.Time.Advance(TimeSpan.FromSeconds(10));
        await init;
        h.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrated);
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromMinutes(1));

        var disposal = h.DisposeStoreAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2));
        h.Written.ShouldBeEmpty();
        h.Time.Advance(TimeSpan.FromSeconds(2));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await disposal.WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Telemetry_ListenerThrows_LaterStepsStillRun()
    {
        // Every ducky.persistence.* measurement throws in the MeterListener: each throw is Warning 2024, a successful write
        // still sets its baseline, and the flush still counts every dirty key and
        // disposes the module.
        var logs = new FakeLogCollector();
        await using var h = new WriterHarness(
            static d => d.Persist<LevelSlice>().Persist<DialSlice>(),
            services: s => s.AddSingleton<ILogger<PersistenceMiddleware>>(new FakeLogger<PersistenceMiddleware>(logs)));
        using var listener = new ThrowingMeterListener(h.Meters);
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();
        await h.IterationsAsync("level", 1);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":1}""");
        listener.Measured.ShouldBe(["ducky.persistence.save.duration", "ducky.persistence.saves"], ignoreOrder: true);

        // The circuit is gone: both keys fail and sleep in their backoff; the flush counts both, the first count's throw
        // notwithstanding.
        h.OnSet = static _ => throw new JSDisconnectedException("circuit gone");
        h.Store.Dispatch(new SetLevel(3));
        h.Store.Dispatch(new SetDial(3));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);

        listener.Measured.Count(static name => name == "ducky.persistence.lost").ShouldBe(2);
        h.Js.Disposed.ShouldBe(1);
        logs.GetSnapshot().Where(static record => record.Id.Id == 2024).Select(static record => (record.Level, record.Message)).ShouldBe(
        [
            (LogLevel.Warning, "A telemetry listener threw in Record; the step goes on"),
            (LogLevel.Warning, "A telemetry listener threw in Add; the step goes on"),
            (LogLevel.Warning, "A telemetry listener threw in Add; the step goes on"),
            (LogLevel.Warning, "A telemetry listener threw in Add; the step goes on"),
        ]);
    }

    [Fact]
    public void Telemetry_ListenerThrowsFatal_PassesThrough()
    {
        // (non-normative) OutOfMemoryException is fatal (§10): like SafeLogger, SafeTelemetry lets it through. Without an
        // IMeterFactory there are no instruments, and every call is a no-op.
        using var factory = new TestMeterFactory();
        using var listener = new ThrowingMeterListener(factory, ThrowingLogger.Fatal());
        var telemetry = new SafeTelemetry(new SafeLogger(NullLogger.Instance), factory);
        Should.Throw<OutOfMemoryException>(() => telemetry.Add(telemetry.Lost));
        Should.Throw<OutOfMemoryException>(() => telemetry.Record(telemetry.SaveDuration, 1));

        var none = new SafeTelemetry(new SafeLogger(NullLogger.Instance), null);
        none.Saves.ShouldBeNull();
        none.Add(none.Lost);
        none.Record(none.SaveDuration, 1);
        listener.Measured.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Write_ProviderKeepsFailing_BackoffDoublesToThirtySeconds()
    {
        // (non-normative) Any provider failure is retried with a backoff of 1 s doubling to a 30 s cap, reported once for
        // the streak; a success ends the streak and the next failure's backoff starts again at 1 s.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        var calls = 0;
        h.OnSet = _ => ++calls is 8 or 10 ? true : throw new JSException("QuotaExceededError");
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        (await h.NextFailureAsync()).ErrorType.ShouldBe(nameof(JSException));
        foreach (var seconds in new[] { 1, 2, 4, 8, 16, 30, 30 })
        {
            await h.Time.TimerAsync(TimeSpan.FromSeconds(seconds));
            h.Time.Advance(TimeSpan.FromSeconds(seconds));
        }

        await h.IterationsAsync("level", 1);
        calls.ShouldBe(8);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":1}""");
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();

        h.Store.Dispatch(new SetLevel(2));
        (await h.NextFailureAsync()).SliceKey.ShouldBe("level");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.IterationsAsync("level", 2);
        calls.ShouldBe(10);
    }

    [Fact]
    public async Task Dispose_KeyInRetryBackoff_FlushWritesAtOnce()
    {
        // A key sleeping in its 1 s backoff: the flush ends the backoff and makes its one attempt at once, without the clock
        // moving, so a provider back in service stores it and nothing is lost.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        var calls = 0;
        h.OnSet = _ => ++calls == 1 ? Task.FromException<object?>(new JSDisconnectedException("gone")) : true;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));

        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);

        h.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":1}""", """{"Value":1}"""]);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_ChangeBehindInFlightWrite_FlushWritesIt()
    {
        // A change signalled while a write is in flight, the flush beginning before that write returns: the signal is
        // still taken, so the change gets its attempt and nothing is lost.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        h.OnSet = _ => ++calls == 1 ? held.Task : true;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();
        h.Store.Dispatch(new SetLevel(2));

        var disposal = h.DisposeStoreAsync();
        await WriterHarness.Until(() => h.Middleware.Writers["level"].Flushing);
        held.SetResult(true);

        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        await disposal.WaitAsync(_bound, TestContext.Current.CancellationToken);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_FlushAttemptFails_KeyDoneDespitePendingChange()
    {
        // (non-normative) The flush's attempt is the key's last: when it fails, the key stays dirty and is counted lost, and
        // the loop ends there even with a change signalled meanwhile, which a further attempt would otherwise take.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        h.OnSet = _ => ++calls == 1 ? held.Task : throw new JSException("QuotaExceededError");
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();
        h.Store.Dispatch(new SetLevel(2));

        var disposal = h.DisposeStoreAsync();
        await WriterHarness.Until(() => h.Middleware.Writers["level"].Flushing);
        held.SetException(new JSException("QuotaExceededError"));

        await disposal.WaitAsync(_bound, TestContext.Current.CancellationToken);
        await WriterHarness.Until(() => lost.GetMeasurementSnapshot().Sum(static measurement => measurement.Value) == 1);
        calls.ShouldBe(2);
    }

    [Fact]
    public async Task Dispose_WriteHeldPastDisposeTimeout_CountedLostAndLoopEnds()
    {
        // (non-normative) A write held by a dead circuit and a change in its debounce: the flush ends the debounce at once
        // (that attempt is held too) and waits for both, bounded by DisposeTimeout; then the loops' own tokens are
        // cancelled, so DisposeAsync never waits on the held calls, and both keys are counted lost.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>().Persist<DialSlice>(static o => o.Debounce = TimeSpan.FromSeconds(1)));
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        h.OnSet = _ => new TaskCompletionSource<object?>().Task;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();
        h.Store.Dispatch(new SetDial(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));

        var disposal = h.DisposeStoreAsync();
        (await h.NextWriteAsync()).Key.ShouldBe("ducky:dial");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2));
        disposal.IsCompleted.ShouldBeFalse();

        // The flush's bound is the one the store waits with, so the store may stop waiting (Warning 1016) just before the
        // flush counts: the count lands right after.
        h.Time.Advance(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(_bound, TestContext.Current.CancellationToken);
        await WriterHarness.Until(() => lost.GetMeasurementSnapshot().Sum(static measurement => measurement.Value) == 2);
        h.Written.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Dispose_KeyDeferredWhileHydrating_HydrationSkipStillAppliesAndCountsLost()
    {
        // (non-normative) The hydration skip still applies at dispose: a key whose read is still in flight is not written,
        // and it is counted lost.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        h.OnGet = _ => new TaskCompletionSource<object?>().Task;
        _ = h.InitializeAsync();
        h.Line.Send!(new SetLevel(7), false);
        await h.IterationsAsync("level", 1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);

        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.ShouldBeEmpty();
        lost.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_KeyDeferredBeforeFirstRead_CountsLost()
    {
        // (non-normative) A change reduced before persistence's synchronous prefix only marks its key deferred, without
        // signalling its writer. The flush signals every deferred key, so a dispose while the read is in flight counts it
        // lost.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(), early: static d => d.Use<EarlyChange>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        h.OnGet = _ => new TaskCompletionSource<object?>().Task;
        _ = h.InitializeAsync();
        await WriterHarness.Until(() => h.Log.Entries.Contains("restore:@ducky/persistence Hydrating:0"));
        h.Middleware.Deferred.Keys.ShouldContain("level");
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();

        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.ShouldBeEmpty();
        lost.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_BeforeHydratingRestoreReduced_DeferredKeyNotWrittenAndCountsLost()
    {
        // (non-normative) A change reduced before persistence's synchronous prefix waits in _deferred, and init starts from
        // inside that change's drain (as in Write_ChangeBeforeFirstRead_ScopeResolvedSynchronously_NotWrittenBeforeRead),
        // so the prefix's Hydrating restore is queued, not reduced. A dispose from the same drain detaches that restore at
        // step 1: the flush must not release the key, whose writer would find Store.State still at the initial Hydrated
        // status and write in-memory state over storage never read. The key stays dirty and is counted lost.
        Task? disposal = null;
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        h.Log.OnEntry = entry =>
        {
            if (entry.StartsWith(typeof(SetLevel).FullName!, StringComparison.Ordinal))
            {
                _ = h.InitializeAsync();
                disposal = h.DisposeStoreAsync();
            }
        };

        // A restore materializes the store's middleware without starting init (§5.2), so SystemSender's line is connected.
        h.Store.Restore(new Dictionary<string, object>(), Origin.DevTools);
        h.Line.Send!(new SetLevel(2), false);

        await disposal.ShouldNotBeNull().WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.ShouldBeEmpty();
        lost.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public Task Dispose_SkipEndsBeforeFlushDeferralAdd_KeyWrittenNotLost() => SkipEndsDuringFlushAsync(DeferPoint.BeforeAdd);

    [Fact]
    public Task Dispose_SkipEndsAfterFlushDeferralAdd_KeyWrittenNotLost() => SkipEndsDuringFlushAsync(DeferPoint.AfterAdd);

    // (non-normative) A scoped key deferred after a timeout terminal (case (b)). The flush releases it; its attempt still
    // meets the skip, and at `at` inside that deferral (the key's second), once the flush has begun, the late scope is
    // recorded. The recheck (or the recording's release) re-signals the key, so the flush's loop takes that signal and
    // writes it: writable, so not counted lost.
    private static async Task SkipEndsDuringFlushAsync(DeferPoint at)
    {
        var scope = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var deferrals = 0; // the hook runs on the writer's loop only, one call at a time
        WriterHarness? harness = null;
        await using var h = new WriterHarness(
            d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(o =>
            {
                o.Scope = (_, _) => new(scope.Task);
                o.AfterDeferHook = (_, point) =>
                {
                    if (point == at && ++deferrals == 2)
                    {
                        // The writer's own thread blocks: the disposing thread begins the flush (the flush's release can wake
                        // this attempt first), and the scope is recorded by the thread that resumes the resolution.
                        var writer = harness!.Middleware.Writers["level"];
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded waits on other threads' progress
                        WriterHarness.Until(() => writer.Flushing).Wait(_bound, TestContext.Current.CancellationToken).ShouldBeTrue();
                        scope.SetResult("bob");
                        WriterHarness.Until(() => harness.Middleware.Scopes.ContainsKey(0)).Wait(_bound, TestContext.Current.CancellationToken).ShouldBeTrue();
#pragma warning restore VSTHRD002
                    }
                };
            }),
            browser: false);
        harness = h;
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        var init = h.InitializeAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5));
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init;
        h.Store.Dispatch(new SetLevel(1));
        await h.IterationsAsync("level", 1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);

        await h.DisposeStoreAsync().WaitAsync(_bound, TestContext.Current.CancellationToken);
        h.Written.ShouldHaveSingleItem().Key.ShouldBe("ducky:bob:level");
        h.Middleware.LastKnownPayload["ducky:bob:level"].ShouldBe("""{"Value":1}""");
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }
}

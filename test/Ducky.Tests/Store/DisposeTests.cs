using Ducky.Tests.DispatcherFixtures;
using Ducky.Tests.InitFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.11 steps 1-3 and the after-disposal paths, §6.7 (the shared init task at disposal); INV-10, INV-13, INV-29.
// Middleware, effect and subscriber phases (steps 4-6) come with their features.
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
        var store = new DuckyStore([new TrailSlice()], logger, inits: [gate.Init]);
        var buffered = store.DispatchAsync(new Mark("a"));

        store.Dispose();

        // Dispose() is `_ = DisposeAsync();`: the disposal already ran, and DisposeAsync hands back its task.
        (await buffered).ShouldBe(DispatchResult.Disposed);
        store.Dispatcher.Lifetime.IsCancellationRequested.ShouldBeTrue();
        var disposal = store.DisposeAsync().AsTask();
        disposal.IsCompletedSuccessfully.ShouldBeTrue();
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Id.Id.ShouldBe(1010);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("prefer DisposeAsync; pending persistence writes may be lost");
    }

    [Fact]
    public async Task InitializeAsync_DisposedBeforeReady_Completes()
    {
        var gate = new InitGate();
        var store = new DuckyStore([new TrailSlice()], NullLogger.Instance, inits: [gate.Init]);
        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);
        var buffered = store.DispatchAsync(new Mark("a"));

        await store.DisposeAsync();

        // Ready was never reached: dispose completes the shared init task itself, and the buffered action Disposed.
        await initialized;
        initialized.IsCompletedSuccessfully.ShouldBeTrue();
        (await buffered).ShouldBe(DispatchResult.Disposed);
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        // An init that ends after disposal makes nothing Ready: StoreInitialized is never reduced.
        gate.Release();
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
        var store = new DuckyStore([new CountSlice()], NullLogger.Instance, inits: [gate.Init]);
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
}

using Ducky.Tests.InitFixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.7 RequestOverflowAbort; INV-05, INV-13. An init-buffer overflow aborts a Running init through the dispatcher's
// QueueWorkItem seam, never on the caller, once, and drops nothing. Every test but the first replaces the seam with a
// capturing queue and runs the captured abort at a chosen point.
public sealed class OverflowTests
{
    private const string AbortedMessage = "Store init aborted (BufferOverflow): the store is ready without waiting for the remaining middleware init";

    private static string[] Steps(DuckyStore store) => [.. store.Dispatcher.State.Get<Trail>().Steps];

    private static (DuckyStore Store, List<Action> Queued) Capturing(FakeLogger logger, int capacity, params Middleware[] middleware)
    {
        var store = new DuckyStore([new TrailSlice()], logger, timeProvider: new FakeTimeProvider(), middleware: () => middleware,
            initBufferCapacity: capacity);
        List<Action> queued = [];
        store.Dispatcher.QueueWorkItem = queued.Add;
        return (store, queued);
    }

    [Fact]
    public async Task Init_BufferOverflow_AbortsInitDropsNothing()
    {
        // InitBufferCapacity from DI, the production QueueWorkItem (the thread pool): the enqueue past the soft bound aborts
        // the hanging init, every buffered action is replayed in order after StoreInitialized, and StoreInitAborted is
        // logged once with its reason.
        var logger = new FakeLogger<DuckyStore>();
        var services = new ServiceCollection()
            .AddSingleton<TimeProvider>(new FakeTimeProvider())
            .AddSingleton<ILogger<DuckyStore>>(logger)
            .AddSingleton<InitTokens>()
            .AddDucky(d =>
            {
                d.InitBufferCapacity = 2;
                d.AddSlice<TrailSlice>();
                d.Use<Hanging>();
            });
        await using var provider = services.BuildServiceProvider();
        var store = (DuckyStore)provider.GetRequiredService<IStore>();

        store.Dispatch(new Mark("a"));
        var b = store.DispatchAsync(new Mark("b"));
        var token = provider.GetRequiredService<InitTokens>().Tokens.ShouldHaveSingleItem();
        b.IsCompleted.ShouldBeFalse();
        token.IsCancellationRequested.ShouldBeFalse();

        var c = store.DispatchAsync(new Mark("c"));

        (await c.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)).ShouldBe(DispatchResult.Reduced);
        (await b).ShouldBe(DispatchResult.Reduced);
        token.IsCancellationRequested.ShouldBeTrue();
        Steps(store).ShouldBe(["init", "a", "b", "c"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Level, record.Message).ShouldBe((1031, LogLevel.Error, AbortedMessage));
    }

    [Fact]
    public async Task Init_OverflowDuringSyncPrefix_AbortsFromRunning()
    {
        // A synchronous part that fills the buffer exactly to the bound queues nothing: the bound is exceeded, not reached.
        var logger = new FakeLogger();
        var (store, queued) = Capturing(logger, 2, new InitProbe((probe, _) =>
        {
            probe.AttachedStore.Dispatch(new Mark("x1"));
            probe.AttachedStore.Dispatch(new Mark("x2"));
            return new(new TaskCompletionSource().Task);
        }));
        _ = store.InitializeAsync(TestContext.Current.CancellationToken);
        queued.ShouldBeEmpty();

        // One that overflows does it while Starting: each RequestOverflowAbort is a no-op there, and Start step 3 queues the
        // abort from Running, after every synchronous part returned.
        var queuedInPrefix = -1;
        CancellationToken token = default;
        (store, queued) = Capturing(logger, 2, new InitProbe((probe, initToken) =>
        {
            token = initToken;
            probe.AttachedStore.Dispatch(new Mark("x1"));
            probe.AttachedStore.Dispatch(new Mark("x2"));
            probe.AttachedStore.Dispatch(new Mark("x3"));
            queuedInPrefix = queued.Count;
            return new(new TaskCompletionSource().Task);
        }));

        var initialized = store.InitializeAsync(TestContext.Current.CancellationToken);

        queuedInPrefix.ShouldBe(0);
        var abort = queued.ShouldHaveSingleItem();
        Steps(store).ShouldBeEmpty();
        token.IsCancellationRequested.ShouldBeFalse();

        abort();

        token.IsCancellationRequested.ShouldBeTrue();
        await initialized;
        Steps(store).ShouldBe(["init", "x1", "x2", "x3"]);
        var record = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        (record.Id.Id, record.Message).ShouldBe((1031, AbortedMessage));
    }

    [Fact]
    public void Init_OverflowAbort_NeverRunsOnCaller()
    {
        // The init-token callback stands for persistence waiting on its _issue lock: it must never run inside the overflowing
        // Dispatch. The callback reads whether that Dispatch has returned; the abort runs only once the captured item does.
        var returned = new TaskCompletionSource();
        bool? ranAfterReturn = null;
        var logger = new FakeLogger();
        var (store, queued) = Capturing(logger, 1, new InitProbe((_, token) =>
        {
            token.Register(() => ranAfterReturn = returned.Task.IsCompleted);
            return new(new TaskCompletionSource().Task);
        }));
        store.Dispatch(new Mark("a"));

        store.Dispatch(new Mark("b"));
        returned.SetResult();

        ranAfterReturn.ShouldBeNull();
        Steps(store).ShouldBeEmpty();
        queued.ShouldHaveSingleItem()();
        ranAfterReturn.ShouldBe(true);
        Steps(store).ShouldBe(["init", "a", "b"]);
    }

    [Fact]
    public void Init_OverflowTwiceBeforeQueuedAbortRuns_QueuedOnce()
    {
        // The first overflow sets the flag and queues one abort; the next fails the flag CAS and queues nothing.
        var logger = new FakeLogger();
        var gate = new InitGate();
        var (store, queued) = Capturing(logger, 1, gate);
        store.Dispatch(new Mark("a"));
        queued.ShouldBeEmpty();

        store.Dispatch(new Mark("b"));
        store.Dispatch(new Mark("c"));

        var abort = queued.ShouldHaveSingleItem();
        abort();
        gate.Token.IsCancellationRequested.ShouldBeTrue();
        Steps(store).ShouldBe(["init", "a", "b", "c"]);
        logger.Collector.GetSnapshot().ShouldHaveSingleItem().Id.Id.ShouldBe(1031);
    }

    [Fact]
    public void Init_QueuedOverflowAbortRunsAfterComplete_NoOp()
    {
        // Init completes before the queued abort runs: Abort's CAS fails, so nothing is cancelled, logged or reduced again.
        var logger = new FakeLogger();
        var gate = new InitGate();
        var (store, queued) = Capturing(logger, 1, gate);
        store.Dispatch(new Mark("a"));
        store.Dispatch(new Mark("b"));
        var abort = queued.ShouldHaveSingleItem();
        gate.Release();
        Steps(store).ShouldBe(["init", "a", "b"]);

        abort();

        gate.Token.IsCancellationRequested.ShouldBeFalse();
        logger.Collector.Count.ShouldBe(0);
        Steps(store).ShouldBe(["init", "a", "b"]);
    }
}

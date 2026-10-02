using System.Runtime.CompilerServices;
using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.6 (Exhaust running marker, Queue Done chaining, the normative finally order), §17.4; INV-11. Plain gates resume
// a run inline on the test thread; a Queue successor starts on a pool thread, because Done uses
// RunContinuationsAsynchronously, so the tests wait for each run's start signal.
public sealed class ExhaustQueueTests
{
    private const int EffectDropped = 1051;

    // Run 1 holds the marker, so Load(2) starts no run: the action is reduced, its effect is dropped (Debug 1051, the
    // ducky.effect.dropped point). Once run 1 ended, Load(3) starts a run again. The run token is the store lifetime.
    [Fact]
    public async Task Exhaust_SecondWhileRunning_DroppedAndCounted()
    {
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        List<int> handled = [];
        List<CancellationToken> tokens = [];
        var effect = new PolicyHandler<Load>(Concurrency.Exhaust, async (load, _, token) =>
        {
            handled.Add(load.Id);
            tokens.Add(token);
            if (load.Id == 1)
            {
                await gate.Task.ConfigureAwait(false);
            }
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        handled.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(1);

        gate.SetResult();
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.Dispatch(new Load(3));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        handled.ShouldBe([1, 3]);
        tokens.ShouldAllBe(t => t == store.Dispatcher.Lifetime);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), new Load(3)]);
        var drop = logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        drop.Id.Id.ShouldBe(EffectDropped);
        drop.Level.ShouldBe(LogLevel.Debug);
        drop.Message.ShouldBe($"{typeof(PolicyHandler<Load>)} dropped {typeof(Load)}: a run for the same key is in flight");
    }

    // Each run waits for its predecessor's Done, so the three never overlap and run in dispatch order; every queued run is
    // counted from its dispatch, so idle never fires between runs. The last gate resumes run 3 on a pool thread while the
    // test awaits idle: the slot is already gone when idle completes (compare-and-remove precedes the idle decrement).
    [Fact]
    public async Task Queue_ThreeRuns_NeverOverlap_SlotEmptyAtIdle()
    {
        var ct = TestContext.Current.CancellationToken;
        var started = Enumerable.Range(0, 4).Select(_ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
        var gates = new[] { new TaskCompletionSource(), new(), new(), new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var active = 0;
        var maxActive = 0;
        var order = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var effect = new PolicyHandler<Load>(Concurrency.Queue, async (load, _, _) =>
        {
            var now = Interlocked.Increment(ref active);
            maxActive = Math.Max(maxActive, now);
            order.Enqueue(load.Id);
            started[load.Id].SetResult();
            await gates[load.Id].Task.ConfigureAwait(false);
            Interlocked.Decrement(ref active);
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        store.Dispatch(new Load(3));
        order.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(1);
        var idle = store.WhenIdleAsync(ct);

        gates[1].SetResult();
        await started[2].Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        idle.IsCompleted.ShouldBeFalse();

        gates[2].SetResult();
        await started[3].Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        idle.IsCompleted.ShouldBeFalse();

        gates[3].SetResult();
        await idle.WaitAsync(TimeSpan.FromSeconds(10), ct);
        store.Dispatcher.SlotCount.ShouldBe(0);
        order.ShouldBe([1, 2, 3]);
        maxActive.ShouldBe(1);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), new Load(3)]);
    }

    // Run 1 disposes the store while run 2 waits for run 1's Done. Disposal cancels the lifetime, so run 2 stops waiting,
    // never invokes its handler and completes, and the disposal never waits for a chain through the disposing run.
    [Fact]
    public async Task Queue_DisposeFromRunWithQueuedSuccessor_CompletesWithoutTimeout()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        var run1Returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? disposal = null;
        List<int> handled = [];
        DuckyStore store = null!;
        var effect = new PolicyHandler<Load>(Concurrency.Queue, async (load, _, _) =>
        {
            handled.Add(load.Id);
            await gate.Task.ConfigureAwait(false);
            disposal = store.DisposeAsync().AsTask();
            await disposal.ConfigureAwait(false);
            run1Returned.SetResult();
        });
        // A DisposeTimeout far above the 10 s bound: a disposal that waited for a chain through the disposing run would fail
        // the bound below instead of being released by its own timeout.
        store = new DuckyStore([new SeenSlice()], logger, disposeTimeout: TimeSpan.FromMinutes(1), effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        handled.ShouldBe([1]);
        gate.SetResult();

        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), ct);
        await run1Returned.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        handled.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    // Disposal cancels runs 2 and 3 while they wait, synchronously in the DisposeAsync call (step 2): neither handler runs,
    // neither OperationCanceledException is a failure, and run 3, whose slot is current, removes it. The disposal awaits
    // run 1 (§6.11 step 4), so it completes only once the gate releases run 1, which then completes inline on the test thread.
    [Fact]
    public async Task Queue_AfterDispose_HandlerNeverInvoked()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        List<int> handled = [];
        var effect = new PolicyHandler<Load>(Concurrency.Queue, async (load, _, _) =>
        {
            handled.Add(load.Id);
            await gate.Task.ConfigureAwait(false);
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        store.Dispatch(new Load(3));
        handled.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(1);

        var disposal = store.DisposeAsync().AsTask();
        handled.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        gate.SetResult();

        await disposal.WaitAsync(TimeSpan.FromSeconds(10), ct);
        handled.ShouldBe([1]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), new Load(3)]);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    // Non-normative: AddOrUpdate keeps the first installer's key, so a run that replaced a slot removes with that stored key
    // object (inherited through its slot): object.Equals short-circuits on the reference and the user's Equals never runs.
    // Armed, it would throw; the queued successors run, idle is reached, no slot remains and nothing is logged (INV-11).
    [Fact]
    public async Task Queue_KeyEqualityThrowsOnRemove_SuccessorRunsAndSlotFreed()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        var armed = new StrongBox<bool>();
        var handled = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var effect = new PolicyHandler<Load>(
            Concurrency.Queue,
            async (load, _, _) =>
            {
                handled.Enqueue(load.Id);
                if (load.Id == 1)
                {
                    await gate.Task.ConfigureAwait(false);
                }
            },
            _ => new ArmableKey(armed));
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        store.Dispatch(new Load(3));
        handled.ShouldBe([1]);
        armed.Value = true;
        gate.SetResult();

        await store.WhenIdleAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
        handled.ShouldBe([1, 2, 3]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    // Non-normative: distinct keys whose hashes collide share a bucket, and the newer node comes first, so run 1's removal
    // compares key B with key A and calls the user's Equals. When it throws there, run 1 still completes its Done and its
    // idle decrement, and the throw is logged (Error 1003): the queued run 3 starts. Disarmed again, run 3 removes the slot.
    [Fact]
    public async Task Queue_KeyEqualityThrowsOnHashCollision_SuccessorStillRuns()
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new FakeLogger();
        var armed = new StrongBox<bool>();
        TaskCompletionSource[] gates = [new(), new(), new()];
        var run3Started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handled = new System.Collections.Concurrent.ConcurrentQueue<int>();
        var effect = new PolicyHandler<Load>(
            Concurrency.Queue,
            async (load, _, _) =>
            {
                handled.Enqueue(load.Id);
                if (load.Id == 3)
                {
                    run3Started.SetResult();
                }

                await gates[load.Id - 1].Task.ConfigureAwait(false);
            },
            load => new CollidingKey(load.Id == 2 ? "B" : "A", armed));
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        store.Dispatch(new Load(3));
        handled.ShouldBe([1, 2]);
        armed.Value = true;
        gates[0].SetResult();

        await run3Started.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        armed.Value = false;
        gates[2].SetResult();
        gates[1].SetResult();

        await store.WhenIdleAsync(ct).WaitAsync(TimeSpan.FromSeconds(10), ct);
        handled.ShouldBe([1, 2, 3]);
        store.Dispatcher.SlotCount.ShouldBe(0);
        logger.Collector.GetSnapshot().Select(r => (r.Id.Id, r.Level, r.Exception?.Message)).ShouldBe(
            [(1003, LogLevel.Error, "key Equals")]);
    }

    // INV-11: an effect started concurrently with disposal gets an already-cancelled token. The Merge effect registered first
    // disposes the store from its synchronous prefix, so the next runners of the same action start with a cancelled
    // lifetime; like Merge, a Switch, an Exhaust and a first Queue run still invoke their handler (only a Queue run
    // cancelled while it waits for its predecessor skips it, §6.6).
    [Theory]
    [InlineData(Concurrency.Switch)]
    [InlineData(Concurrency.Exhaust)]
    [InlineData(Concurrency.Queue)]
    public async Task Effect_StartedAfterDisposalBegan_HandlerInvokedWithCancelledToken(Concurrency policy)
    {
        var ct = TestContext.Current.CancellationToken;
        var logger = new FakeLogger();
        Task? disposal = null;
        DuckyStore store = null!;
        bool? cancelledAtStart = null;
        var disposer = new Handler<Load>((_, _, _) =>
        {
            disposal = store.DisposeAsync().AsTask();
            return Task.CompletedTask;
        });
        var effect = new PolicyHandler<Load>(policy, (_, _, token) =>
        {
            cancelledAtStart = token.IsCancellationRequested;
            return Task.CompletedTask;
        });
        store = new DuckyStore([new SeenSlice()], logger, effects: () => [(disposer, false), (effect, false)]);

        store.Dispatch(new Load(1));

        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), ct);
        cancelledAtStart.ShouldBe(true);
        store.Dispatcher.SlotCount.ShouldBe(0);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    private sealed class ArmableKey(StrongBox<bool> armed)
    {
        public override bool Equals(object? obj) => armed.Value ? throw new InvalidOperationException("key Equals") : obj is ArmableKey;

        public override int GetHashCode() => 1;
    }

    private sealed class CollidingKey(string name, StrongBox<bool> armed)
    {
        private string Name => name;

        public override bool Equals(object? obj) =>
            armed.Value ? throw new InvalidOperationException("key Equals") : obj is CollidingKey other && other.Name == name;

        public override int GetHashCode() => 1;
    }

    private sealed class PolicyHandler<TAction>(
        Concurrency policy,
        Func<TAction, EffectContext, CancellationToken, Task> handle,
        Func<TAction, object?>? key = null)
        : Effect<TAction>
        where TAction : notnull
    {
        public override Concurrency Policy => policy;

        protected override object? ConcurrencyKey(TAction action) => key?.Invoke(action);

        public override Task Handle(TAction action, EffectContext context, CancellationToken cancellationToken) =>
            handle(action, context, cancellationToken);
    }
}

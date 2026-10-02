using Ducky.Tests.EffectFixtures;
using Ducky.Tests.MiddlewareFixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Tests;

// SPEC §6.5 (causal depth through effects), D12, ADR-0026; INV-06. Single-threaded: every drain and every effect
// continuation runs on the test thread, the continuations driven inline by FakeTimeProvider.Advance (§17.3 keeps these
// tests out of Ducky.Concurrency.Tests).
public sealed class CausalDepthTests
{
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(1);

    // Two effects dispatch each other in their synchronous prefix (before their first await): every dispatch runs while
    // its parent is being processed, so it is a synchronous child and the chain is dropped above MaxDispatchDepth (64).
    [Fact]
    public async Task EffectPingPong_SynchronousPrefix_IsStoppedByDepthGuard()
    {
        var time = new FakeTimeProvider();
        var reduced = new List<ActionContext>();
        var recorder = new Recorder("r", []) { OnAfter = reduced.Add };

        // Safety cap: without the guard this test fails instead of hanging.
        var ping = new Handler<Load>(async (load, context, token) =>
        {
            if (load.Id < 100)
            {
                context.Dispatch(new Loaded(load.Id + 1));
            }

            await Task.Delay(_interval, context.Time, token).ConfigureAwait(false);
        });
        var pong = new Handler<Loaded>(async (loaded, context, token) =>
        {
            context.Dispatch(new Load(loaded.Id + 1));
            await Task.Delay(_interval, context.Time, token).ConfigureAwait(false);
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, timeProvider: time, middleware: () => [recorder], effects: () => [(ping, false), (pong, false)]);

        (await store.DispatchAsync(new Load(0))).ShouldBe(DispatchResult.Reduced);

        // Depths 0..64 are reduced on the root's chain, alternating Load and Loaded; Loaded(65) at depth 65 is dropped.
        var root = reduced.First(c => c.Action is Load);
        var chain = reduced.Where(c => c.Action is Load or Loaded).ToList();
        chain.Select(c => c.Depth).ShouldBe(Enumerable.Range(0, 65));
        chain.Select(c => c.Action).ShouldBe(Enumerable.Range(0, 65).Select(i => i % 2 == 0 ? (object)new Load(i) : new Loaded(i)));
        chain.ShouldAllBe(c => c.CorrelationId == root.CorrelationId);
        chain.Skip(1).ShouldAllBe(c => c.Origin == Origin.Effect);
        store.State.Get<Seen>().Actions.ShouldBe(chain.Select(c => c.Action));

        // The drop's ReducerFailed(DispatchLoopException) is reduced, at depth 0 on the same chain.
        var failure = reduced.Single(c => c.Action is ReducerFailed);
        (failure.Origin, failure.Depth, failure.CorrelationId).ShouldBe((Origin.System, 0, root.CorrelationId));
        var failed = (ReducerFailed)failure.Action;
        failed.ActionType.ShouldBe(typeof(Loaded).ToString());
        failed.Exception.ShouldBeOfType<DispatchLoopException>().Depth.ShouldBe(65);
    }

    // Tick -> await Time.Delay -> Dispatch(Tick): each dispatch follows a real asynchronous yield, so it restarts the depth
    // budget on the same chain, and the polling effect outlives MaxDispatchDepth ticks.
    [Fact]
    public async Task PollingEffect_RunsBeyondMaxDepthTicks()
    {
        const int ticks = 200;
        var time = new FakeTimeProvider();
        var reduced = new List<ActionContext>();
        var recorder = new Recorder("r", []) { OnAfter = reduced.Add };
        var poll = new Handler<Load>(async (load, context, token) =>
        {
            await Task.Delay(_interval, context.Time, token).ConfigureAwait(false);
            if (load.Id < ticks)
            {
                context.Dispatch(new Load(load.Id + 1));
            }
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, timeProvider: time, middleware: () => [recorder], effects: () => [(poll, false)]);
        (await store.DispatchAsync(new Load(0))).ShouldBe(DispatchResult.Reduced);

        for (var i = 0; i < ticks; i++)
        {
            time.Advance(_interval);
        }

        store.State.Get<Seen>().Actions.ShouldBe(Enumerable.Range(0, ticks + 1).Select(i => new Load(i)));
        var chain = reduced.Where(c => c.Action is Load).ToList();
        chain.ShouldAllBe(c => c.Depth == 0 && c.CorrelationId == chain[0].CorrelationId);
        chain.Skip(1).ShouldAllBe(c => c.Origin == Origin.Effect);
        reduced.ShouldNotContain(c => c.Action is ReducerFailed);
    }

    // The continuation of an effect triggered at depth 1 is resumed by FakeTimeProvider.Advance on the test thread, after
    // the drain exited: it still carries its parent's scope, but nothing is being processed (_processingSeq is 0), so its
    // dispatch starts a new chain at depth 0 that keeps the old correlation id.
    [Fact]
    public async Task AsyncDispatchWhileStoreIdle_StartsNewChainAtDepthZero()
    {
        var time = new FakeTimeProvider();
        var reduced = new List<ActionContext>();
        var recorder = new Recorder("r", []) { OnAfter = reduced.Add };
        Task<DispatchResult>? continuation = null;
        var load = new Handler<Load>((l, context, _) =>
        {
            context.Dispatch(new Loaded(l.Id));
            return Task.CompletedTask;
        });
        var loaded = new Handler<Loaded>(async (_, context, token) =>
        {
            await Task.Delay(_interval, context.Time, token).ConfigureAwait(false);
            continuation = context.DispatchAsync(new Derived());
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, timeProvider: time, middleware: () => [recorder], effects: () => [(load, false), (loaded, false)]);

        (await store.DispatchAsync(new Load(1))).ShouldBe(DispatchResult.Reduced);
        var root = reduced.Single(c => c.Action is Load);
        var parent = reduced.Single(c => c.Action is Loaded);
        (parent.Origin, parent.Depth, parent.CorrelationId).ShouldBe((Origin.Effect, 1, root.CorrelationId));

        // The store is idle: the drain exited and the test thread carries no scope.
        store.Dispatcher.DrainExited.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        store.Dispatcher.Causal.ShouldBeNull();
        continuation.ShouldBeNull();

        time.Advance(_interval);

        (await continuation.ShouldNotBeNull()).ShouldBe(DispatchResult.Reduced);
        var child = reduced.Single(c => c.Action is Derived);
        (child.Origin, child.Depth, child.CorrelationId).ShouldBe((Origin.Effect, 0, root.CorrelationId));
        child.Id.ShouldBeGreaterThan(parent.Id);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Loaded(1), new Derived()]);
    }
}

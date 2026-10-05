using CsCheck;
using Ducky.Blazor;
using Microsoft.Extensions.Logging.Abstractions;
using RendererDispatcher = Microsoft.AspNetCore.Components.Dispatcher;

namespace Ducky.Concurrency.Tests;

// SPEC §11.2 (no lost wake-up) and §17.3 row 15 against Ducky.Blazor's SubscriptionCore: INV-19.
[Collection(nameof(Interleaving))]
public sealed class SubscriptionCoreInterleavingTests
{
    private const int MaxSequentialOperations = 10;
    private const int MaxParallelOperations = 6;

    // Publishes (awaited commits, each notifying the core on its drainer) race renders on a real renderer dispatcher
    // (parent re-renders reading the selection) and the core's own Evaluate. Whatever the interleaving, the last publish is
    // evaluated: once everything settles, the renderer shows the final value, and at no point were two InvokeAsync calls
    // pending at once. The lost wake-up INV-19 names (a drainer CAS that saw 1 while the pending Evaluate read an older
    // snapshot) needs a commit inside the few nanoseconds between Evaluate's reset and its snapshot read, with no later
    // Render to heal it, so random interleavings almost never reach it: the reset-before-recompute ordering is pinned
    // deterministically by SubscriptionCoreTests.Evaluate_SelectorThrows_AtMostOnePendingInvoke, whose selector commits
    // during Evaluate and must find the flag already reset.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task SubscriptionCore_RandomPublishInterleavings_LastPublishAlwaysEvaluated(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var publish = Gen.Int.Operation<Actual, Model>(_ => "DispatchAsync(Tick)", (a, _) => a.Publish(), (m, _) => m.Ticks++);
        var render = Gen.Int.Operation<Actual, Model>(_ => "Render", (a, _) => a.Render(), (_, _) => { });

        // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
        await Interleaving.WithinProperty(Task.Run(
            () => Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                publish,
                render,
                equal: (a, m) => a.Ticks == m.Ticks && a.SettledRender == m.Ticks && a.MaxPending <= 1,
                maxSequentialOperations: MaxSequentialOperations,
                maxParallelOperations: MaxParallelOperations),
            cancellationToken));
    }

    private sealed class Actual
    {
        private readonly RendererDispatcher _renderer = RendererDispatcher.CreateDefault();
        private readonly Selection<long> _selection;
        private long _rendered;
        private int _pending;
        private int _maxPending;
        private long? _settledRender;

        public Actual()
        {
            // Never disposed: the sample's store and renderer die with it.
            var core = new SubscriptionCore(Store, InvokeAsync, Draw, NullLogger.Instance);
            _selection = core.Select(static state => state.Get<Left>().Ticks);
            Render();
        }

        public DuckyStore Store { get; } = Interleaving.Store(new LeftSlice());

        public long Ticks => Store.State.Get<Left>().Ticks;

        public int MaxPending => Volatile.Read(ref _maxPending);

        // Once, after the run (equal may be called once per linearization tried). Every publish has returned, so the
        // drain's exit follows every OnCommit and the InvokeAsync it made. The first renderer round runs every queued
        // delegate, whose Task.Yield posts Evaluate behind it; the second runs those Evaluates and their renders.
        public long SettledRender => _settledRender ??= Settle();

        public void Publish() => Interleaving.Wait(Store.DispatchAsync(new Tick())).ShouldBe(DispatchResult.Reduced);

        // A parent re-render: the renderer reads the selection, which moves the core's baseline.
        public void Render() => Interleaving.Wait(_renderer.InvokeAsync(Draw));

        public override string ToString() => $"ticks {Ticks}, rendered {Volatile.Read(ref _rendered)}, max pending {MaxPending}";

        private void Draw() => Volatile.Write(ref _rendered, _selection.Value);

        // Counts InvokeAsync calls whose delegate has not started: the flag stays 1 until Evaluate, after that start.
        private Task InvokeAsync(Func<Task> work)
        {
            var pending = Interlocked.Increment(ref _pending);
            int max;
            while (pending > (max = Volatile.Read(ref _maxPending)) && Interlocked.CompareExchange(ref _maxPending, pending, max) != max)
            {
            }

            return _renderer.InvokeAsync(() =>
            {
                Interlocked.Decrement(ref _pending);
                return work();
            });
        }

        private long Settle()
        {
            Interleaving.Wait(Interleaving.Settled(Store));
            Interleaving.Wait(_renderer.InvokeAsync(() => { }));
            Interleaving.Wait(_renderer.InvokeAsync(() => { }));
            return Volatile.Read(ref _rendered);
        }
    }

    private sealed class Model
    {
        public long Ticks { get; set; }

        public override string ToString() => $"ticks {Ticks}";
    }
}

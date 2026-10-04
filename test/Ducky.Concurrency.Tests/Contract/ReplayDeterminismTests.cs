using CsCheck;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Concurrency.Tests;

// SPEC §17.3 row 9; INV-01, INV-04, INV-07. 2 to 4 producers on dedicated threads dispatch at once into one store; a
// reducer that dispatches queues its child, and some reducers throw. The linearization a middleware recorded on the
// drainer, replayed on a fresh store on one thread, yields an equal final snapshot, and keeps each producer's order.
[Collection(nameof(Interleaving))]
public sealed class ReplayDeterminismTests
{
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task ReplayDeterminism_SameActionsSameSnapshot(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        await Interleaving.WithinProperty(Task.Run(
            () => Gen.Int[1, 40].Array[2, 4].Sample(counts => FireAndReplay(counts, cancellationToken), threads: 1),
            cancellationToken));
    }

    private static void FireAndReplay(int[] counts, CancellationToken cancellationToken)
    {
        var recorder = new ProcessingOrder();
        var slice = new ReplaySlice();
        var store = new DuckyStore([slice], NullLogger.Instance, middleware: () => [recorder]);
        slice.OnOp = op =>
        {
            if (op.Value > 0 && op.Value % 3 == 0)
            {
                store.Dispatch(op with { Value = -op.Value });
            }
        };

        // Initialized first, so the last drain's exit is the moment the queue is empty once every producer returned
        // (DrainExited is null while an async init is pending).
        Interleaving.Wait(store.InitializeAsync(cancellationToken));
        using var start = new Barrier(counts.Length);
        var producers = counts.Select((count, p) => Task.Factory.StartNew(
            () =>
            {
                start.SignalAndWait(cancellationToken);
                for (var i = 1; i <= count; i++)
                {
                    store.Dispatch(new Op(p, i));
                }
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)).ToArray();
        Interleaving.Wait(Task.WhenAll(producers));
        Interleaving.Wait(Interleaving.Settled(store));

        var replay = new DuckyStore([new ReplaySlice()], NullLogger.Instance);
        foreach (var action in recorder.Actions)
        {
            replay.Dispatch(action);
        }

        var roots = recorder.Actions.Cast<Op>().Where(op => op.Value > 0).ToList();
        for (var p = 0; p < counts.Length; p++)
        {
            roots.Where(op => op.Producer == p).Select(op => op.Value).ShouldBe(Enumerable.Range(1, counts[p]), $"producer {p}");
        }

        recorder.Actions.Count.ShouldBe(roots.Count + roots.Count(op => op.Value % 3 == 0));
        replay.State.Version.ShouldBe(store.State.Version);
        replay.State.Get<Trace>().ShouldBe(store.State.Get<Trace>());
    }

    private sealed record Op(int Producer, int Value);

    // Order-sensitive: the trace lists the ops in processing order, and the fold is not commutative.
    private sealed record Trace(string Ops, long Fold);

    // OnOp runs inside each reducer call; a value divisible by 7 throws (no commit).
    private sealed class ReplaySlice : Slice<Trace>
    {
        public ReplaySlice() => On<Op>((state, op) =>
        {
            OnOp?.Invoke(op);
            return op.Value % 7 == 0
                ? throw new InvalidOperationException("reducer")
                : new($"{state.Ops}{op.Producer}:{op.Value};", (state.Fold * 31) + op.Value);
        });

        public Action<Op>? OnOp { get; set; }

        protected override Trace Initial => new(string.Empty, 0);
    }

    // AfterReduce runs on the drainer, one action at a time (INV-01), so a plain list records the linearization.
    private sealed class ProcessingOrder : Middleware
    {
        public List<object> Actions { get; } = [];

        public override void AfterReduce(ActionContext context)
        {
            if (context.Origin == Origin.Local)
            {
                Actions.Add(context.Action);
            }
        }
    }
}

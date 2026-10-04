using CsCheck;
using Ducky.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ducky.Tests.Contract;

// SPEC §17.5, the single-threaded twin of §17.3 row 9; INV-01, INV-04, INV-07. Producers' dispatches interleave in a
// random order on one thread; a reducer that dispatches queues its child behind the queue (never nested), and some
// reducers throw (no commit). The processing order a middleware recorded, replayed on a fresh store, yields an equal
// final snapshot: the store's result depends on the linearization alone.
public sealed class ReplayDeterminismTests
{
    [Fact]
    public void ReplayDeterminism_SameActionsSameSnapshot_Deterministic()
    {
        // (producer, value): a value divisible by 3 dispatches a child from its reducer, one divisible by 7 throws.
        var runs = Gen.Select(Gen.Int[0, 3], Gen.Int[1, 50]).List[1, 40];
        Property.Check(runs, run =>
        {
            var recorder = new ProcessingOrder();
            var slice = new ReplaySlice();
            using var store = new DuckyStore([slice], NullLogger.Instance, middleware: () => [recorder]);
            slice.OnOp = op =>
            {
                if (op.Value % 3 == 0)
                {
                    store.Dispatch(op with { Value = op.Value + 1000 });
                }
            };
            foreach (var (producer, value) in run)
            {
                store.Dispatch(new Op(producer, value));
            }

            using var replay = new DuckyStore([new ReplaySlice()], NullLogger.Instance);
            foreach (var action in recorder.Actions)
            {
                replay.Dispatch(action);
            }

            recorder.Actions.Count.ShouldBe(run.Count + run.Count(s => s.Item2 % 3 == 0));
            Snapshots.ShouldBeEqual(replay.State, store.State);
        });
    }

    internal sealed record Op(int Producer, int Value);

    // Order-sensitive: the trace lists the ops in processing order, and the sum is a non-commutative fold.
    internal sealed record Trace(string Ops, long Fold);

    internal sealed class ReplaySlice : Slice<Trace>
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

    // The user actions in processing order (AfterReduce sees failed actions too, §6.4).
    internal sealed class ProcessingOrder : Middleware
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

    internal static class Snapshots
    {
        public static void ShouldBeEqual(StateSnapshot actual, StateSnapshot expected)
        {
            actual.Version.ShouldBe(expected.Version);
            actual.Keys.ShouldBe(expected.Keys);
            foreach (var key in expected.Keys)
            {
                actual.Get(key).ShouldBe(expected.Get(key));
            }
        }
    }
}

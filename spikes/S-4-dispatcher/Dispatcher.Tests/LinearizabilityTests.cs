using System.Collections.Concurrent;
using System.Collections.Immutable;
using CsCheck;
using Xunit;

namespace Dispatcher.Tests;

// The §17.3 row 13 shape against the stub: DUCKY_REPEAT repetitions (default 50), each a CsCheck SampleParallel over the
// linearizable operations (awaited DispatchAsync, ReadState) against a sequential reducer model, under a 10 s WaitAsync.
// The counts are the S-4 answer (docs/spec/spikes.md); S4_SEQ and S4_PAR override them for tuning runs. iter is left to
// CsCheck (100, or CsCheck_Iter), so PropertyLong's CsCheck_Iter can still lengthen the run.
public sealed class LinearizabilityTests
{
    private static readonly int _maxSequentialOperations = Env("S4_SEQ", 10);
    private static readonly int _maxParallelOperations = Env("S4_PAR", 6);

    public static TheoryData<int> Repeat() => [.. Enumerable.Range(1, Env("DUCKY_REPEAT", 50))];

    [Theory]
    [MemberData(nameof(Repeat))]
    public async Task Linearizability_DispatchVsModel(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;

        var dispatch = Gen.Int[0, 99].Operation<Actual, Model>(
            i => $"DispatchAsync({i})", (a, i) => a.DispatchAndWait(i), (m, i) => m.State.Add(i));
        // Each ReadState carries a fresh key, so the read the stub returned is compared with the model's read of the same
        // operation in the linearization being tried.
        var read = Gen.Int.Select(_ => new object()).Operation<Actual, Model>(
            _ => "ReadState", (a, k) => a.Reads[k] = a.Store.State, (m, k) => m.Reads[k] = [.. m.State]);

        var run = Task.Run(
            () => Gen.Const(() => (new Actual(), new Model())).SampleParallel(
                dispatch,
                read,
                equal: (a, m) => a.Store.State.SequenceEqual(m.State)
                    && a.Reads.Count == m.Reads.Count
                    && m.Reads.All(r => a.Reads.TryGetValue(r.Key, out var seen) && seen.SequenceEqual(r.Value)),
                maxSequentialOperations: _maxSequentialOperations,
                maxParallelOperations: _maxParallelOperations),
            cancellationToken);

        await run.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, cancellationToken);
    }

    private static int Env(string name, int fallback) =>
        int.TryParse(Environment.GetEnvironmentVariable(name), out var n) ? n : fallback;

    private sealed class Actual
    {
        public DispatcherStub Store { get; } = new();

        public ConcurrentDictionary<object, ImmutableList<int>> Reads { get; } = new();

        // An operation is synchronous, so the awaited DispatchAsync blocks here until its action is reduced.
        public void DispatchAndWait(int action) => Store.DispatchAsync(action).GetAwaiter().GetResult();

        public override string ToString() => $"[{string.Join(',', Store.State)}]";
    }

    private sealed class Model
    {
        public List<int> State { get; } = [];

        public Dictionary<object, ImmutableList<int>> Reads { get; } = [];

        public override string ToString() => $"[{string.Join(',', State)}]";
    }
}

using Ducky.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Concurrency.Tests;

// SPEC §15 and §17.3 row 22: TestStore.Processed and Failures are immutable snapshots, so the test thread can enumerate
// them while a LongRunning effect keeps dispatching from pool threads: no "Collection was modified", no torn read, and
// every snapshot is a prefix of the final list.
public sealed class TestStoreInterleavingTests
{
    private const int Ticks = 400;

    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public async Task TestStore_ProcessedReadWhileLongRunningEffectDispatches_NoException(int repeat)
    {
        _ = repeat;
        var cancellationToken = TestContext.Current.CancellationToken;
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var store = TestStore.Create(
            d =>
            {
                d.ThrowOnUnhandledAction = false;
                d.AddSlice<TickSlice>().AddEffect<StreamEffect>();
            },
            s => s.AddSingleton(done),
            typeof(Flagged));
        await store.InitializeAsync(cancellationToken);

        // The reader runs on a dedicated thread: it spins until the stream ends and must not hold a pool thread the
        // effect's continuations need. stop ends it when the repeat fails first, so no failed repeat leaves it spinning.
        using var stop = new CancellationTokenSource();
        var reader = Task.Factory.StartNew(
            () =>
            {
                List<(IReadOnlyList<ActionContext> Processed, IReadOnlyList<object> Failures)> snapshots = [];
                while (!done.Task.IsCompleted && !stop.IsCancellationRequested)
                {
                    var processed = store.Processed;
                    var failures = store.Failures;
                    _ = processed.Count(c => c.Action is Tick);
                    _ = failures.Count(f => f is Flagged);
                    snapshots.Add((processed, failures));
                }

                return snapshots;
            },
            cancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        List<(IReadOnlyList<ActionContext> Processed, IReadOnlyList<object> Failures)> snapshots;
        try
        {
            store.Dispatch(new Start());
            snapshots = await Interleaving.Within(reader);
        }
        finally
        {
            await stop.CancelAsync();
        }

        await Interleaving.Within(store.Settled());

        var processed = store.Processed;
        var failures = store.Failures;
        processed.Count(c => c.Action is Tick).ShouldBe(Ticks);
        failures.Count.ShouldBe(Ticks / 4);
        store.State.Get<TickState>().Count.ShouldBe(Ticks);
        snapshots.ShouldAllBe(s => IsPrefix(s.Processed, processed) && IsPrefix(s.Failures, failures));
    }

    private static bool IsPrefix<T>(IReadOnlyList<T> snapshot, IReadOnlyList<T> final) =>
        snapshot.Count <= final.Count && snapshot.SequenceEqual(final.Take(snapshot.Count));

    private sealed record Start;

    private sealed record Tick(int Seq);

    // A failure type (Create's failureTypes) that no slice reduces.
    private sealed record Flagged(int Seq);

    private sealed record TickState(int Count);

    private sealed class TickSlice : Slice<TickState>
    {
        public TickSlice() => On<Tick>(s => s with { Count = s.Count + 1 });

        protected override TickState Initial => new(0);
    }

    // Each Task.Yield moves the stream to a pool thread, so the dispatches (and the inline drains they start) come
    // from changing threads while the test thread reads.
    private sealed class StreamEffect(TaskCompletionSource done) : Effect<Start>
    {
        public override bool LongRunning => true;

        public override async Task Handle(Start action, EffectContext context, CancellationToken cancellationToken)
        {
            try
            {
                for (var i = 0; i < Ticks; i++)
                {
                    context.Dispatch(new Tick(i));
                    if (i % 4 == 0)
                    {
                        context.Dispatch(new Flagged(i));
                    }

                    await Task.Yield();
                }
            }
            finally
            {
                done.TrySetResult();
            }
        }
    }
}

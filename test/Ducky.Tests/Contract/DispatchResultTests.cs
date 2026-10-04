using Ducky.Tests.ContractFixtures;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using TestMeterFactory = Ducky.Tests.Diagnostics.TelemetryTests.TestMeterFactory;

namespace Ducky.Tests.Contract;

// SPEC §5.2, §6.4 steps 1, 3 and 4-8, §6.6, §6.11, §17.5; INV-10, INV-02. Every DispatchResult a DispatchAsync task can
// complete with, one row per path. Each Dropped path counts ducky.dispatch.dropped once with its reason; the
// after-disposal rows complete Disposed and count nothing. The SliceStore rows arrive with SliceStore (M9-01b).
public sealed class DispatchResultTests
{
    public static TheoryData<string> Paths() =>
    [
        "Reduced",
        "Vetoed",
        "Failed",
        "Dropped: depth",
        "Dropped: step-3 run check",
        "Dropped: EffectContext call-time run check",
        "Disposed",
        "Disposed: EffectContext of a Merge run after DisposeAsync began",
        "Disposed: run-tagged action processed after dispose steps 1-2",
    ];

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task DispatchAsync_EveryPath_ReturnsExpectedResult(string path)
    {
        using var meters = new TestMeterFactory();
        using var dropped = new MetricCollector<long>(meters, "Ducky", "ducky.dispatch.dropped");
        var (actual, expected, reason) = path switch
        {
            "Reduced" => (await Reduced(meters), DispatchResult.Reduced, null),
            "Vetoed" => (await Vetoed(meters), DispatchResult.Vetoed, null),
            "Failed" => (await Failed(meters), DispatchResult.Failed, null),
            "Dropped: depth" => (await DroppedByDepth(meters), DispatchResult.Dropped, "depth"),
            "Dropped: step-3 run check" => (await DroppedAtProcess(meters), DispatchResult.Dropped, "run"),
            "Dropped: EffectContext call-time run check" => (await DroppedAtCall(meters), DispatchResult.Dropped, "run"),
            "Disposed" => (await Disposed(meters), DispatchResult.Disposed, null),
            "Disposed: EffectContext of a Merge run after DisposeAsync began" => (await DisposedMergeRun(meters), DispatchResult.Disposed, null),
            "Disposed: run-tagged action processed after dispose steps 1-2" => (await DisposedAtProcess(meters), DispatchResult.Disposed, (string?)null),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null),
        };

        actual.ShouldBe(expected);
        dropped.GetMeasurementSnapshot().Select(m => (m.Value, m.Tags["ducky.drop.reason"]))
            .ShouldBe(reason is null ? [] : [(1L, (object?)reason)]);
    }

    private static DuckyStore Store(
        TestMeterFactory meters,
        JournalSlice? slice = null,
        Middleware? middleware = null,
        Effect? effect = null,
        int maxDispatchDepth = 64) =>
        new(
            [slice ?? new JournalSlice()],
            NullLogger.Instance,
            maxDispatchDepth,
            middleware: () => middleware is null ? [] : [middleware],
            effects: () => effect is null ? [] : [(effect, false)],
            meterFactory: meters);

    private static async Task<DispatchResult> Reduced(TestMeterFactory meters)
    {
        await using var store = Store(meters);
        return await store.DispatchAsync(new Go(1));
    }

    private static async Task<DispatchResult> Vetoed(TestMeterFactory meters)
    {
        await using var store = Store(meters, middleware: new Veto());
        return await store.DispatchAsync(new Go(1));
    }

    private static async Task<DispatchResult> Failed(TestMeterFactory meters)
    {
        await using var store = Store(meters);
        return await store.DispatchAsync(new Fail());
    }

    // Step 1: Go's reducer dispatches a synchronous child at depth 1, past MaxDispatchDepth 0.
    private static async Task<DispatchResult> DroppedByDepth(TestMeterFactory meters)
    {
        var slice = new JournalSlice();
        await using var store = Store(meters, slice, maxDispatchDepth: 0);
        Task<DispatchResult>? child = null;
        slice.OnGo = () => child = store.DispatchAsync(new Go(2));
        (await store.DispatchAsync(new Go(1))).ShouldBe(DispatchResult.Reduced);
        return await child.ShouldNotBeNull();
    }

    // Step 3 on a live store: Go(2) is queued before run 1's prefix queues Child(1), so Go(2) supersedes run 1 (Switch)
    // before Child(1) is processed, and FIFO still drops the stale Child(1). Run 2's Child(2) is reduced.
    private static async Task<DispatchResult> DroppedAtProcess(TestMeterFactory meters)
    {
        var effect = new ParkedEffect(Concurrency.Switch) { DispatchChild = true };
        await using var store = Store(meters, effect: effect);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.BeforeProcessHook = action =>
        {
            if (action is Go { Id: 1 })
            {
                store.Dispatch(new Go(2));
            }
        };
        try
        {
            store.Dispatch(new Go(1));
            (await effect.Children[2]).ShouldBe(DispatchResult.Reduced);
            return await effect.Children[1];
        }
        finally
        {
            effect.Release();
        }
    }

    // §6.6: a superseded Switch run's EffectContext drops at call time, on a live store.
    private static async Task<DispatchResult> DroppedAtCall(TestMeterFactory meters)
    {
        var effect = new ParkedEffect(Concurrency.Switch);
        await using var store = Store(meters, effect: effect);
        try
        {
            (await store.DispatchAsync(new Go(1))).ShouldBe(DispatchResult.Reduced);
            (await store.DispatchAsync(new Go(2))).ShouldBe(DispatchResult.Reduced);
            return await effect.Contexts[1].DispatchAsync(new Child(1));
        }
        finally
        {
            effect.Release();
        }
    }

    private static async Task<DispatchResult> Disposed(TestMeterFactory meters)
    {
        var store = Store(meters);
        (await store.DispatchAsync(new Go(1))).ShouldBe(DispatchResult.Reduced);
        await store.DisposeAsync();
        return await store.DispatchAsync(new Go(2));
    }

    // A Merge run's token is the store lifetime: once DisposeAsync began, its EffectContext completes Disposed, never Dropped.
    private static async Task<DispatchResult> DisposedMergeRun(TestMeterFactory meters)
    {
        var effect = new ParkedEffect(Concurrency.Merge);
        var store = Store(meters, effect: effect);
        try
        {
            (await store.DispatchAsync(new Go(1))).ShouldBe(DispatchResult.Reduced);
            var disposing = store.DisposeAsync();
            var result = await effect.Contexts[1].DispatchAsync(new Child(1));
            effect.Release();
            await disposing;
            return result;
        }
        finally
        {
            effect.Release();
        }
    }

    // Step 3 after disposal began: the Merge run's Child(1) is queued while live, then dispose steps 1-2 run at the top of
    // its Process (BeforeProcessHook), cancelling the run's token: Disposed, uncounted.
    private static async Task<DispatchResult> DisposedAtProcess(TestMeterFactory meters)
    {
        var effect = new ParkedEffect(Concurrency.Merge) { DispatchChild = true };
        var store = Store(meters, effect: effect);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        ValueTask disposing = default;
        store.Dispatcher.BeforeProcessHook = action =>
        {
            if (action is Child)
            {
                disposing = store.DisposeAsync();
            }
        };
        try
        {
            store.Dispatch(new Go(1));
            var result = await effect.Children[1];
            effect.Release();
            await disposing;
            return result;
        }
        finally
        {
            effect.Release();
        }
    }
}

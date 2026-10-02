using Ducky.Tests.EffectFixtures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Tests;

// SPEC §6.4 step 3 (the run check), §6.6 (Switch slots, compare-and-remove, CTS ownership, EffectRunToken, the
// EffectContext precedence: disposal before the run check); INV-02, INV-11. Gates are plain TaskCompletionSources, so
// SetResult resumes the run inline on the test thread; every test asserts SlotCount == 0 once WhenIdleAsync completed.
public sealed class SwitchTests
{
    private const int RunDropped = 1050;

    // Run 1 ignores its cancelled token and completes after run 2 replaced its slot: its compare-and-remove fails, so
    // run 2's slot stays installed until run 2 itself ends.
    [Fact]
    public async Task Switch_OldRunCompletion_DoesNotRemoveNewSlot()
    {
        var gates = new Dictionary<int, TaskCompletionSource> { [1] = new(), [2] = new() };
        var tokens = new Dictionary<int, CancellationToken>();
        var effect = new SwitchHandler<Load>(async (load, _, token) =>
        {
            tokens[load.Id] = token;
            await gates[load.Id].Task.ConfigureAwait(false);
        });
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        tokens[1].IsCancellationRequested.ShouldBeTrue();
        tokens[2].IsCancellationRequested.ShouldBeFalse();
        store.Dispatcher.SlotCount.ShouldBe(1);

        gates[1].SetResult();
        store.Dispatcher.SlotCount.ShouldBe(1);

        gates[2].SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2)]);
        Should.Throw<ObjectDisposedException>(() => tokens[2].WaitHandle); // run 2 took its own slot out: it owned the Cts
    }

    // A superseded run that ignores its token still can't publish: both dispatch calls are dropped at call time (Debug
    // log), while the live run's dispatch from its synchronous prefix is reduced.
    [Fact]
    public async Task Switch_SupersededRunIgnoringToken_CannotDispatch()
    {
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        Task<DispatchResult>? stale = null;
        var effect = new SwitchHandler<Load>(async (load, context, _) =>
        {
            if (load.Id == 2)
            {
                context.Dispatch(new Loaded(2));
                return;
            }

            await gate.Task.ConfigureAwait(false);
            context.Dispatch(new Loaded(10));
            stale = context.DispatchAsync(new Loaded(1));
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        store.Dispatch(new Load(2));
        gate.SetResult();

        (await stale.ShouldNotBeNull()).ShouldBe(DispatchResult.Dropped);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), new Loaded(2)]);
        var drops = logger.Collector.GetSnapshot().Where(r => r.Id.Id == RunDropped).ToList();
        drops.Select(r => r.Message).ShouldBe([
            $"{typeof(Loaded)} dropped: the effect run that dispatched it was superseded",
            $"{typeof(Loaded)} dropped: the effect run that dispatched it was superseded",
        ]);
        drops.ShouldAllBe(r => r.Level == LogLevel.Debug);
    }

    // Run 1 dispatches Loaded(1) while its run is still current, so the call-time check lets it through, but Load(2) is
    // already queued ahead of it: processing Load(2) supersedes run 1, and step 3 drops Loaded(1) at process time.
    [Fact]
    public async Task Switch_StaleResultQueuedBeforeSupersession_IsDropped()
    {
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        var hold = new TaskCompletionSource();
        Task<DispatchResult>? stale = null;
        var effect = new SwitchHandler<Load>(async (load, context, _) =>
        {
            if (load.Id == 1)
            {
                await gate.Task.ConfigureAwait(false);
                stale = context.DispatchAsync(new Loaded(1));
                await hold.Task.ConfigureAwait(false);
            }
        });
        DuckyStore store = null!;
        store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        using var selection = store.Select(s => s.Get<Seen>(), seen =>
        {
            if (seen.Actions[^1] is Base)
            {
                store.Dispatch(new Load(2));
                gate.SetResult();
            }
        });

        store.Dispatch(new Load(1));
        store.Dispatch(new Base());

        (await stale.ShouldNotBeNull()).ShouldBe(DispatchResult.Dropped);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Base(), new Load(2)]);
        logger.Collector.GetSnapshot().Where(r => r.Id.Id == RunDropped).ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Debug);
        hold.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
    }

    // The drainer cancels the superseded run with CancelAsync, so the throwing callback runs off the drainer: the action
    // that superseded it is reduced and its own run starts, and the fault is logged at Warning 1014, never EffectFailed. The
    // drainer took the slot out, so it owns the Cts and disposes it once the callbacks ran.
    [Fact]
    public async Task Switch_CancelCallbackThrows_DrainerSurvives()
    {
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        List<int> handled = [];
        CancellationToken superseded = default;
        var effect = new SwitchHandler<Load>(async (load, _, token) =>
        {
            handled.Add(load.Id);
            if (load.Id == 1)
            {
                superseded = token;
                token.Register(() => throw new InvalidOperationException("callback"));
                await gate.Task.ConfigureAwait(false);
            }
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));
        (await store.DispatchAsync(new Load(2))).ShouldBe(DispatchResult.Reduced);
        (await store.DispatchAsync(new Loaded(3))).ShouldBe(DispatchResult.Reduced);

        handled.ShouldBe([1, 2]);
        var record = await LogAsync(logger, 1014);
        record.Level.ShouldBe(LogLevel.Warning);
        record.Message.ShouldBe("A cancellation callback of a superseded Switch run threw");
        record.Exception.ShouldBeOfType<AggregateException>().Flatten().InnerExceptions
            .ShouldHaveSingleItem().ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("callback");
        Should.Throw<ObjectDisposedException>(() => superseded.WaitHandle); // the drainer owned and disposed the Cts
        gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1), new Load(2), new Loaded(3)]);
    }

    // Switch cancels only the same key: a run for product B leaves product A's run alone, and a second run for A
    // supersedes only the first A run, whose cancellation is our cancellation, never EffectFailed.
    [Fact]
    public async Task Effect_KeyedSwitch_ProductB_DoesNotCancelProductA()
    {
        List<(LoadProduct Action, CancellationToken Token, TaskCompletionSource Gate)> runs = [];
        var effect = new SwitchHandler<LoadProduct>(
            async (load, _, token) =>
            {
                var gate = new TaskCompletionSource();
                runs.Add((load, token, gate));
                await gate.Task.WaitAsync(token).ConfigureAwait(false);
            },
            key: load => load.Product);
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new LoadProduct("A", 1));
        store.Dispatch(new LoadProduct("B", 1));
        runs[0].Token.IsCancellationRequested.ShouldBeFalse();
        store.Dispatcher.SlotCount.ShouldBe(2);

        store.Dispatch(new LoadProduct("A", 2));
        runs.Select(r => r.Token.IsCancellationRequested).ShouldBe([true, false, false]);
        store.Dispatcher.SlotCount.ShouldBe(2);

        runs[1].Gate.SetResult();
        runs[2].Gate.SetResult();
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBeEmpty();
    }

    // Non-normative: the key is computed inside the run's own try/catch, so a throwing key function is EffectFailed for
    // that effect, its handler never runs and no slot is installed (the run disposes the CTS it never installed).
    [Fact]
    public async Task Switch_ConcurrencyKeyThrows_EffectFailedNoSlot()
    {
        var handled = false;
        var effect = new SwitchHandler<Load>(
            (_, _, _) =>
            {
                handled = true;
                return Task.CompletedTask;
            },
            key: _ => throw new FormatException("key"));
        var store = new DuckyStore([new SeenSlice()], NullLogger.Instance, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        handled.ShouldBeFalse();
        store.Dispatcher.SlotCount.ShouldBe(0);
        var actions = store.State.Get<Seen>().Actions;
        actions.Count.ShouldBe(2);
        actions[1].ShouldBeOfType<EffectFailed>().Exception.ShouldBeOfType<FormatException>();
    }

    // Non-normative: once disposal began, a run's dispatch completes Disposed (Debug 1002), never Dropped, although
    // disposal cancelled the run's token: the disposal check precedes the run check (§6.6, INV-02).
    [Fact]
    public async Task Switch_DispatchAfterDisposalBegan_IsDisposedNotDropped()
    {
        var logger = new FakeLogger();
        var gate = new TaskCompletionSource();
        Task<DispatchResult>? late = null;
        CancellationToken runToken = default;
        var effect = new SwitchHandler<Load>(async (_, context, token) =>
        {
            runToken = token;
            await gate.Task.ConfigureAwait(false);
            context.Dispatch(new Loaded(1));
            late = context.DispatchAsync(new Loaded(2));
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        store.Dispatch(new Load(1));

        await store.DisposeAsync();
        runToken.IsCancellationRequested.ShouldBeTrue();
        gate.SetResult();

        (await late.ShouldNotBeNull()).ShouldBe(DispatchResult.Disposed);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        logger.Collector.GetSnapshot().Select(r => r.Id.Id).ShouldBe([1002, 1002]);
    }

    // Non-normative: step 3 applies the same precedence. A Merge run's action (its token is the store lifetime) is
    // queued before disposal and dequeued after disposal began and cancelled the lifetime: Disposed, not Dropped.
    [Fact]
    public async Task RunCheck_ActionDequeuedAfterDisposalBegan_IsDisposedNotDropped()
    {
        var logger = new FakeLogger();
        Task<DispatchResult>? queued = null;
        var effect = new Handler<Load>((_, context, _) =>
        {
            queued = context.DispatchAsync(new Loaded(1));
            return Task.CompletedTask;
        });
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);
        Task? disposal = null;
        store.Dispatcher.BeforeProcessHook = action =>
        {
            if (action is Loaded)
            {
                disposal = store.DisposeAsync().AsTask();
            }
        };

        store.Dispatch(new Load(1));

        (await queued.ShouldNotBeNull()).ShouldBe(DispatchResult.Disposed);
        await disposal.ShouldNotBeNull().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await store.WhenIdleAsync(TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1)]);
        logger.Collector.GetSnapshot().ShouldNotContain(r => r.Id.Id == RunDropped);
    }

    // Non-normative: the key's Equals and GetHashCode are user code. Installing into an empty bucket never calls Equals,
    // and GetHashCode succeeds once, so only the run's compare-and-remove could throw: it must run neither, or the finally
    // would leave before the idle decrement and the slot would leak.
    [Fact]
    public async Task Switch_KeyEqualityThrowsOnRemove_IdleReachedAndSlotFreed()
    {
        var logger = new FakeLogger();
        var effect = new SwitchHandler<Load>((_, _, _) => Task.CompletedTask, _ => new ThrowsAfterInstallKey());
        var store = new DuckyStore([new SeenSlice()], logger, effects: () => [(effect, false)]);

        store.Dispatch(new Load(1));

        await store.WhenIdleAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        store.Dispatcher.SlotCount.ShouldBe(0);
        store.State.Get<Seen>().Actions.ShouldBe([new Load(1)]);
        logger.Collector.GetSnapshot().ShouldBeEmpty();
    }

    private sealed class ThrowsAfterInstallKey
    {
        private int _hashes;

        public override bool Equals(object? obj) => throw new InvalidOperationException("key Equals");

        public override int GetHashCode() =>
            ++_hashes == 1 ? 1 : throw new InvalidOperationException("key GetHashCode after install");
    }

    private static async Task<FakeLogRecord> LogAsync(FakeLogger logger, int eventId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await foreach (var record in logger.Collector.GetLogsAsync(timeout.Token).ConfigureAwait(false))
        {
            if (record.Id.Id == eventId)
            {
                return record;
            }
        }

        throw new InvalidOperationException("unreachable: GetLogsAsync ends only by cancellation");
    }

    private sealed record LoadProduct(string Product, int Version);
}

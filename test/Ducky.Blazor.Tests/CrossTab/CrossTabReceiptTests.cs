using System.Collections.Concurrent;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.DistributedCache;
using Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.CrossTab;

// SPEC §11.7 (M7-02b), INV-16, INV-17, INV-18: absence is a payload for the echo guard. A removal (a per-key one, a missing
// value on the re-read-all path, a too-large key gone before its pull) bumps the key's sequence number under _issue, then
// restores the initial state and sets the baseline to its local serialization, or restores nothing when the baseline is
// already absent. storageSet/storageRemove answering false re-registers the id and re-reads every area. Scoped keys are
// matched against the current epoch's scope, on receipt and again when the restore is issued.
public sealed class CrossTabReceiptTests
{
    private static readonly TimeSpan _writeDebounce = TimeSpan.FromSeconds(1);

    // Its own length, so waiting for the dial's debounce timer never takes one of the level's.
    private static readonly TimeSpan _dialDebounce = TimeSpan.FromSeconds(2);

    private static CancellationToken Ct => Xunit.TestContext.Current.CancellationToken;

    [Fact]
    public async Task CrossTab_RemovalReceipt_NoWriteBack()
    {
        // Level was written, then changed again; that change is still in its debounce when another tab removes the key.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, _writeDebounce))
            .Persist<DialSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, _dialDebounce)));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        await x.H.Time.TimerAsync(_writeDebounce);
        x.H.Time.Advance(_writeDebounce);
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        x.H.Store.Dispatch(new SetLevel(4));
        await x.H.Time.TimerAsync(_writeDebounce);

        // The removal resets the slice and sets the baseline to the initial state's local serialization, so the pending
        // write of the now-initial state dedupes away instead of recreating the key the other tab removed.
        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.H.Time.Advance(_writeDebounce);
        await x.H.IterationsAsync("level", 2);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.ShouldHaveSingleItem().Payload.ShouldBe("""{"Value":3}""");
        x.EventIds(LogLevel.Debug).ShouldNotContain(2054);

        // A key whose baseline is already absent (a local change still waiting for its first write): nothing is restored,
        // with a Debug log naming the key, and the change is still written.
        x.H.Store.Dispatch(new SetDial(7));
        await x.H.Time.TimerAsync(_dialDebounce);
        await x.ReceiveAsync("tab", "local", "ducky:dial", null);
        x.H.Store.State.Get<Dial>().ShouldBe(new(7));
        x.Restores.Entries.ShouldHaveSingleItem();
        x.H.Middleware.LastKnownPayload.ContainsKey("ducky:dial").ShouldBeFalse();
        var kept = x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2054);
        kept.Level.ShouldBe(LogLevel.Debug);
        kept.Message.ShouldContain("ducky:dial");
        x.H.Time.Advance(_dialDebounce);
        var write = await x.H.NextWriteAsync();
        (write.Key, write.Payload).ShouldBe(("ducky:dial", """{"Value":7}"""));
    }

    [Fact]
    public async Task CrossTab_ReceiptQueuedBehindDrain_WriterSignalledOnCommit_Deterministic()
    {
        // (non-normative) A receipt handled while another drain is running: its restore queues behind that drain. Its
        // writer is signalled only once the restore has committed, never when it is issued, or it would read the state from
        // before the restore, find it unlike the new baseline and write it back (here: level 3 over the removal).
        var plan = new DrainerPlan();
        await using var x = new CrossTabHarness(
            static d => d
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
                .Persist<DialSlice>(static o => o.SyncAcrossTabs = true)
                .Use<RunOnDrainer>(),
            services: s => s.AddSingleton(plan));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var level = x.H.Middleware.Writers["level"];
        var dial = x.H.Middleware.Writers["dial"];
        ConcurrentQueue<(string Keys, long Level, long Dial)> commits = new(); // each signals only its own key
        x.Restores.OnEntry = keys => commits.Enqueue((keys, level.Signals, dial.Signals));
        (long Level, long Dial, Level State) during = default;
        plan.Run = () =>
        {
            var removal = x.Notify("tab", "local", "ducky:level", null, tooLarge: false);
            var value = x.Notify("tab", "local", "ducky:dial", x.Envelope(new Dial(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            removal.IsCompleted.ShouldBeTrue();
            value.IsCompleted.ShouldBeTrue();
            during = (level.Signals, dial.Signals, x.H.Store.State.Get<Level>());
        };

        x.H.Store.Dispatch(new Increment());
        await x.H.Store.WhenIdleAsync(Ct);
        during.ShouldBe((1L, 0L, new Level(3)));
        commits.ShouldBe([("level", 2L, 0L), ("dial", 2L, 1L)]);
        (x.H.Store.State.Get<Level>(), x.H.Store.State.Get<Dial>()).ShouldBe((new(0), new(5)));
        await x.H.IterationsAsync("level", 2);
        await x.H.IterationsAsync("dial", 1);
        x.H.Written.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_ReceiptRestoreFails_LaterLocalChangesStillWritten()
    {
        // (non-normative) A receipt's record goes with its own restore's AfterReduce, also when that restore committed
        // nothing (a BeforeReduce threw, §6.4): a failed restore never holds the key's writes. The first receipt commits,
        // so the second one's restore is the oldest still pending when it fails.
        var failure = new CrossTabFailure();
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Use<FailCrossTab>(),
            services: s => s.AddSingleton(failure));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        failure.Armed = true;
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(6)));
        failure.Armed.ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);

        // Signalled by its failed restore, the writer finds the slice unlike the new baseline and writes it, as for any
        // local state a receipt did not replace; a later local change is written too.
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        x.H.Store.Dispatch(new SetLevel(8));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":8}""");
    }

    [Fact]
    public async Task CrossTab_ReceiptDuringHydration_RestoreFails_LocalStateWrittenOverItsBaseline()
    {
        // (non-normative) §11.7: a value received while the key's hydration read is open is a Hydration restore, and its
        // writer is signalled at once. When that restore commits nothing (a BeforeReduce threw, §6.4), the baseline still
        // names the value read, so once the read closes the writer finds the slice unlike it and writes it, as for any
        // local state a receipt did not replace.
        var failure = new CrossTabFailure { Origin = Origin.Hydration };
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Use<FailCrossTab>(),
            services: s => s.AddSingleton(failure));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.HoldNextRead(read);
        var init = x.H.InitializeAsync();
        await WriterHarness.Until(() => x.H.Js.Calls.Any(static call => call.Identifier == "storageGet"));

        failure.Armed = true;
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(2)));
        failure.Armed.ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":2}""");

        read.SetResult(x.Envelope(new Level(1)));
        await init;
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":0}""");
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
    }

    [Fact]
    public async Task CrossTab_PublicRestoreAheadOfQueuedReceipt_KeySignalledOnReceiptCommit_Deterministic()
    {
        // (non-normative) A public IStore.Restore with Origin.CrossTab queued ahead of a receipt's restore is not the
        // receipt's: its commit signals nothing; the receipt's own commit signals the key.
        var plan = new DrainerPlan();
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Use<RunOnDrainer>(),
            services: s => s.AddSingleton(plan));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var level = x.H.Middleware.Writers["level"];
        ConcurrentQueue<(Level State, long Signals)> commits = new();
        x.Restores.OnEntry = _ => commits.Enqueue((x.H.Store.State.Get<Level>(), level.Signals));
        plan.Run = () =>
        {
            x.H.Store.Restore(new Dictionary<string, object> { ["level"] = new Level(7) }, Origin.CrossTab);
            var receipt = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            receipt.IsCompleted.ShouldBeTrue();
        };

        x.H.Store.Dispatch(new Increment());
        await x.H.Store.WhenIdleAsync(Ct);
        commits.ShouldBe([(new Level(7), 1L), (new Level(5), 2L)]);
        await x.H.IterationsAsync("level", 2);
        x.H.Written.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_SupersededQueuedReceipt_CommitSignalsNothing_Deterministic()
    {
        // (non-normative, INV-16) Two receipts for one key queue behind another drain. The older one's commit is superseded:
        // the newer receipt holds the key's record, so it signals nothing. A writer signalled there would snapshot 5 while the
        // baseline already holds 6 and, with the record gone, write 5 over it. Only the newer receipt's commit signals.
        var plan = new DrainerPlan();
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Use<RunOnDrainer>(),
            services: s => s.AddSingleton(plan));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var level = x.H.Middleware.Writers["level"];
        ConcurrentQueue<(Level State, long Signals)> commits = new();
        x.Restores.OnEntry = _ => commits.Enqueue((x.H.Store.State.Get<Level>(), level.Signals));
        plan.Run = () =>
        {
            var older = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            var newer = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(6)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            (older.IsCompleted, newer.IsCompleted).ShouldBe((true, true));
        };

        x.H.Store.Dispatch(new Increment());
        await x.H.Store.WhenIdleAsync(Ct);
        commits.ShouldBe([(new Level(5), 1L), (new Level(6), 2L)]);
        x.H.Store.State.Get<Level>().ShouldBe(new(6));
        await x.H.IterationsAsync("level", 2);
        x.H.Written.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_ReceiptQueuedAtDispose_LocalChangeFlushed_Deterministic()
    {
        // (non-normative) A receipt's restore still queued when disposal begins never reduces (it completes Disposed,
        // INV-29), so stopping the CrossTabSync drops its record: the flush writes the key's pending local change rather
        // than deferring it until it is counted lost.
        var plan = new DrainerPlan();
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, _writeDebounce)).Use<RunOnDrainer>(),
            services: s => s.AddSingleton(plan));
        using var lost = new MetricCollector<long>(x.H.Meters, "Ducky", "ducky.persistence.lost");
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        await x.H.Time.TimerAsync(_writeDebounce);
        x.H.Time.Advance(_writeDebounce);
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        x.H.Store.Dispatch(new SetLevel(8));
        await x.H.Time.TimerAsync(_writeDebounce);
        Task? disposal = null;
        plan.Run = () =>
        {
            var receipt = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            receipt.IsCompleted.ShouldBeTrue();
            disposal = x.H.DisposeStoreAsync();
        };

        x.H.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => disposal is not null);
        await disposal!.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(8));
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":8}"""]);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_ReceiptQueuedAtDispose_CleanKey_NoWriteBack_Deterministic()
    {
        // (non-normative) The twin of the test above without a pending local change: the key is clean when disposal drops
        // the queued receipt's record, so stopping the CrossTabSync wakes no writer and the flush writes nothing back over
        // the other tab's value.
        var plan = new DrainerPlan();
        await using var x = new CrossTabHarness(
            static d => d.Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, _writeDebounce)).Use<RunOnDrainer>(),
            services: s => s.AddSingleton(plan));
        using var lost = new MetricCollector<long>(x.H.Meters, "Ducky", "ducky.persistence.lost");
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        await x.H.Time.TimerAsync(_writeDebounce);
        x.H.Time.Advance(_writeDebounce);
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        Task? disposal = null;
        plan.Run = () =>
        {
            var receipt = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            receipt.IsCompleted.ShouldBeTrue();
            disposal = x.H.DisposeStoreAsync();
        };

        x.H.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => disposal is not null);
        await disposal!.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}"""]);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_WriteDeferredByQueuedReceiptAtDispose_LocalChangeFlushed_Deterministic()
    {
        // (non-normative) The key's write compared while a receipt's restore was queued behind another drain, so it deferred
        // to that restore's commit; disposal then begins before the restore reduces (it completes Disposed, INV-29). Stopping
        // the CrossTabSync signals each dirty writer whose record it drops: the flush writes the pending local change, not lost.
        var plan = new DrainerPlan();
        Hold? hold = null; // armed by the test; the writer's hook takes it once
        await using var x = new CrossTabHarness(
            d => d
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
                .Use<RunOnDrainer>()
                .AddBlazor(o => o.AfterDeferHook = (key, point) =>
                {
                    if (key == "level" && point == DeferPoint.AfterSnapshot && Interlocked.Exchange(ref hold, null) is { } taken)
                    {
                        taken.Paused.SetResult();
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded wait on the test thread's progress
                        taken.Resume.Task.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
#pragma warning restore VSTHRD002
                    }
                }),
            services: s => s.AddSingleton(plan));
        using var lost = new MetricCollector<long>(x.H.Meters, "Ducky", "ducky.persistence.lost");
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];

        var held = hold = new();
        x.H.Store.Dispatch(new SetLevel(9));
        await held.Paused.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Task? disposal = null;
        (bool Handled, bool Deferred) during = default;
        plan.Run = () =>
        {
            var receipt = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            var iterations = writer.Iterations;
            held.Resume.SetResult();
#pragma warning disable VSTHRD002 // justification: bounded wait on the writer's progress, on the drainer the restore queues behind
            var ended = WriterHarness.Until(() => writer.Iterations > iterations).Wait(TimeSpan.FromSeconds(10), Ct);
#pragma warning restore VSTHRD002
            during = (receipt.IsCompleted, ended && writer.Dirty && !writer.Signalled);
            disposal = x.H.DisposeStoreAsync();
        };

        x.H.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => disposal is not null);
        await disposal!.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        during.ShouldBe((true, true));
        x.H.Store.State.Get<Level>().ShouldBe(new(9));
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":9}"""]);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_ReceiptDuringInFlightWrite_StateFromBeforeItNeverWritten_Deterministic()
    {
        // (non-normative, INV-16) A write already past its snapshot when the state moves: a receipt sets its baseline before
        // its restore commits, so that write holds the state from before it and may read the baseline after it. Written as
        // is, it would bring back the key another tab removed (9, then the removal's own 0).
        var plan = new DrainerPlan();
        Hold? hold = null; // armed by the test; the writer's hook takes it once
        await using var x = new CrossTabHarness(
            d => d
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
                .Use<RunOnDrainer>()
                .AddBlazor(o => o.AfterDeferHook = (key, point) =>
                {
                    if (key == "level" && point == DeferPoint.AfterSnapshot && Interlocked.Exchange(ref hold, null) is { } taken)
                    {
                        taken.Paused.SetResult();
                        // The writer's own thread blocks; the test thread commits meanwhile.
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded wait on the test thread's progress
                        taken.Resume.Task.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
#pragma warning restore VSTHRD002
                    }
                }),
            services: s => s.AddSingleton(plan));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];

        // The removal commits while the write holds 9: the slot is no longer the snapshot's, so the key goes again.
        var held = hold = new();
        x.H.Store.Dispatch(new SetLevel(9));
        await held.Paused.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        held.Resume.SetResult();
        await x.H.IterationsAsync("level", 3);
        await WriterHarness.Until(() => !writer.Dirty);
        x.H.Written.ShouldHaveSingleItem();

        // A DevTools jump commits while the write holds 9, and signals nothing: the key goes again on its own and writes the
        // jumped-to state that was pending (§11.5), never the 9 it no longer holds.
        held = hold = new();
        x.H.Store.Dispatch(new SetLevel(9));
        await held.Paused.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.H.Store.Restore(new Dictionary<string, object> { ["level"] = new Level(5) }, Origin.DevTools);
        held.Resume.SetResult();
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        await WriterHarness.Until(() => !writer.Dirty);

        // The removal's restore queues behind another drain while the write holds 9, and has not committed when the write
        // compares: the slot is still the snapshot's, but the receipt is recorded, so nothing is written until its commit
        // signals the key, which then finds the removal's baseline.
        held = hold = new();
        x.H.Store.Dispatch(new SetLevel(9));
        await held.Paused.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        (bool Handled, bool Ended, Level State) during = default;
        plan.Run = () =>
        {
            var removal = x.Notify("tab", "local", "ducky:level", null, tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            var iterations = writer.Iterations;
            held.Resume.SetResult();
#pragma warning disable VSTHRD002 // justification: bounded wait on the writer's progress, on the drainer the restore queues behind
            var ended = WriterHarness.Until(() => writer.Iterations > iterations).Wait(TimeSpan.FromSeconds(10), Ct);
#pragma warning restore VSTHRD002
            during = (removal.IsCompleted, ended, x.H.Store.State.Get<Level>());
        };
        x.H.Store.Dispatch(new Increment());
        await x.H.Store.WhenIdleAsync(Ct);
        during.ShouldBe((true, true, new Level(9)));
        await WriterHarness.Until(() => !writer.Dirty);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":5}"""]);
    }

    [Fact]
    public async Task CrossTab_RemovalOfSliceHoldingInitialState_SignalsOnCommit()
    {
        // (non-normative) The slice already holds its initial state (here a public CrossTab restore, which sets no baseline),
        // so the removal's restore changes nothing. Its record is cleared on that commit all the same: its writer is
        // signalled, and a later change is written, not held back for a commit that already happened.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];
        x.H.Store.Restore(new Dictionary<string, object> { ["level"] = x.H.Store.InitialState.Get("level") }, Origin.CrossTab);
        var signals = writer.Signals;

        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        writer.Signals.ShouldBe(signals + 1);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");

        x.H.Store.Dispatch(new SetLevel(5));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
    }

    [Fact]
    public async Task CrossTab_Removal_InitialStateNotSerializable_BumpsAndRestoresNothing()
    {
        // (non-normative, §11.7) A removal bumps the key's sequence number whatever its initial state's serialization, which
        // only its restore needs: the read that began before it is dropped. With a baseline present it still restores
        // nothing, since no baseline could be set for that initial state (the serializer logs it).
        await using var x = new CrossTabHarness(static d => d.AddSlice<GaugeSlice>().Persist<GaugeSlice>(static o => o.SyncAcrossTabs = true));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.HoldNextRead(read);
        var init = x.H.InitializeAsync();
        await WriterHarness.Until(() => x.H.Js.Calls.Any(static call => call.Identifier == "storageGet"));

        await x.ReceiveAsync("tab", "local", "ducky:gauge", null);
        read.SetResult(x.Envelope(new Gauge(1)));
        await init;
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.WasRestored("gauge").ShouldBeFalse();
        x.H.Store.State.Get<Gauge>().Value.ShouldBe(double.NaN);

        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("gauge", 1);
        await x.ReceiveAsync("tab", "local", "ducky:gauge", null);
        x.H.Store.State.Get<Gauge>().ShouldBe(new(3));
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Middleware.LastKnownPayload["ducky:gauge"].ShouldBe("""{"Value":3}""");
    }

    [Fact]
    public async Task CrossTab_RemovalDuringHydration_RemovalWins()
    {
        // Tab B clears while tab A's read is in flight, and the 50 ms-debounced removal lands first: it moves the key's
        // sequence number, so A's read, which saw the value from before the removal, is dropped. Nothing was ever stored
        // here (an absent baseline), so the removal restores nothing; the slice stays unrestored and its load runs.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .AddEffect<LoadUnlessRestored>());
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.HoldNextRead(read);
        var init = x.H.InitializeAsync();
        await WriterHarness.Until(() => x.H.Js.Calls.Any(static call => call.Identifier == "storageGet"));

        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrating);

        read.SetResult(x.Envelope(new Level(1)));
        await init;
        await x.H.Store.WhenIdleAsync(Ct);
        x.Restores.Entries.ShouldBeEmpty();
        x.Restores.Completed.ShouldBe([false]);
        x.H.Store.State.WasRestored<Level>().ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(99));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":99}""");
        x.H.Written.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_ResyncAfterDisconnect_UnwrittenLocalChangeOnOtherKey_Kept()
    {
        // Level was written; dial's first write failed on a disconnect and waits in its retry backoff. A resync (key null,
        // after failed deliveries) finds both keys missing: level, whose baseline exists, is reset; dial, whose baseline is
        // still absent, keeps its local change, which its retry then writes.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .Persist<DialSlice>(static o => o.SyncAcrossTabs = true));
        var dialFailures = 1;
        x.H.OnSet = write => write.Key == "ducky:dial" && dialFailures-- > 0 ? Task.FromException<object?>(new JSDisconnectedException("circuit gone")) : true;
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(2));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        await x.H.IterationsAsync("level", 1);
        x.H.Store.Dispatch(new SetDial(5));
        (await x.H.NextWriteAsync()).Key.ShouldBe("ducky:dial");
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));

        await x.ReceiveAsync("tab", "local", null, null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Store.State.Get<Dial>().ShouldBe(new(5));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2054).Message.ShouldContain("ducky:dial");

        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        var retried = await x.H.NextWriteAsync();
        (retried.Key, retried.Payload).ShouldBe(("ducky:dial", """{"Value":5}"""));
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.Select(static write => write.Key).ShouldBe(["ducky:level", "ducky:dial", "ducky:dial"]);

        // A write that was not delivered is no "unknown id" answer: nothing re-registers.
        x.Watches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CrossTab_ResyncAfterRemoval_UnwrittenLocalChange_Kept()
    {
        // (non-normative, §11.7, INV-16) A removal receipt sets the baseline to the initial state's serialization, which an
        // absent key reads as: a second removal (a resync re-read finding the key missing) is an echo, and keeps a local
        // change still waiting for its first write to the now-absent key. A baseline one value off is no echo: it resets.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var failures = 0;
        x.H.OnSet = _ => failures-- > 0 ? Task.FromException<object?>(new JSDisconnectedException("circuit gone")) : true;
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(1));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await x.H.IterationsAsync("level", 1);

        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.EventIds(LogLevel.Debug).ShouldNotContain(2054);
        await x.H.IterationsAsync("level", 2);

        failures = 1;
        x.H.Store.Dispatch(new SetLevel(5));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));

        await x.ReceiveAsync("tab", "local", null, null);
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.Restores.Entries.ShouldHaveSingleItem();
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2054).Message.ShouldContain("ducky:level");

        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":1}""", """{"Value":5}""", """{"Value":5}"""]);
    }

    [Fact]
    public async Task CrossTab_RemovalAfterWriteOfInitialStateContent_ResetsSlice()
    {
        // (non-normative, §11.7, INV-18) A baseline a write set is no "absent", even when it serializes like the initial
        // state: the key is stored. A removal then resets the slice, and the pending change dedupes away instead of
        // recreating the key the other tab removed. Only a baseline a removal receipt set reads as absent.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, _writeDebounce)));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        await x.H.Time.TimerAsync(_writeDebounce);
        x.H.Time.Advance(_writeDebounce);
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        x.H.Store.Dispatch(new SetLevel(0));
        await x.H.Time.TimerAsync(_writeDebounce);
        x.H.Time.Advance(_writeDebounce);
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":0}""");
        await x.H.IterationsAsync("level", 2);
        x.H.Store.Dispatch(new SetLevel(4));
        await x.H.Time.TimerAsync(_writeDebounce);

        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2054);
        x.H.Time.Advance(_writeDebounce);
        await x.H.IterationsAsync("level", 3);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":0}"""]);
    }

    [Fact]
    public async Task CrossTab_ReceiptReadsOwnUnacknowledgedRemoval_KeyStaysRemoved()
    {
        // ClearPersistedState's removeItem ran, its result not back in .NET yet, so the baseline still holds {3} when a
        // receipt of the key re-reads it and finds it absent. That absence is this store's own removal in flight, an echo:
        // taking the removal branch would reset the clearing store (which keeps its in-memory state, §11.5), and its writer
        // would then write the cleared key again.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var removed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnRemove = _ => removed.Task;
        x.H.Store.Dispatch(new ClearPersistedState());
        try
        {
            (await x.H.NextRemovalAsync()).Key.ShouldBe("ducky:level");
            x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":3}""");

            await x.ReceiveAsync("tab", "local", "ducky:level", null);
            x.Restores.Entries.ShouldBeEmpty();
            x.H.Store.State.Get<Level>().ShouldBe(new(3));
            x.EventIds(LogLevel.Debug).ShouldNotContain(2054);
        }
        finally
        {
            // Answered even after a failed assertion: the dispose would wait on the held removal.
            removed.TrySetResult(true);
        }

        await x.H.IterationsAsync("level", 2);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.ShouldHaveSingleItem();
        x.H.Storage.ContainsKey(("local", "ducky:level")).ShouldBeFalse();
        x.H.Middleware.LastKnownPayload.ContainsKey("ducky:level").ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(3));
        x.H.Middleware.InFlight.ContainsKey("ducky:level").ShouldBeFalse();
        x.H.Middleware.Resolved["ducky:level"].Task.IsCompleted.ShouldBeTrue();

        // Acknowledged, the removal is no longer in flight: another tab's later {5}, then its removal, both apply.
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level"), (Origin.CrossTab, "level")]);
    }

    [Fact]
    public async Task CrossTab_ValueReceiptDuringOwnRemoval_Restored_NotWrittenBack()
    {
        // Only an absence is this store's own removal in flight: a value read meanwhile was written by another store after
        // that removeItem ran, and is restored. The removal's result then keeps the baseline the receipt set (it went over
        // {3}), so the receipt's signal finds state and baseline equal and writes nothing back (INV-18, no echo).
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var removed = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnRemove = _ => removed.Task;
        x.H.Store.Dispatch(new ClearPersistedState());
        try
        {
            (await x.H.NextRemovalAsync()).Key.ShouldBe("ducky:level");
            await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
            x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
            x.H.Store.State.Get<Level>().ShouldBe(new(5));
        }
        finally
        {
            removed.TrySetResult(true);
        }

        await x.H.IterationsAsync("level", 2);
        await WriterHarness.Until(() => !x.H.Middleware.Writers["level"].Dirty && !x.H.Middleware.Writers["level"].Signalled);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}"""]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":5}""");
        x.H.Middleware.InFlight.ContainsKey("ducky:level").ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
    }

    [Fact]
    public async Task CrossTab_ReceiptReadOvertakenByOwnRemoval_Dropped()
    {
        // (non-normative, §11.7) A removal starts like a write: a receipt whose storageGet ran before this store's own
        // removeItem, answered once that removal was acknowledged, read a value storage no longer holds. Restoring it would
        // set a baseline for an absent key: it reads the key again, finds it absent, and the clearing store keeps its state,
        // with an absent baseline.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);

        // Another tab writes {5}; this store's receipt reads it, and the result is held.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = false;
        var answer = x.H.OnGet;
        x.H.OnGet = _ =>
        {
            x.H.OnGet = answer;
            asked = true;
            return read.Task;
        };
        var other = x.Envelope(new Level(5));
        var handled = x.Notify("tab", "local", "ducky:level", other);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        try
        {
            await WriterHarness.Until(() => asked);
            x.H.Store.Dispatch(new ClearPersistedState());
            (await x.H.NextRemovalAsync()).Key.ShouldBe("ducky:level");
            await x.H.IterationsAsync("level", 2);
            x.H.Middleware.LastKnownPayload.ContainsKey("ducky:level").ShouldBeFalse();
        }
        finally
        {
            read.TrySetResult(other);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Store.State.Get<Level>().ShouldBe(new(3));
        x.H.Middleware.LastKnownPayload.ContainsKey("ducky:level").ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTab_RemovalFailed_NotInFlight_RemovalReceiptResets()
    {
        // (non-normative, §11.7) A removal that failed is no longer in flight: until its retry the baseline still names the
        // stored {3}, and a receipt that finds the key absent (another store removed it) resets the slice like any removal.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var removals = 0;
        x.H.OnRemove = _ => ++removals == 1 ? Task.FromException<object?>(new JSDisconnectedException("circuit gone")) : true;
        x.H.Store.Dispatch(new ClearPersistedState());
        (await x.H.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":3}""");

        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
    }

    [Fact]
    public async Task CrossTab_ReceiptReadOvertakenByFailedOwnWrite_ReadAgain()
    {
        // §11.7: a write of this store sent after a receipt's read overtakes it, and the value read is not
        // applied; but a write that failed (a quota error) never landed: the key is read again, and the other tab's {5},
        // which storage still holds, is restored. Dropped, it would leave this store on {7} for good: no later notification
        // comes, and the retries keep failing.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);

        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var asked = false;
        var answer = x.H.OnGet;
        x.H.OnGet = _ =>
        {
            x.H.OnGet = answer;
            asked = true;
            return read.Task;
        };
        var other = x.Envelope(new Level(5));
        var handled = x.Notify("tab", "local", "ducky:level", other);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        try
        {
            await WriterHarness.Until(() => asked);
            x.H.OnSet = static _ => Task.FromException<object?>(new JSException("QuotaExceededError"));
            x.H.Store.Dispatch(new SetLevel(7));
            (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":7}""");
            await x.H.NextFailureAsync();
            await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level"));
        }
        finally
        {
            read.TrySetResult(other);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2056);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":5}""");
    }

    [Fact]
    public async Task CrossTab_ReceiptReadOvertakenByFailedOwnWriteOfSameValue_ReadAgain()
    {
        // §11.7: the read is sent before this store's own write {7}, and returns another tab's {7} while that write is still
        // in flight. Equal to the write in flight, it is still stale (the count moved): the key is read again, after the
        // write. The write fails (a quota error), so the re-read finds the other tab's {7} over the unchanged baseline {3},
        // and restores it: the baseline names what storage holds, and the retry dedupes. Dropped as an echo of the write in
        // flight, the baseline would stay {3}, and the writer would retry {7} and log PersistenceFailed for good.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];

        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reread = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = x.H.OnGet;
        x.H.OnGet = _ =>
        {
            x.H.OnGet = _ =>
            {
                x.H.OnGet = answer;
                return reread.Task;
            };
            return read.Task;
        };
        var write = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = x.Envelope(new Level(7));
        var handled = x.Notify("tab", "local", "ducky:level", other);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        try
        {
            await WriterHarness.Until(() => Reads(x) == 1);
            x.H.OnSet = _ => write.Task;
            x.H.Store.Dispatch(new SetLevel(7));
            (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":7}""");
            read.TrySetResult(other);
            await WriterHarness.Until(() => Reads(x) == 2 || handled.IsCompleted);
            write.TrySetException(new JSException("QuotaExceededError"));
            await x.H.NextFailureAsync();
            await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level"));
        }
        finally
        {
            read.TrySetResult(other);
            write.TrySetException(new JSException("QuotaExceededError"));
            reread.TrySetResult(other);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.Get<Level>().ShouldBe(new(7));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":7}""");

        // The retry dedupes against the baseline the restore set: nothing is written again.
        x.H.OnSet = static _ => true;
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));
        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        await WriterHarness.Until(() => !writer.Dirty);
        x.H.Written.Select(static w => w.Payload).ShouldBe(["""{"Value":3}""", """{"Value":7}"""]);
    }

    [Fact]
    public async Task CrossTab_ReceiptReadEqualToFailingOwnWriteInFlight_WaitsAndReadsAgain()
    {
        // §11.7: the re-read is sent after this store's own write {7} and returns another tab's {7} while that write is still
        // in flight. Equal to the write in flight, it is ambiguous until the write resolves: the receipt waits for it, then
        // reads again. The write fails (a quota error), so the third read finds {7} over the unchanged baseline {3}, and
        // restores it: the retry dedupes. Dropped as an echo of the write in flight, the baseline would stay {3}, and the
        // writer would retry {7} and log PersistenceFailed for good, although storage already holds {7}.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];

        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reread = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var answer = x.H.OnGet;
        x.H.OnGet = _ =>
        {
            x.H.OnGet = _ =>
            {
                x.H.OnGet = answer;
                return reread.Task;
            };
            return read.Task;
        };
        var write = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = x.Envelope(new Level(7));
        var handled = x.Notify("tab", "local", "ducky:level", other);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        try
        {
            await WriterHarness.Until(() => Reads(x) == 1);
            x.H.OnSet = _ => write.Task;
            x.H.Store.Dispatch(new SetLevel(7));
            (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":7}""");
            read.TrySetResult(other);
            await WriterHarness.Until(() => Reads(x) == 2);
            reread.TrySetResult(other);
            await WriterHarness.Until(() => x.EventIds(LogLevel.Debug).Contains(2056) || handled.IsCompleted);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBeEmpty();
            write.TrySetException(new JSException("QuotaExceededError"));
            await x.H.NextFailureAsync();
        }
        finally
        {
            read.TrySetResult(other);
            reread.TrySetResult(other);
            write.TrySetException(new JSException("QuotaExceededError"));
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(3);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.Get<Level>().ShouldBe(new(7));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":7}""");

        // The retry dedupes against the baseline the restore set: nothing is written again.
        x.H.OnSet = static _ => true;
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));
        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        await WriterHarness.Until(() => !writer.Dirty);
        x.H.Written.Select(static w => w.Payload).ShouldBe(["""{"Value":3}""", """{"Value":7}"""]);
    }

    [Fact]
    public async Task CrossTab_RemovalReceiptDuringOwnWriteOfInitialStateContent_Applied()
    {
        // (non-normative, §11.7) Only a value equal to the write in flight is its echo: an absence read after this store
        // sent {0}, the initial state's serialization, is another store's removal that landed after it, and takes the
        // removal branch (sequence bump, restore, baseline) although its serialization equals the write in flight.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(0));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":0}""");
        try
        {
            await x.ReceiveAsync("tab", "local", "ducky:level", null);
            Reads(x).ShouldBe(1);
            x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
            x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
            x.H.Middleware.InFlight.ContainsKey("ducky:level").ShouldBeFalse();
        }
        finally
        {
            ack.TrySetResult(true);
        }
    }

    [Fact]
    public async Task CrossTab_ReceiptDuringOwnWrite_ValueOfBaseline_Restored()
    {
        // §11.7, INV-18: this store's {4} is in flight (its setItem ran, its result not back) when another
        // tab writes {3} again, the value this store's baseline still names. Ambiguous until the write resolves, the receipt
        // waits for it, then reads again: delivered, the write moved the baseline to {4}, so the re-read {3} is a newer
        // value, not an echo, and is restored. Dropped as an echo, this store would stay on {4} while storage holds {3}.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task handled;
        try
        {
            handled = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(3)));
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => Reads(x) == 1);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBeEmpty();
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level") && !writer.Dirty);
        x.H.Store.State.Get<Level>().ShouldBe(new(3));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":3}""");
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":4}"""]);
    }

    [Fact]
    public async Task CrossTab_UnreadableValueDuringOwnWrite_Ignored()
    {
        // §11.7: a value the EnvelopeReader can't read (a newer version, Warning 2031) is ignored, also while this store's
        // own write is in flight: no restore, and that write's result alone moves the baseline.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        try
        {
            await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5), version: 2));
            x.Restores.Entries.ShouldBeEmpty();
            x.H.Store.State.Get<Level>().ShouldBe(new(4));
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level"));
        await x.H.Store.WhenIdleAsync(Ct);
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Store.State.Get<Level>().ShouldBe(new(4));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":4}""");
        x.EventIds(LogLevel.Warning).ShouldContain(2031);
    }

    [Fact]
    public async Task CrossTab_ReceiptDuringOwnWrite_ChangedBackBeforeItsResult_WrittenAgain()
    {
        // §11.5, §11.7, INV-18: this store's {4} is in flight when another tab's {5} lands after it, and is restored;
        // then the state goes back to {4} before the write's result arrives. That success must not set the baseline to {4}
        // over the receipt's {5} (what storage holds): the writer would dedupe the {4}, and this store would stay on {4}
        // while storage and the other tab hold {5}. It is written again.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        try
        {
            await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
            x.H.Store.State.Get<Level>().ShouldBe(new(5));
            x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":5}""");
            x.H.Store.Dispatch(new SetLevel(4));
            await x.H.Store.WhenIdleAsync(Ct);
        }
        finally
        {
            x.H.OnSet = static _ => true;
            ack.TrySetResult(true);
        }

        await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level") && !writer.Dirty);
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":4}""", """{"Value":4}"""]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":4}""");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task CrossTab_TooLargeKeyRemovedBeforePull_ResetsSlice(int length)
    {
        // ducky.js saw a value above InlinePayloadBytes, and the key was removed before .NET pulled it: storageGetStream
        // answers a one-byte Uint8Array (§11.9; zero bytes too, whatever the runtime makes of it), which takes the removal
        // branch. The reference is disposed, the slice reset, its baseline the initial state's, and nothing is written back.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        var gone = new FakeJsStreamReference(new byte[length]);
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) => identifier == "storageGetStream" ? gone : respond(identifier, args);
        await x.H.InitializeAsync();
        await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(9)));

        await x.ReceiveAsync("tab", "local", "ducky:level", null, tooLarge: true);

        x.H.Js.Calls.Where(static call => call.Identifier == "storageGetStream").ShouldHaveSingleItem().Args.ShouldBe(["local", "ducky:level"]);
        gone.Disposed.ShouldBeTrue();
        gone.Opened.ShouldBeFalse();
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level"), (Origin.CrossTab, "level")]);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        await x.H.IterationsAsync("level", 2);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Written.ShouldBeEmpty();
    }

    [Fact]
    public async Task CrossTab_StorageSetReportsUnknownId_ReWatchesAndReReads()
    {
        // ducky.js pruned this id (its deliveries failed past the prune cap) and the circuit came back: storageSet still
        // writes, then answers false. .NET re-issues watchStorage with the same six arguments and re-reads every key of each
        // registered area, which brings in what another store wrote meanwhile; its own write reads back as an echo.
        await using var x = new CrossTabHarness(static d => d
            .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true)
            .Persist<DialSlice>()
            .Persist<CounterSlice>(static o => o.Storage = PersistStorage.Session));
        await x.H.InitializeAsync();
        var id = (string)x.Watches.ShouldHaveSingleItem()[0]!;
        x.H.OnSet = write =>
        {
            x.H.Storage[(write.Area, write.Key)] = write.Value;
            return false;
        };
        x.H.Storage[("session", "ducky:counter")] = x.Envelope(new Counter(4));

        x.H.Store.Dispatch(new SetLevel(1));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await WriterHarness.Until(() => x.Watches.Count == 2);
        x.Watches[1].ShouldBe(x.Watches[0]);
        x.Watches[1].Length.ShouldBe(6);
        var repaired = x.Logs.GetSnapshot().Single(static record => record.Id.Id == 2055);
        repaired.Level.ShouldBe(LogLevel.Debug);
        repaired.Message.ShouldContain(id);
        await WriterHarness.Until(() => Debounces(x) == 3); // one re-read per registered area: one debounce per key it accepts
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => x.H.Store.State.Get<Counter>() == new Counter(4));
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Js.Calls.Where(static call => call.Identifier == "storageGet").Skip(3).Select(static call => ((string)call.Args[0]!, (string)call.Args[1]!))
            .ShouldBe([("local", "ducky:level"), ("local", "ducky:dial"), ("session", "ducky:counter")], ignoreOrder: true);
        Debounces(x).ShouldBe(3);
        x.H.Store.State.Get<Level>().ShouldBe(new(1));
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "counter")]);
        x.H.Written.ShouldHaveSingleItem();

        // storageRemove answers the same way: a clear re-registers and re-reads too. The clearing store keeps its state:
        // the keys it removed read back missing, and their baselines are absent.
        x.H.OnRemove = static _ => false;
        x.H.Store.Dispatch(new ClearPersistedState());
        await WriterHarness.Until(() => x.Watches.Count == 5); // one per removal
        await WriterHarness.Until(() => Debounces(x) == 12);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await WriterHarness.Until(() => x.H.Js.Calls.Count(static call => call.Identifier == "storageGet") == 9); // once per key
        await x.H.Store.WhenIdleAsync(Ct);
        (x.H.Store.State.Get<Level>(), x.H.Store.State.Get<Counter>()).ShouldBe((new(1), new(4)));
        x.Restores.Entries.ShouldHaveSingleItem();
        x.H.Js.Calls.Count(static call => call.Identifier == "storageGet").ShouldBe(9);

        // A write the dispose flush makes is answered false too, and re-registers nothing: the id is being removed.
        await using var flushed = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, TimeSpan.FromMinutes(1))));
        flushed.H.OnSet = static _ => false;
        await flushed.H.InitializeAsync();
        flushed.H.Store.Dispatch(new SetLevel(2));
        await flushed.H.Time.TimerAsync(TimeSpan.FromMinutes(1));
        await flushed.H.DisposeStoreAsync();
        flushed.H.Written.ShouldHaveSingleItem().Payload.ShouldBe("""{"Value":2}""");
        flushed.H.Js.Calls.Select(static call => call.Identifier).Where(static i => i != "import").ShouldBe(["watchStorage", "storageGet", "storageSet", "unwatchStorage"]);
        flushed.EventIds(LogLevel.Debug).ShouldNotContain(2055);
    }

    private static int Debounces(CrossTabHarness x) => x.H.Time.DueTimes.Count(static due => due == CrossTabHarness.Debounce);

    [Fact]
    public async Task CrossTab_UnknownIdRepair_StartsOnceBaselineRecordsTheCall_Deterministic()
    {
        // The repair's re-read compares what it reads with the baseline: started before the baseline records the call that
        // answered false, the store's own write would read as no echo, and its own removal as a removal to reset to. Each
        // watchStorage the repair issues records the baseline it sees then.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.OnSet = static _ => false;
        x.H.OnRemove = static _ => false;
        var seen = new ConcurrentQueue<string?>();
        var respond = x.H.Js.Respond;
        x.H.Js.Respond = (identifier, args) =>
        {
            if (identifier == "watchStorage")
            {
                seen.Enqueue(x.H.Middleware.LastKnownPayload.TryGetValue("ducky:level", out var baseline) ? baseline : null);
            }

            return respond(identifier, args);
        };

        // Awaited on seen, not on Watches: the fake records a call before Respond runs, so seen could still be one short.
        x.H.Store.Dispatch(new SetLevel(1));
        await WriterHarness.Until(() => seen.Count == 1);
        x.H.Store.Dispatch(new ClearPersistedState());
        await WriterHarness.Until(() => seen.Count == 2);

        seen.ShouldBe(["""{"Value":1}""", null]);
        x.Watches.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CrossTab_OldScopeEventDuringSwitch_Ignored()
    {
        // A Blazor Server store whose Local key is scoped by the signed-in user. Alice's value is accepted and still in its
        // debounce when the user changes to bob: acceptance is re-checked when the restore is issued, so it is dropped.
        var auth = new SwitchingAuth("alice");
        await using var x = new CrossTabHarness(
            static d => d
                .AddBlazor(static o => o.Scope = DuckyScopes.NameIdentifier)
                .Persist<LevelSlice>(static o => (o.SyncAcrossTabs, o.Debounce) = (true, TimeSpan.Zero)), // the switch read waits for no debounce
            browser: false,
            services: s => s.AddSingleton<AuthenticationStateProvider>(auth));
        await x.H.InitializeAsync();
        var pending = x.Notify("tab", "local", "ducky:alice:level", x.Envelope(new Level(5)), tooLarge: false);

        var bobResolved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        auth.Gate = bobResolved.Task;
        auth.Change("bob");
        x.H.Store.State.Get<PersistenceState>().ScopeEpoch.ShouldBe(1);
        x.H.Time.Advance(CrossTabHarness.Debounce);
        await pending.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.Restores.Entries.ShouldBeEmpty(); // the reset left the initial state unchanged
        x.Logs.GetSnapshot().Where(static record => record.Id.Id == 2050).ShouldHaveSingleItem().Message.ShouldContain("ducky:alice:level");

        // Between the epoch bump and the new scope's recording no scoped key is accepted, the new user's included.
        await x.ReceiveAsync("tab", "local", "ducky:alice:level", x.Envelope(new Level(6)));
        await x.ReceiveAsync("tab", "local", "ducky:bob:level", x.Envelope(new Level(7)));
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.EventIds(LogLevel.Debug).Count(static id => id == 2050).ShouldBe(3);

        // Once bob is recorded, his switch read restores what storage holds (the 7 another tab wrote meanwhile); another tab
        // still signed in as alice keeps writing her key: ignored; bob's key is accepted.
        bobResolved.SetResult();
        await WriterHarness.Until(() => x.H.Store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        x.H.Store.State.Get<Level>().ShouldBe(new(7));
        await x.ReceiveAsync("tab", "local", "ducky:alice:level", x.Envelope(new Level(8)));
        x.H.Store.State.Get<Level>().ShouldBe(new(7));
        x.EventIds(LogLevel.Debug).Count(static id => id == 2050).ShouldBe(4);
        await x.ReceiveAsync("tab", "local", "ducky:bob:level", x.Envelope(new Level(9)));
        x.H.Store.State.Get<Level>().ShouldBe(new(9));
        x.Restores.Entries.Last().ShouldBe((Origin.CrossTab, "level"));
    }

    // One hold of a writer at DeferPoint.AfterSnapshot: the hook completes Paused and waits for Resume.
    private sealed class Hold
    {
        public TaskCompletionSource Paused { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    [Fact]
    public async Task CrossTab_RemovalReceiptDuringOwnFirstWrite_Applied_TabsConverge()
    {
        // §11.7, INV-18: this store's first {4} is in flight (its setItem ran, its result not back, no baseline yet) when
        // another tab removes the key. Ambiguous until the write resolves, the receipt waits for it, then reads again:
        // delivered, the write set the baseline to {4}, so the removal landed after it, no local change is still waiting for
        // its first write, and the removal resets the slice. Kept as an echo, this store would stay on {4} while storage is empty.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        var writer = x.H.Middleware.Writers["level"];
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task handled;
        try
        {
            x.H.Middleware.LastKnownPayload.ContainsKey("ducky:level").ShouldBeFalse();
            handled = x.Notify("tab", "local", "ducky:level", null);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => Reads(x) == 1);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBeEmpty();
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);

        await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level") && !writer.Dirty && !writer.Signalled);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.H.Written.ShouldHaveSingleItem();
        x.H.Storage.ContainsKey(("local", "ducky:level")).ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTab_RemovalReceiptDuringOwnWriteOverRemovedBaseline_Applied_TabsConverge()
    {
        // §11.7, INV-18: as above, with the baseline an earlier removal receipt set (the key was absent) when {4} is sent.
        // Once the write delivered, that baseline is {4}: the re-read removal landed after it, and resets the slice.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        var writer = x.H.Middleware.Writers["level"];
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        await x.ReceiveAsync("tab", "local", "ducky:level", null);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        await WriterHarness.Until(() => !writer.Dirty && !writer.Signalled);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");

        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task handled;
        try
        {
            handled = x.Notify("tab", "local", "ducky:level", null);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => Reads(x) == 2);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2 + 1);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level"), (Origin.CrossTab, "level")]);

        await WriterHarness.Until(() => !x.H.Middleware.InFlight.ContainsKey("ducky:level") && !writer.Dirty && !writer.Signalled);
        await x.H.Store.WhenIdleAsync(Ct);
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":0}""");
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":4}"""]);
        x.H.Storage.ContainsKey(("local", "ducky:level")).ShouldBeFalse();
    }

    [Fact]
    public async Task CrossTab_ReceiptOfBaselineDuringOwnWrite_WriteFails_LocalChangeKept()
    {
        // §11.7, INV-16, INV-18: this store's {4} is in flight over the baseline {3} when a receipt reads {3}. That read is
        // ambiguous while the call is unresolved: another store's newer {3} if {4} landed, storage's old {3} if it never does
        // (an interop timeout on a dropped circuit). The receipt waits for the call, then reads again: the write failed, so
        // the re-read is an echo of the baseline, the local change is kept, and the writer's retry writes it.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task handled;
        try
        {
            handled = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(3)));
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => Reads(x) == 1);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBeEmpty();
            x.H.OnSet = static _ => true;
        }
        finally
        {
            ack.TrySetException(new TaskCanceledException("interop timeout"));
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.EventIds(LogLevel.Debug).Count(static id => id == 2056).ShouldBe(1);
        x.EventIds(LogLevel.Debug).ShouldNotContain(2054); // a value echo is dropped before the removal's checks
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Store.State.Get<Level>().ShouldBe(new(4));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":3}""");
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));
        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        await WriterHarness.Until(() => x.H.Middleware.LastKnownPayload["ducky:level"] == """{"Value":4}""");
    }

    [Fact]
    public async Task CrossTab_ResyncDuringOwnFirstWrite_WriteFails_LocalChangeKept()
    {
        // §11.7, INV-16, INV-18: this store's first {4} is in flight (no baseline) when a resync reads the key absent. While
        // the call is unresolved the absence is ambiguous: another store's removal if {4} landed, the untouched key if it
        // never does. The receipt waits for the call, then reads again: the write failed, so the absence is kept as an echo
        // (no local change waiting for its first write is discarded), and the writer's retry writes {4}.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task handled;
        try
        {
            handled = x.Notify("tab", "local", null, null);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => Reads(x) == 1);
            handled.IsCompleted.ShouldBeFalse();
            x.Restores.Entries.ShouldBeEmpty();
            x.H.OnSet = static _ => true;
        }
        finally
        {
            ack.TrySetException(new TaskCanceledException("interop timeout"));
        }

        await handled.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Reads(x).ShouldBe(2);
        x.EventIds(LogLevel.Debug).Count(static id => id == 2056).ShouldBe(1);
        x.Restores.Entries.ShouldBeEmpty();
        x.H.Store.State.Get<Level>().ShouldBe(new(4));
        x.H.Middleware.LastKnownPayload.ContainsKey("ducky:level").ShouldBeFalse();
        await x.H.Time.TimerAsync(TimeSpan.FromSeconds(1));
        x.H.Time.Advance(TimeSpan.FromSeconds(1));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        await WriterHarness.Until(() => x.H.Middleware.LastKnownPayload.GetValueOrDefault("ducky:level") == """{"Value":4}""");
    }

    [Fact]
    public async Task CrossTab_WaitingReceiptSuperseded_EndsWithoutReading()
    {
        // §11.7: a receipt waiting for this store's {4} to resolve is superseded by a newer notification of the key, which
        // reads the other tab's {5} (no echo: neither the write in flight nor the baseline) and restores it. Once {4}
        // resolves, the older receipt ends without reading again, and {4}'s success keeps the baseline the receipt set.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var writer = x.H.Middleware.Writers["level"];
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task waiting;
        try
        {
            waiting = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(3)));
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => x.EventIds(LogLevel.Debug).Contains(2056));
            await x.ReceiveAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)));
            Reads(x).ShouldBe(2);
            x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
            x.H.Store.State.Get<Level>().ShouldBe(new(5));
            waiting.IsCompleted.ShouldBeFalse();
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => x.H.Middleware.Resolved["ducky:level"].Task.IsCompleted && !writer.Dirty && !writer.Signalled);
        await x.H.Store.WhenIdleAsync(Ct);
        Reads(x).ShouldBe(2);
        x.Restores.Entries.ShouldBe([(Origin.CrossTab, "level")]);
        x.H.Store.State.Get<Level>().ShouldBe(new(5));
        x.H.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":5}""");
        x.H.Written.Select(static write => write.Payload).ShouldBe(["""{"Value":3}""", """{"Value":4}"""]);
    }

    [Fact]
    public async Task CrossTab_WaitingReceipt_EndsOnDispose()
    {
        // (non-normative, §11.7) A receipt waiting for this store's {4} to resolve ends when the store is disposed, before
        // that write's result arrives (the dispose stops cross-tab receipts before its flush): it neither reads nor restores.
        await using var x = new CrossTabHarness(static d => d.Persist<LevelSlice>(static o => o.SyncAcrossTabs = true));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetLevel(3));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await x.H.IterationsAsync("level", 1);
        var ack = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        x.H.OnSet = _ => ack.Task;
        x.H.Store.Dispatch(new SetLevel(4));
        (await x.H.NextWriteAsync()).Payload.ShouldBe("""{"Value":4}""");
        Task disposed;
        try
        {
            var waiting = x.Notify("tab", "local", "ducky:level", x.Envelope(new Level(3)));
            x.H.Time.Advance(CrossTabHarness.Debounce);
            await WriterHarness.Until(() => x.EventIds(LogLevel.Debug).Contains(2056));
            disposed = x.H.DisposeStoreAsync();
            await waiting.WaitAsync(TimeSpan.FromSeconds(10), Ct);
            ack.Task.IsCompleted.ShouldBeFalse();
            Reads(x).ShouldBe(1);
            x.Restores.Entries.ShouldBeEmpty();
        }
        finally
        {
            ack.TrySetResult(true);
        }

        await disposed.WaitAsync(TimeSpan.FromSeconds(10), Ct);
    }

    [Fact]
    public async Task CrossTab_ServerWriteBlockingSynchronously_ReceiptOfBrowserKeyProceeds()
    {
        // (non-normative, §11.7) A Server key gets no receipt, so its provider call is not ordered with receipts' reads: a
        // cache write that blocks in its synchronous part (sync-over-async user code) never holds up a receipt of a synced
        // browser key, which runs under _issue. The cache waits (at most 30 s) for that receipt to restore {2}.
        using var receipted = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        var released = false;
        var cache = new TestCache(yield: false)
        {
            SetFault = () =>
            {
                entered.Set();
                released = receipted.Wait(TimeSpan.FromSeconds(30));
                return null;
            },
        };
        await using var x = new CrossTabHarness(
            d => d
                .AddBlazor(static o => (o.KeyPrefix, o.Scope) = ("app", static (_, _) => ValueTask.FromResult<string?>("alice")))
                .Persist<LevelSlice>(static o => o.SyncAcrossTabs = true).Persist<DialSlice>(static o => o.Storage = PersistStorage.Server),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(cache));
        await x.H.InitializeAsync();
        x.H.Store.Dispatch(new SetDial(1));
        await WriterHarness.Until(() => entered.IsSet);

        await x.ReceiveAsync("tab", "local", "app:alice:level", x.Envelope(new Level(2)));
        x.H.Store.State.Get<Level>().ShouldBe(new(2));
        receipted.Set();
        await WriterHarness.Until(() => cache.Stored("app:alice:dial") is not null);
        released.ShouldBeTrue();
    }

    // The storageGet calls this store made for the level key (its hydration read excluded: it ran before the test's writes).
    private static int Reads(CrossTabHarness x) => x.H.Js.Calls.Count(static call => call.Identifier == "storageGet" && (string?)call.Args[1] == "ducky:level") - 1;
}

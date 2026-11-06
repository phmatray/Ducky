using System.Collections.Concurrent;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;
using Ducky.Blazor.Tests.Fakes;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
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
            var removal = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", null, tooLarge: false);
            var value = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:dial", x.Envelope(new Dial(5)), tooLarge: false);
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
            var receipt = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
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
            var older = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
            x.H.Time.Advance(CrossTabHarness.Debounce);
            var newer = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(6)), tooLarge: false);
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
            var receipt = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
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
            var receipt = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
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
            var receipt = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", x.Envelope(new Level(5)), tooLarge: false);
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
            var removal = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:level", null, tooLarge: false);
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
        x.H.OnGet = _ => read.Task;
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
        x.H.OnGet = _ => read.Task;
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

        x.H.Store.Dispatch(new SetLevel(1));
        await WriterHarness.Until(() => x.Watches.Count == 2);
        x.H.Store.Dispatch(new ClearPersistedState());
        await WriterHarness.Until(() => x.Watches.Count == 3);

        seen.ShouldBe(["""{"Value":1}""", null]);
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
        var pending = x.Sync.OnStorageChangedAsync("tab", "local", "ducky:alice:level", x.Envelope(new Level(5)), tooLarge: false);

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

        // Once bob is recorded, another tab still signed in as alice keeps writing her key: ignored; bob's key is accepted.
        bobResolved.SetResult();
        await WriterHarness.Until(() => x.H.Store.State.Get<PersistenceState>() is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await x.ReceiveAsync("tab", "local", "ducky:alice:level", x.Envelope(new Level(8)));
        x.H.Store.State.Get<Level>().ShouldBe(new(0));
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
}

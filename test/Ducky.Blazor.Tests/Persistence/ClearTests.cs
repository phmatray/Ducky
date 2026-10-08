using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 "ClearPersistedState" and "A command ends the backoff sleep", INV-16: the clear posts a removal to each
// persisted key's writer, keyed from the clear's snapshot epoch; the loop runs it after any in-flight write, discards the
// signals raised before the clear, removes the exact key and resets the baseline to absent. Storage only.
public sealed class ClearTests
{
    [Fact]
    public async Task Write_AfterClear_SameContent_IsWritten()
    {
        // The clear resets the baseline to absent: a change that serializes to the content written before the clear (here
        // 1 → 2 → 1 inside one debounce) is written, not deduped away. The clear leaves the store's own state alone.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromSeconds(1)));
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await h.IterationsAsync("level", 1);

        h.Store.Dispatch(new ClearPersistedState());
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);
        h.Middleware.LastKnownPayload.ShouldBeEmpty();
        h.Store.State.Get<Level>().ShouldBe(new Level(1));

        h.Store.Dispatch(new SetLevel(2));
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        await h.IterationsAsync("level", 3);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":1}""");
    }

    [Fact]
    public async Task Clear_RemovesOnlyPersistedKeysOfThisScope()
    {
        // Each persisted slice's exact {prefix}:{scope}:{sliceKey}, in its own area, under the store's id: another scope's
        // key, a foreign key and an unpersisted slice's key stay, and nothing clears storage wholesale.
        await using var h = new WriterHarness(
            static d => d
                .Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero)
                .Persist<DialSlice>(static o => (o.Storage, o.Debounce) = (PersistStorage.Session, TimeSpan.Zero))
                .AddBlazor(static o => o.Scope = static (_, _) => new("alice")),
            browser: false);
        (string, string)[] others = [("local", "ducky:bob:level"), ("local", "other"), ("local", "ducky:alice:counter"), ("session", "ducky:alice:level")];
        foreach (var key in others)
        {
            h.Storage[key] = "x";
        }

        h.Storage[("local", "ducky:alice:level")] = "x";
        h.Storage[("session", "ducky:alice:dial")] = "x";
        await h.InitializeAsync();

        h.Store.Dispatch(new ClearPersistedState());
        var removals = new[] { await h.NextRemovalAsync(), await h.NextRemovalAsync() };
        await h.IterationsAsync("level", 1);
        await h.IterationsAsync("dial", 1);

        removals.Select(static removal => (removal.Area, removal.Key)).ShouldBe([("local", "ducky:alice:level"), ("session", "ducky:alice:dial")], ignoreOrder: true);
        removals.Select(static removal => removal.Id).Distinct().ShouldHaveSingleItem().Length.ShouldBe(32);
        h.Storage.Keys.ShouldBe(others, ignoreOrder: true);
        h.Js.Calls.Select(static call => call.Identifier).Distinct().ShouldBe(["import", "storageGet", "storageRemove"], ignoreOrder: true);
        h.Written.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clear_DuringInFlightWrite_KeyStaysRemoved()
    {
        // The removal runs in the key's writer after the write in flight when the clear was reduced, never beside it, and
        // that write's signal was raised before the clear: nothing writes the key back.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnSet = _ => held.Task;
        var heldDoneAtRemove = new ConcurrentQueue<bool>();
        h.OnRemove = _ =>
        {
            heldDoneAtRemove.Enqueue(held.Task.IsCompleted);
            return true;
        };
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();

        h.Store.Dispatch(new ClearPersistedState());
        h.Js.Calls.ShouldNotContain(static call => call.Identifier == "storageRemove");
        held.SetResult(true);

        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);
        heldDoneAtRemove.ToArray().ShouldBe([true]);
        h.Log.Entries.Where(static entry => entry.StartsWith("write:", StringComparison.Ordinal) || entry.StartsWith("remove:", StringComparison.Ordinal))
            .ShouldBe(["""write:ducky:level:{"Value":1}""", "remove:ducky:level"]);
        h.Middleware.LastKnownPayload.ShouldBeEmpty();
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();
        h.Middleware.Writers["level"].Signalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Clear_WithPendingDebouncedChange_KeyStaysRemoved()
    {
        // A change still in its debounce when the clear is reduced is discarded with the clear: the loop removes the key,
        // and neither that iteration nor the one the clear's own wake-up runs writes it back.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromSeconds(1)));
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.NextWriteAsync();
        await h.IterationsAsync("level", 1);

        h.Store.Dispatch(new SetLevel(2));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Store.Dispatch(new ClearPersistedState());
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);

        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.IterationsAsync("level", 3);
        h.Written.Count.ShouldBe(1);
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();
        h.Store.State.Get<Level>().ShouldBe(new Level(2));
    }

    [Fact]
    public async Task Clear_WhileKeyDeferredDuringHydration_KeyStaysRemoved()
    {
        // (non-normative) A change met the hydration skip and waits in _deferred when the clear is reduced: the deferral is
        // a signal raised before the clear, so the terminal's release drops it instead of writing that change back.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnGet = _ => read.Task;
        var init = h.InitializeAsync();
        h.Line.Send!(new SetLevel(4), false);
        await h.IterationsAsync("level", 1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);

        h.Line.Send!(new ClearPersistedState(), false);
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);

        read.SetResult(null);
        await init;
        await AssertStaysRemovedAsync(h);
    }

    [Fact]
    public async Task Clear_WhileKeyDeferredBeforeFirstRead_KeyStaysRemoved()
    {
        // (non-normative) The same for a change reduced before persistence's synchronous prefix, which only marks its key
        // deferred, without signalling its writer.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero), early: static d => d.Use<EarlyChange>());
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnGet = _ => read.Task;
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => h.Log.Entries.Contains("restore:@ducky/persistence Hydrating:0"));
        h.Middleware.Deferred.Keys.ShouldContain("level");

        h.Line.Send!(new ClearPersistedState(), false);
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 1);

        read.SetResult(null);
        await init;
        await AssertStaysRemovedAsync(h);
    }

    [Fact]
    public async Task Clear_BeforeHydratingRestoreReduced_LaterChangeStillWritten()
    {
        // (non-normative) Init starts from inside a change's drain, behind a clear and a second change already queued: both
        // are reduced once the writer exists but before the Hydrating restore, so each only marks the key deferred. The
        // clear discards the first change's deferral, not the second's: after the read, the later change is written.
        Task? init = null;
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnGet = _ => read.Task;
        h.Log.OnEntry = entry =>
        {
            if (entry.StartsWith(typeof(SetLevel).FullName!, StringComparison.Ordinal) && init is null)
            {
                h.Line.Send!(new ClearPersistedState(), false);
                h.Line.Send!(new SetLevel(3), false);
                init = h.InitializeAsync();
            }
        };

        // A restore materializes the store's middleware without starting init (§5.2), so SystemSender's line is connected.
        h.Store.Restore(new Dictionary<string, object>(), Origin.DevTools);
        h.Line.Send!(new SetLevel(2), false);
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);
        h.Written.ShouldBeEmpty();

        read.SetResult(null);
        await init.ShouldNotBeNull();
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        h.Log.Entries.Where(static entry => entry.StartsWith("write:", StringComparison.Ordinal) || entry.StartsWith("remove:", StringComparison.Ordinal))
            .ShouldBe(["remove:ducky:level", """write:ducky:level:{"Value":3}"""]);
    }

    [Fact]
    public async Task Clear_WithDeferredChangeAndDebouncedRemoval_KeyStaysRemoved()
    {
        // (non-normative) The terminal comes while the removal still waits out its debounce: the release drops the deferral
        // made before the clear, so the removal, once it runs, leaves nothing to write.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromSeconds(1)));
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnGet = _ => read.Task;
        var init = h.InitializeAsync();
        h.Line.Send!(new SetLevel(1), false);
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.IterationsAsync("level", 1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);

        h.Line.Send!(new ClearPersistedState(), false);
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        read.SetResult(null);
        await init;
        h.Middleware.Deferred.ShouldBeEmpty();
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);
        await AssertStaysRemovedAsync(h);
    }

    [Fact]
    public async Task Dispose_RemovalFailsOnLastAttempt_CountedLost()
    {
        // (non-normative) A removal that still fails on the flush's last attempt leaves the old value in storage: the key is
        // counted ducky.persistence.lost although no change is dirty.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        h.OnRemove = static _ => throw new JSException("SecurityError");
        await h.InitializeAsync();
        h.Store.Dispatch(new ClearPersistedState());
        await h.NextFailureAsync();
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();

        await h.DisposeStoreAsync().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        h.Js.Calls.Count(static call => call.Identifier == "storageRemove").ShouldBe(2);
        lost.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    [Fact]
    public async Task Clear_WriterInRetryBackoff_RemovedImmediately()
    {
        // Writes keep failing until the backoff reaches 30 s. The clear ends that sleep: the removal runs at once, without
        // the clock moving, and the failing change, signalled before the clear, is discarded rather than retried.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        h.OnSet = static _ => Task.FromException<object?>(new JSDisconnectedException("circuit gone"));
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        foreach (var seconds in new[] { 1, 2, 4, 8, 16 })
        {
            await h.Time.TimerAsync(TimeSpan.FromSeconds(seconds));
            h.Time.Advance(TimeSpan.FromSeconds(seconds));
        }

        await h.Time.TimerAsync(TimeSpan.FromSeconds(30));
        var at = h.Time.GetUtcNow();
        h.Store.Dispatch(new ClearPersistedState());

        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 2);
        h.Time.GetUtcNow().ShouldBe(at);
        h.Written.Count.ShouldBe(6);
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Clear_RemovalFailsInBackoff_BackoffResumesAtItsStepAndStreakResets()
    {
        // (non-normative) The clear ends a 4 s backoff sleep and its removal fails (reported: a provider failure starts a
        // streak). The backoff resumes at the step it had reached, 4 s, neither doubled nor reset; the retried removal
        // succeeds and ends the streak, so the next provider failure is reported again.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        h.OnSet = static _ => Task.FromException<object?>(new JSDisconnectedException("circuit gone"));
        var removals = 0;
        h.OnRemove = _ => ++removals == 1 ? throw new JSException("SecurityError") : true;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        foreach (var seconds in new[] { 1, 2 })
        {
            await h.Time.TimerAsync(TimeSpan.FromSeconds(seconds));
            h.Time.Advance(TimeSpan.FromSeconds(seconds));
        }

        await h.Time.TimerAsync(TimeSpan.FromSeconds(4));
        h.Store.Dispatch(new ClearPersistedState());
        await h.NextRemovalAsync();
        var failure = await h.NextFailureAsync();
        (failure.SliceKey, failure.ErrorType).ShouldBe(("level", nameof(JSException)));

        await h.Time.TimerAsync(TimeSpan.FromSeconds(4));
        h.Time.Advance(TimeSpan.FromSeconds(4));
        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 1);
        h.Written.Count.ShouldBe(3);

        h.OnSet = static _ => throw new JSException("QuotaExceededError");
        h.Store.Dispatch(new SetLevel(2));
        (await h.NextFailureAsync()).ErrorType.ShouldBe(nameof(JSException));
    }

    [Fact]
    public async Task Clear_DuringWriteThatFails_RemovedWithoutBackoff()
    {
        // (non-normative) A clear posted while a write is in flight, and that write fails: the removal is already pending,
        // so no backoff sleep begins; the removal runs at once and discards the failed change.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnSet = _ => held.Task;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync();

        h.Store.Dispatch(new ClearPersistedState());
        held.SetException(new JSDisconnectedException("circuit gone"));

        (await h.NextRemovalAsync()).Key.ShouldBe("ducky:level");
        await h.IterationsAsync("level", 1);
        h.Written.Count.ShouldBe(1);
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();
    }

    [Fact]
    public async Task Clear_ScopeNullOrUnknown_SkippedWithDebugLog()
    {
        // (non-normative) A scoped key whose epoch scope is null, or still unknown after a timeout terminal, can't be named:
        // no removal, a Debug log (EventId 2040) naming the slice. A scope recorded later doesn't revive the clear.
        foreach (var unknown in new[] { false, true })
        {
            var logs = new FakeLogCollector();
            var scope = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!unknown)
            {
                scope.SetResult(null);
            }

            await using var h = new WriterHarness(
                d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(o => o.Scope = (_, _) => new(scope.Task)),
                browser: false,
                services: s => s.AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(logs)).SetMinimumLevel(LogLevel.Debug)));
            var init = h.InitializeAsync();
            if (unknown)
            {
                await h.Time.TimerAsync(TimeSpan.FromSeconds(5));
                h.Time.Advance(TimeSpan.FromSeconds(5));
            }

            await init;
            h.Store.Dispatch(new ClearPersistedState());
            await WriterHarness.Until(() => logs.GetSnapshot().Any(static record => record.Id.Id == 2040));
            var skipped = logs.GetSnapshot().Where(static record => record.Id.Id == 2040).ShouldHaveSingleItem();
            skipped.Level.ShouldBe(LogLevel.Debug);
            skipped.Message.ShouldContain("'level'");

            scope.TrySetResult("alice");
            await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(0));
            h.Middleware.Writers["level"].Signalled.ShouldBeFalse();
            h.Middleware.Writers["level"].Iterations.ShouldBe(0);
            h.Js.Calls.ShouldNotContain(static call => call.Identifier == "storageRemove");
        }
    }

    // After the terminal: the deferral made before the clear was dropped, not signalled, and nothing was written.
    private static async Task AssertStaysRemovedAsync(WriterHarness h)
    {
        await WriterHarness.Until(() => h.Store.State.Get<PersistenceState>().Status != PersistenceStatus.Hydrating);
        h.Middleware.Deferred.ShouldBeEmpty();
        h.Middleware.Writers["level"].Signalled.ShouldBeFalse();
        h.Middleware.Writers["level"].Dirty.ShouldBeFalse();
        h.Written.ShouldBeEmpty();
        h.Storage.Keys.ShouldNotContain(("local", "ducky:level"));
    }
}

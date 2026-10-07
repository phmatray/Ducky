using System.Collections.Concurrent;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 "A skipped key stays dirty", INV-16: one predicate ShouldSkip(snap, key) for the skip and its recheck
// (add-then-recheck), the two releases of _deferred (a terminal, a scope recording) and the AfterDeferHook twins of the
// Ducky.Concurrency.Tests race Write_SkipRacesTerminal_DeferredKeyStillWritten.
public sealed class WriterDeferTests
{
    [Fact]
    public async Task Write_TerminalBetweenReadAndDefer_WriterRechecksAndResignals_Deterministic()
    {
        // The terminal commits after the writer's snapshot read and before its add: AfterReduce finds nothing to take, and
        // the writer's recheck removes its own key and re-signals itself, so the key is written once, never stranded.
        await TerminalAtAsync(DeferPoint.BeforeAdd);
    }

    [Fact]
    public async Task Write_TerminalAfterDeferAdd_AfterReduceSignalsOnce_Deterministic()
    {
        // The terminal commits between the add and the recheck: AfterReduce takes the key and signals it, and the writer's
        // own TryRemove fails, so it does not signal again: one write.
        await TerminalAtAsync(DeferPoint.AfterAdd);
    }

    [Fact]
    public async Task Write_TerminalBeforeScopeResolved_NoSpin_WrittenOnceScopeRecorded()
    {
        // A timeout terminal before the scope resolved: a scoped change is skipped for its unknown epoch scope (case (b))
        // and its recheck holds too, so the key waits in _deferred without spinning; recording the scope signals it, and
        // it is written once, under that scope.
        var scope = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var h = new WriterHarness(
            d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(o => o.Scope = (_, _) => new(scope.Task)),
            browser: false);
        var init = h.InitializeAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5));
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init;
        h.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);

        h.Store.Dispatch(new SetLevel(1));
        await h.IterationsAsync("level", 1);
        await NoSpinAsync(h);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);
        h.Written.ShouldBeEmpty();

        scope.SetResult("bob");
        var write = await h.NextWriteAsync();
        (write.Key, write.Payload).ShouldBe(("ducky:bob:level", """{"Value":1}"""));
        await h.IterationsAsync("level", 2);
        h.Middleware.Writers["level"].Iterations.ShouldBe(2);
        h.Written.Count.ShouldBe(1);
        h.Middleware.Deferred.ShouldBeEmpty();
    }

    [Fact]
    public async Task Write_ScopeRecordedBeforeTerminal_TerminalReleasesTheKey()
    {
        // (non-normative) A scope recorded while its attempt is still Hydrating releases nothing (the attempt's own terminal
        // will): a change deferred during the attempt is written once, after the terminal, under that scope.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var h = new WriterHarness(
            static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.Scope = static (_, _) => new("alice")),
            browser: false);
        h.OnGet = _ => read.Task;
        var init = h.InitializeAsync();
        h.Line.Send!(new SetLevel(4), false);
        await h.IterationsAsync("level", 1);
        h.Middleware.Scopes.ShouldContainKey(0);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);

        read.SetResult(null);
        await init;
        var write = await h.NextWriteAsync();
        (write.Key, write.Payload).ShouldBe(("ducky:alice:level", """{"Value":4}"""));
        await h.IterationsAsync("level", 2);
        h.Written.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Write_ChangeBeforeFirstRead_ScopeResolvedSynchronously_NotWrittenBeforeRead()
    {
        // A change reduced before persistence's synchronous prefix waits in _deferred. Init starts from inside that change's
        // drain (a State read or InitializeAsync from a hook does that), so the prefix's Hydrating restore is queued behind
        // the drain, not reduced, when the scope, resolved synchronously inside the prefix, is recorded: Store.State still
        // shows the initial Hydrated status. That recording, before the attempt's terminal, releases nothing; a release would
        // signal the writer, which would find ShouldSkip false and write in-memory state over the stored data before the
        // read. The key is written once, after the read.
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var readDoneAtWrite = new ConcurrentQueue<bool>();
        Task? init = null;
        (PersistenceStatus Status, string? Scope, string Deferred, int Written)? atRecording = null;
        await using var h = new WriterHarness(
            static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.Scope = static (_, _) => new("alice")),
            browser: false);
        h.OnGet = _ => read.Task;
        h.OnSet = _ =>
        {
            readDoneAtWrite.Enqueue(read.Task.IsCompleted);
            return true;
        };

        // On the drain, after persistence's AfterReduce deferred the key. Every synchronous init part has run when
        // InitializeAsync returns (§6.7), the scope recording included; the drain is still this one, so nothing after
        // SetLevel is reduced yet. Captured here, asserted below (a hook's throw is only logged).
        h.Log.OnEntry = entry =>
        {
            if (entry.StartsWith(typeof(SetLevel).FullName!, StringComparison.Ordinal))
            {
                init = h.InitializeAsync();
                atRecording = (h.Store.State.Get<PersistenceState>().Status, h.Middleware.Scopes.GetValueOrDefault(0), string.Join(",", h.Middleware.Deferred.Keys), h.Written.Count);
            }
        };

        // A restore materializes the store's middleware without starting init (§5.2), so SystemSender's line is connected.
        h.Store.Restore(new Dictionary<string, object>(), Origin.DevTools);
        h.Line.Send!(new SetLevel(2), false);
        atRecording.ShouldBe((PersistenceStatus.Hydrated, "alice", "level", 0));

        h.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Hydrating);
        read.SetResult(null);
        await init.ShouldNotBeNull();
        var write = await h.NextWriteAsync();
        (write.Key, write.Payload).ShouldBe(("ducky:alice:level", """{"Value":2}"""));
        readDoneAtWrite.ToArray().ShouldBe([true]);
    }

    [Fact]
    public async Task Wasm_ScopeWithoutHandOff_ScopedChange_NoWriterSpin()
    {
        // A browser Scope app with no Ducky component never hands its renderer's services over: the scope never resolves,
        // hydration ends at HydrationTimeout, and a scoped change stays deferred, with no write and no spin on WASM's only
        // thread (default debounce 0).
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>().AddBlazor(static o => o.Scope = static (_, _) => new("alice")));
        var init = h.InitializeAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5));
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init;
        h.Store.State.Get<PersistenceState>().Status.ShouldBe(PersistenceStatus.Failed);

        h.Store.Dispatch(new SetLevel(1));
        await h.IterationsAsync("level", 1);
        await NoSpinAsync(h);
        h.Middleware.Writers["level"].Iterations.ShouldBe(1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);
        h.Written.ShouldBeEmpty();
        h.Js.Calls.ShouldNotContain(static call => call.Identifier == "storageSet");
    }

    // A spinning writer re-signals itself at the end of each iteration and runs the next at once on another thread: after
    // many yields of this one, the loop has ended no further iteration and has no signal waiting.
    private static async Task NoSpinAsync(WriterHarness h)
    {
        var writer = h.Middleware.Writers["level"];
        var iterations = writer.Iterations;
        for (var i = 0; i < 200; i++)
        {
            await Task.Yield();
        }

        writer.Iterations.ShouldBe(iterations);
        writer.Signalled.ShouldBeFalse();
    }

    // A change during the attempt is skipped; at `at`, inside the writer's deferral, the hook completes the read and waits
    // until the terminal has been reduced (and AfterReduce has run on it), then lets the writer go on. Exactly one write.
    private static async Task TerminalAtAsync(DeferPoint at)
    {
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var terminal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        List<(string Key, DeferPoint Point)> points = []; // the hook runs on the writer's loop only, one call at a time
        await using var h = new WriterHarness(d => d.Persist<LevelSlice>().AddBlazor(o => o.AfterDeferHook = (key, point) =>
        {
            points.Add((key, point));
            if (point == at && read.TrySetResult(null))
            {
                // The writer's own pool thread blocks; the terminal is reduced by the thread that issues it.
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded wait on another thread's progress
                terminal.Task.Wait(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken).ShouldBeTrue();
#pragma warning restore VSTHRD002
            }
        }));
        h.OnGet = _ => read.Task;
        h.Log.OnEntry = entry =>
        {
            if (entry.StartsWith("completed:", StringComparison.Ordinal))
            {
                terminal.TrySetResult();
            }
        };
        var init = h.InitializeAsync();

        h.Line.Send!(new SetLevel(7), false);
        await init.WaitAsync(TimeSpan.FromSeconds(10), Xunit.TestContext.Current.CancellationToken);
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":7}""");
        await h.IterationsAsync("level", 2);
        h.Written.Count.ShouldBe(1);
        h.Middleware.Deferred.ShouldBeEmpty();
        points.ShouldBe([("level", DeferPoint.BeforeAdd), ("level", DeferPoint.AfterAdd)]);
    }
}

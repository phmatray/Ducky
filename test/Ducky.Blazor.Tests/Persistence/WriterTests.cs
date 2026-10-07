using System.Collections.Concurrent;
using CsCheck;
using Ducky.Blazor.Tests.Core;
using Ducky.TestSupport;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Tests.Persistence;

// SPEC §11.5 "Writes (PersistenceWriter, one per key)", INV-16: AfterReduce signals the changed keys of every origin but
// Hydration/CrossTab/DevTools; one loop per key (wake-up channel, debounce on TimeProvider, one snapshot read, hydration
// skip into _deferred, key from the snapshot's epoch, declared-type serialization, dedupe against LastKnownPayload, write,
// baseline on success); PersistenceFailed once per failure streak.
public sealed class WriterTests
{
    private static readonly Origin[] _restores = [Origin.Hydration, Origin.CrossTab, Origin.DevTools];

    private static readonly Gen<(int Kind, int Value)[]> _scripts = Gen.Select(Gen.Int[0, 3], Gen.Int[0, 5]).Array[0, 12];

    private static readonly Gen<(int Origin, int Key, int Value)[]> _restoreScripts = Gen.Select(Gen.Int[0, 3], Gen.Int[0, 1], Gen.Int[0, 9]).Array[0, 10];

    [Fact]
    public async Task Writes_NeverOverlapPerKey()
    {
        // Property: whatever the order of changes and write completions, a key's writer never issues a storageSet while
        // its previous one is still in flight.
        foreach (var script in Cases(_scripts))
        {
            var run = await RunAsync(script);
            run.MaxInFlight.ShouldBeLessThanOrEqualTo(1, Describe(script));
        }
    }

    [Fact]
    public async Task LastWrite_EqualsFinalState()
    {
        // Property: once every write has completed, the last write of each key holds the final committed state (each write
        // reads the state current when it runs, never the state of the change that signalled it).
        foreach (var script in Cases(_scripts))
        {
            var run = await RunAsync(script);
            run.Stored.ShouldBe(run.Expected, ignoreOrder: true, Describe(script));
        }
    }

    [Fact]
    public async Task NoWrite_ForHydrationCrossTabDevToolsOrigins()
    {
        // Every excluded origin, then a local change: deterministic coverage of each branch the property reaches.
        await NoWriteCaseAsync([(0, 0, 1), (1, 1, 2), (2, 0, 3)]);
        await NoWriteCaseAsync([(3, 0, 4), (0, 1, 5), (2, 1, 6)]);

        // Property: a Hydration, CrossTab or DevTools restore never signals a writer; a local change always does.
        foreach (var script in Cases(_restoreScripts))
        {
            await NoWriteCaseAsync(script);
        }
    }

    [Fact]
    public async Task PersistenceFailed_OncePerStreak()
    {
        // The provider fails twice (one streak: the write and its retry), succeeds, then fails again (a new streak).
        await using var h = new WriterHarness(static d => d.AddSlice<CrashOnPersistenceFailedSlice>().AddSlice<FailureSlice>().Persist<LevelSlice>());
        var calls = 0;
        h.OnSet = _ => ++calls is 3 or 5 ? true : throw new JSException("QuotaExceededError: the quota has been exceeded.");
        await h.InitializeAsync();

        // Call 1 fails: the streak starts and is reported once, with the slice key, the error type and its message, and the
        // key keeps no baseline.
        h.Store.Dispatch(new SetLevel(1));
        (await h.NextFailureAsync()).ShouldBe(new PersistenceFailed("level", nameof(JSException), "QuotaExceededError: the quota has been exceeded."));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
        h.Middleware.LastKnownPayload.ShouldBeEmpty();

        // Its retry (call 2) writes the state current then and fails in the same streak: not reported again. Call 3, the
        // next retry, succeeds and ends the streak (baseline set).
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Store.Dispatch(new SetLevel(2));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2));
        h.Time.Advance(TimeSpan.FromSeconds(2));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        await h.IterationsAsync("level", 2);
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":2}""");
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();

        // Call 4 fails: a new streak, reported again, and the baseline stays the last successful write.
        h.Store.Dispatch(new SetLevel(3));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        (await h.NextFailureAsync()).SliceKey.ShouldBe("level");
        h.Middleware.LastKnownPayload["ducky:level"].ShouldBe("""{"Value":2}""");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":3}""");
        await h.IterationsAsync("level", 3);
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();

        // Dispatched with isFailure: a reducer that throws on it is logged, never routed as a ReducerFailed (INV-12).
        h.Store.State.Get<Failures>().Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Persist_TransientNaN_NextValidValueIsWritten()
    {
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        await h.InitializeAsync();

        // A NaN can't be serialized: PersistenceFailed, nothing written, and the signal is consumed (no timer retry).
        h.Store.Dispatch(new SetLevel(double.NaN));
        var failed = await h.NextFailureAsync();
        failed.SliceKey.ShouldBe("level");
        failed.ShouldBe(new PersistenceFailed("level", "SerializationFailed", "The state of 'level' could not be serialized as Level."));
        await h.IterationsAsync("level", 1);
        h.Time.Advance(TimeSpan.FromMinutes(1));
        h.Middleware.Writers["level"].Signalled.ShouldBeFalse();

        // The next valid value is written, and it is the first write.
        h.Store.Dispatch(new SetLevel(2.5));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2.5}""");
        h.Written.Count.ShouldBe(1);
        await h.IterationsAsync("level", 2);

        // A NaN starts a streak; going back to the stored value is deduped, and storage holds the state again, so the
        // streak ends there and a later provider failure is a new streak, reported again.
        h.Store.Dispatch(new SetLevel(double.NaN));
        (await h.NextFailureAsync()).ErrorType.ShouldBe("SerializationFailed");
        await h.IterationsAsync("level", 3);
        h.Store.Dispatch(new SetLevel(2.5));
        await h.IterationsAsync("level", 4);
        h.Written.Count.ShouldBe(1);
        h.OnSet = static _ => throw new JSException("QuotaExceededError");
        h.Store.Dispatch(new SetLevel(3));
        (await h.NextFailureAsync()).ShouldBe(new PersistenceFailed("level", nameof(JSException), "QuotaExceededError"));
    }

    [Fact]
    public async Task Write_FirstChange_EnvelopeUnderItsKeyAreaAndStoreId()
    {
        // (non-normative) {prefix}:{sliceKey} in the slice's area, the envelope with the declared version and the write time,
        // and the store's one id for every write; a change to a slice that is not persisted writes nothing.
        await using var h = new WriterHarness(static d => d
            .Persist<LevelSlice>(static o => o.Migrate(1, static node => node).Migrate(2, static node => node).Version = 3)
            .Persist<DialSlice>(static o => o.Storage = PersistStorage.Session)
            .AddBlazor(static o => o.KeyPrefix = "app"));
        await h.InitializeAsync();

        h.Store.Dispatch(new Increment());
        h.Store.Dispatch(new SetLevel(1.5));
        var level = await h.NextWriteAsync();
        h.Store.Dispatch(new SetDial(2));
        var dial = await h.NextWriteAsync();

        level.ShouldBe(level with { Area = "local", Key = "app:level", Value = EnvelopeWriter.Write("""{"Value":1.5}""", 3, h.Time.GetUtcNow()) });
        level.Id.ShouldMatch("^[0-9a-f]{32}$");
        dial.ShouldBe(new StoredWrite("session", "app:dial", EnvelopeWriter.Write("""{"Value":2}""", 1, h.Time.GetUtcNow()), level.Id));
        await h.IterationsAsync("level", 1);
        await h.IterationsAsync("dial", 1);
        h.Middleware.LastKnownPayload.ShouldBe(new Dictionary<string, string> { ["app:level"] = """{"Value":1.5}""", ["app:dial"] = """{"Value":2}""" }, ignoreOrder: true);
        h.Middleware.Writers.Keys.ShouldBe(["level", "dial"], ignoreOrder: true);
    }

    [Fact]
    public async Task Write_SameAsBaseline_Skipped()
    {
        // (non-normative) The hydration read set the baseline: a change back to the stored content writes nothing.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        h.Storage[("local", "ducky:level")] = EnvelopeWriter.Write("""{"Value":4}""", 1, h.Time.GetUtcNow());
        await h.InitializeAsync();
        h.Store.State.Get<Level>().ShouldBe(new Level(4));

        h.Store.Dispatch(new SetLevel(4));
        await h.IterationsAsync("level", 1);
        h.Written.ShouldBeEmpty();

        h.Store.Dispatch(new SetLevel(5));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":5}""");
        h.Written.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Write_DuringHydration_DeferredThenWrittenAfterTerminal()
    {
        // (non-normative) A change while its key is Hydrating is skipped into _deferred, and the terminal signals it again.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>());
        var read = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnGet = _ => read.Task;
        var init = h.InitializeAsync();

        h.Line.Send!(new SetLevel(7), false);
        await h.IterationsAsync("level", 1);
        h.Middleware.Deferred.Keys.ShouldBe(["level"]);
        h.Written.ShouldBeEmpty();

        read.SetResult(null);
        await init;
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":7}""");
        h.Middleware.Deferred.ShouldBeEmpty();
        // Nothing was written before the terminal (Written was empty above). The write may be logged before the recorder logs
        // the terminal: persistence's AfterReduce signals it first. StoreInitialized may come before or after it.
        h.Log.Entries.Where(static entry => entry is not "initialized Hydrated:0" and not """write:ducky:level:{"Value":7}""").ShouldBe([
            "restore:@ducky/persistence Hydrating:0",
            "Ducky.Blazor.Tests.Persistence.SetLevel Hydrating:0",
            "completed:False:0:System Hydrated:0",
        ]);
    }

    [Fact]
    public async Task Write_ChangeBeforeFirstRead_WrittenAfterTerminal()
    {
        // (non-normative) Nothing is written before the first read: a change reduced before persistence's synchronous prefix
        // (a DispatchSystem from a middleware registered earlier) only marks its key deferred.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(), early: static d => d.Use<EarlyChange>());
        await h.InitializeAsync();

        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");
        // The write follows the read. It may be logged before the recorder logs the terminal: persistence's AfterReduce
        // signals it first.
        h.Js.Calls.Select(static call => call.Identifier).ShouldBe(["import", "storageGet", "storageSet"]);
        h.Log.Entries.Where(static entry => entry is not "initialized Hydrated:0" and not """write:ducky:level:{"Value":2}""").ShouldBe([
            "Ducky.Blazor.Tests.Persistence.SetLevel Hydrated:0",
            "Ducky.Blazor.Tests.Core.Increment Hydrated:0",
            "restore:@ducky/persistence Hydrating:0",
            "completed:False:0:System Hydrated:0",
        ]);
    }

    [Fact]
    public async Task Write_Debounce_OnTimeProvider_ChangesCoalesce()
    {
        // (non-normative) A change waits for Debounce on the TimeProvider; changes inside it are written once, as the
        // state current when the debounce ends.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.FromSeconds(1)));
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Store.Dispatch(new SetLevel(2));
        h.Written.ShouldBeEmpty();
        h.Time.Advance(TimeSpan.FromSeconds(1));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":2}""");

        // The second change's signal waits once more, then finds nothing new to write.
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.IterationsAsync("level", 2);
        h.Written.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Write_ServerBrowserStorage_Default250msDebounce()
    {
        // (non-normative) §11.1: browser storage written from the server waits 250 ms by default.
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>(), browser: false);
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        await h.Time.TimerAsync(TimeSpan.FromMilliseconds(250));
        h.Written.ShouldBeEmpty();
        h.Time.Advance(TimeSpan.FromMilliseconds(250));
        (await h.NextWriteAsync()).Payload.ShouldBe("""{"Value":1}""");
    }

    [Fact]
    public async Task Write_Scoped_KeyFromTheSnapshotEpochsScope()
    {
        // (non-normative) With a Scope, the key carries the scope recorded for the snapshot's epoch.
        await using var h = new WriterHarness(
            static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.Scope = static (_, _) => new("alice")),
            browser: false);
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        (await h.NextWriteAsync()).Key.ShouldBe("ducky:alice:level");
    }

    [Fact]
    public async Task Write_NullScope_SkippedWithoutDeferral()
    {
        // (non-normative) A scope recorded as null names no key: a scoped change does no I/O and is not deferred (the next
        // switch resets scoped slices anyway).
        await using var h = new WriterHarness(
            static d => d.Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.Scope = static (_, _) => new((string?)null)),
            browser: false);
        await h.InitializeAsync();

        h.Store.Dispatch(new SetLevel(1));
        await h.IterationsAsync("level", 1);
        h.Middleware.Scopes[0].ShouldBeNull();
        h.Middleware.Deferred.ShouldBeEmpty();
        h.Written.ShouldBeEmpty();
    }

    // CsCheck generates the cases (fixed seeds when gated); each runs as an async scenario afterwards, so a failure names
    // its case instead of shrinking it.
    private static List<T> Cases<T>(Gen<T> gen)
    {
        ConcurrentBag<T> cases = [];
        Property.Check(gen, cases.Add);
        return [.. cases];
    }

    private static string Describe<T>(T[] script) => $"script [{string.Join(", ", script)}]";

    // Two persisted keys whose every storageSet is held until the script (or the final drain) completes it. Kinds: 0 sets
    // the level, 1 the dial, 2 completes the level's oldest held write, 3 the dial's.
    private static async Task<Run> RunAsync((int Kind, int Value)[] script)
    {
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>().Persist<DialSlice>(static o => o.Storage = PersistStorage.Session));
        var held = new ConcurrentDictionary<string, ConcurrentQueue<TaskCompletionSource<object?>>>();
        var inFlight = new ConcurrentDictionary<string, int>();
        var stored = new ConcurrentDictionary<string, string>();
        var maxInFlight = 0;
        h.OnSet = write =>
        {
            var count = inFlight.AddOrUpdate(write.Key, 1, static (_, n) => n + 1);
            InterlockedMax(ref maxInFlight, count);
            stored[write.Key] = write.Payload;
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            held.GetOrAdd(write.Key, static _ => new()).Enqueue(completion);
            return completion.Task;
        };

        bool Complete(string key)
        {
            if (!held.GetOrAdd(key, static _ => new()).TryDequeue(out var completion))
            {
                return false;
            }

            inFlight.AddOrUpdate(key, 0, static (_, n) => n - 1);
            completion.SetResult(true);
            return true;
        }

        await h.InitializeAsync();
        var expected = new Dictionary<string, string>();
        foreach (var (kind, value) in script)
        {
            switch (kind)
            {
                case 0:
                    h.Store.Dispatch(new SetLevel(value));
                    expected["ducky:level"] = WriterHarness.Payload(new Level(value));
                    break;
                case 1:
                    h.Store.Dispatch(new SetDial(value));
                    expected["ducky:dial"] = WriterHarness.Payload(new Dial(value));
                    break;
                default:
                    Complete(kind == 2 ? "ducky:level" : "ducky:dial");
                    break;
            }

            await Task.Yield();
        }

        // A last change of each key to a value the script never writes: once its write is issued, nothing the script raised
        // can follow it (a later iteration reads this same final state), so the drain below can't stop on an earlier write
        // of an equal value while a write of an older snapshot is still on its way.
        h.Store.Dispatch(new SetLevel(100));
        h.Store.Dispatch(new SetDial(100));
        expected["ducky:level"] = WriterHarness.Payload(new Level(100));
        expected["ducky:dial"] = WriterHarness.Payload(new Dial(100));

        // Complete every held write until the stored values are the final state and nothing is in flight. In that order: a
        // write counts itself in flight before it stores, so a final write still on its way is never missed.
        while (true)
        {
            h.TakeWrites();
            var completed = Complete("ducky:level") | Complete("ducky:dial");
            if (!completed && expected.All(pair => stored.GetValueOrDefault(pair.Key) == pair.Value) && inFlight.Values.All(static n => n == 0))
            {
                break;
            }

            if (!completed)
            {
                // ponytail: the drain ends on the property itself, so a final state never written surfaces here, after the
                // 10 s bound; named with its script and what was stored. A quiescence-based drain would fail faster.
                try
                {
                    await h.WriteArrivedAsync();
                }
                catch (TimeoutException exception)
                {
                    throw new ShouldAssertException($"{Describe(script)}: the final state was never written; stored [{string.Join(", ", stored.OrderBy(static pair => pair.Key))}], expected [{string.Join(", ", expected.OrderBy(static pair => pair.Key))}]", exception);
                }
            }
        }

        // Nothing is in flight and storage holds the final state, so the dispose flush finds every key clean.
        await h.DisposeAsync();
        return new(Volatile.Read(ref maxInFlight), new(stored), expected);
    }

    private static void InterlockedMax(ref int target, int value)
    {
        var current = Volatile.Read(ref target);
        while (value > current && Interlocked.CompareExchange(ref target, value, current) is var seen && seen != current)
        {
            current = seen;
        }
    }

    // Both writers are held inside a write, so a signal raised by the script stays visible in their channel. Origins: 0-2
    // restore with Hydration, CrossTab, DevTools; 3 dispatches locally.
    private static async Task NoWriteCaseAsync((int Origin, int Key, int Value)[] script)
    {
        await using var h = new WriterHarness(static d => d.Persist<LevelSlice>().Persist<DialSlice>());
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnSet = _ => held.Task;
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(-1));
        h.Store.Dispatch(new SetDial(-1));
        await h.NextWriteAsync();
        await h.NextWriteAsync();

        var local = new bool[2];
        foreach (var (origin, key, value) in script)
        {
            object state = key == 0 ? new Level(value) : new Dial(value);
            if (origin == 3)
            {
                h.Store.Dispatch(key == 0 ? new SetLevel(value) : new SetDial(value));
                local[key] = true;
            }
            else
            {
                h.Store.Restore(new Dictionary<string, object> { [key == 0 ? "level" : "dial"] = state }, _restores[origin]);
            }
        }

        h.Middleware.Writers["level"].Signalled.ShouldBe(local[0], Describe(script));
        h.Middleware.Writers["dial"].Signalled.ShouldBe(local[1], Describe(script));
        held.SetResult(true);
    }

    private sealed record Run(int MaxInFlight, Dictionary<string, string> Stored, Dictionary<string, string> Expected);
}

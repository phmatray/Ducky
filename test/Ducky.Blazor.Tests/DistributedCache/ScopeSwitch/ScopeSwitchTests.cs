using System.Collections.Concurrent;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.6 "Scope switch", §11.5 "Final write under the old scope" and "Switch reads"; INV-14, INV-16, INV-17. A circuit
// store over the real MemoryDistributedCache (gated per call) whose provider raises AuthenticationStateChanged.
public sealed partial class ScopeSwitchTests : IDisposable
{
    private const int NullScopeEventId = 2027;
    private const int DiscardedEventId = 2041;
    private const int SwitchFailedEventId = 2042;
    private const int SubscribeFailedEventId = 2043;
    private const string Alice = "app:alice:counter";
    private const string Bob = "app:bob:counter";
    private const string Carol = "app:carol:counter";
    private static readonly TimeSpan _debounce = TimeSpan.FromSeconds(3);
    private static readonly Action<PersistOptions> _server = static o => o.Storage = PersistStorage.Server;
    private readonly FakeLogCollector _logs = new();
    private readonly GatedCache _cache = new();
    private readonly SwitchingAuth _auth = new("alice");
    private Task<string?> _scope = Task.FromResult<string?>("alice"); // ScopeFromField's result, set before each notification

    [Fact]
    public async Task ScopeSwitch_TwoOverlappingSwitches_DistinctEpochs_LatestWins()
    {
        // A second switch while the first one's read is in flight: each takes its own epoch from the counter, the first
        // issues nothing, and only the latest restores and ends. The second read of that key's loop never waits for the first:
        // nothing releases Bob's held read, only its attempt's token ends it.
        _cache.Store(Bob, Envelope(5));
        _cache.Store(Carol, Envelope(7));
        await using var h = Circuit();
        await h.InitializeAsync();
        _cache.HoldUntilCancelled($"get:{Bob}");
        var carolRead = _cache.Hold($"get:{Carol}");

        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        _auth.Change("carol");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Carol}"));
        Persistence(h).ScopeEpoch.ShouldBe(2);
        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrating);
        carolRead.SetResult();

        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(7));
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice", [1] = "bob", [2] = "carol" });
        h.Log.Entries.Where(static e => e.StartsWith("completed:", StringComparison.Ordinal) || e.StartsWith("failed:", StringComparison.Ordinal))
            .ShouldBe(["completed:False:0:System Hydrated:0", "completed:True:2:System Hydrated:2"]);
        h.Log.Entries.ShouldContain("restore:@ducky/persistence Hydrating:1");
        h.Log.Entries.ShouldContain("restore:@ducky/persistence Hydrating:2");
        h.Log.Entries.ShouldContain("restore:counter Hydrating:2");

        // The new epoch's baseline: nothing is written back.
        await h.IterationsAsync("counter", 2);
        _cache.Calls.ShouldNotContain($"set:{Carol}");
        _cache.Calls.ShouldNotContain($"set:{Bob}");
    }

    [Fact]
    public async Task ScopeSwitch_StaleScopeResolvedAfterNewerSwitch_NeverRecorded()
    {
        // (non-normative) §11.6 "if the attempt is no longer current, stop": a scope that resolves for a superseded epoch is
        // not recorded and reads nothing; the epoch's snapshots stay unnamed.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _scope = bob.Task;
        _auth.Change("bob");
        _scope = Task.FromResult<string?>("carol");
        _auth.Change("carol");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        bob.SetResult("bob");
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));

        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice", [2] = "carol" });
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Carol}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ToAnonymous_StatusReturnsToHydrated_UnscopedWritesResume()
    {
        // A sign-out: the scoped slice is reset, no I/O under any scoped key, the attempt ends at once with
        // HydrationCompleted(false) and Status returns to Hydrated. Unscoped writes were never blocked.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        await h.InitializeAsync();
        h.Store.State.Get<Counter>().ShouldBe(new Counter(3));

        _auth.Change(null);

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice", [1] = null });
        h.Log.Entries.ShouldContain("completed:False:1:System Hydrated:1");
        _cache.Attempts.ShouldBe([$"get:{Alice}"]);

        h.Store.Dispatch(new SetDial(8));
        (await h.NextWriteAsync()).ShouldSatisfyAllConditions(
            static write => write.Key.ShouldBe("app:dial"),
            static write => write.Payload.ShouldBe("""{"Value":8}"""));

        // A scoped change under the null scope: skipped without deferral, no write and nothing left dirty.
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        h.Middleware.Deferred.ShouldBeEmpty();
        _cache.Attempts.ShouldBe([$"get:{Alice}"]);
        _cache.Stored(Alice).ShouldBe(Envelope(3));

        // The next sign-in hydrates again, and a second sign-out logs nothing more: the Info log is once per store.
        _auth.Change("alice");
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(3));
        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 3, Status: PersistenceStatus.Hydrated });
        var nullScope = _logs.GetSnapshot().Where(static r => r.Id.Id == NullScopeEventId).ShouldHaveSingleItem();
        nullScope.Level.ShouldBe(LogLevel.Information);
    }

    [Fact]
    public async Task DistributedCache_UserChangesInsideCircuit_ActionDuringSwitch_NeverWrittenUnderOtherScope()
    {
        // A change committed after the reset, while the new scope is unknown: never under the old key, and never under the
        // new key before that key was read. A stored value replaces it (the documented ceiling), and with nothing stored it
        // is written after the terminal.
        _cache.Store(Alice, Envelope(3));
        _cache.Store(Bob, Envelope(9));
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _scope = bob.Task;
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 1);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Middleware.Deferred.ContainsKey("counter"));
        _cache.Attempts.ShouldBe([$"get:{Alice}"]);

        bob.SetResult("bob");
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(9));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Bob}"]);
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":3}""");

        var carol = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = carol.Task;
        _auth.Change("carol");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Middleware.Deferred.ContainsKey("counter"));
        carol.SetResult("carol");
        await WriterHarness.Until(() => _cache.Stored(Carol) is not null);

        Payload(_cache.Stored(Carol)!).ShouldBe("""{"Value":1}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Bob}", $"get:{Carol}", $"set:{Carol}"]);
        Payload(_cache.Stored(Bob)!).ShouldBe("""{"Value":9}""");
    }

    [Fact]
    public async Task DistributedCache_UserChangeWithConcurrentLocalChange_NeverWritesOldStateUnderNewKey()
    {
        // A write in flight and a newer change at the switch, and one more change right after the reset: the old key ends with
        // the newest old state, the new key gets no old state, and its first write comes after its read and carries only the
        // change made under the new epoch.
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 1);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        h.Store.Dispatch(new Increment());
        await h.Store.WhenIdleAsync(Ct);
        inFlight.SetResult();
        await WriterHarness.Until(() => _cache.Stored(Bob) is not null);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        Payload(_cache.Stored(Bob)!).ShouldBe("""{"Value":1}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}", $"get:{Bob}", $"set:{Bob}"]);
    }

    [Fact]
    public async Task Write_UnscopedChangeDuringScopeSwitch_IsWrittenAfterTerminal()
    {
        // A switch during the initial attempt carries its unscoped key (§11.6 step 1): the writer keeps skipping it while
        // Hydrating, and the change is written after the terminal, not left for the key's next change. The superseded
        // attempt's slow read of the old user is discarded when it lands.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        var statusAtWrite = PersistenceStatus.NotStarted;
        h.OnSet = _ =>
        {
            statusAtWrite = Persistence(h).Status;
            return true;
        };
        var aliceRead = _cache.Hold($"get:{Alice}");
        var bobRead = _cache.Hold($"get:{Bob}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));

        _auth.Change("bob");
        h.Line.Send!(new SetDial(8), false);
        await WriterHarness.Until(() => h.Middleware.Deferred.ContainsKey("dial"));
        h.Written.ShouldBeEmpty();

        bobRead.SetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var write = await h.NextWriteAsync();
        (write.Key, write.Payload).ShouldBe(("app:dial", """{"Value":8}"""));
        statusAtWrite.ShouldBe(PersistenceStatus.Hydrated); // not the log's order: the writer can log ahead of the recorder
        h.Log.Entries.ShouldContain("completed:False:1:System Hydrated:1");

        aliceRead.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        h.Log.Entries.ShouldNotContain(static e => e.EndsWith(":0:System Hydrated:0", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScopeSwitch_AfterAnAttemptRestored_UnscopedKeysNotReadAgain()
    {
        // (non-normative) §11.6 step 1: only an attempt that has not restored carries its unscoped keys. After that, the switch
        // reads the scoped keys alone, and an unscoped change meets no skip at all: it is written before the terminal.
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        h.Storage[("local", "app:dial")] = EnvelopeWriter.Write("""{"Value":4}""", 1, h.Time.GetUtcNow());
        await h.InitializeAsync();
        var bobRead = _cache.Hold($"get:{Bob}");

        _auth.Change("bob");
        h.Store.Dispatch(new SetDial(8));
        (await h.NextWriteAsync()).Key.ShouldBe("app:dial");
        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrating);
        bobRead.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);

        h.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial"]);
        h.Store.State.Get<Dial>().ShouldBe(new Dial(8));
    }

    [Fact]
    public async Task ScopeSwitch_ChangeCommittedJustBeforeSwitch_WrittenUnderOldKey()
    {
        // The handler runs inside the drain of a first change, with a second one queued ahead of the reset: Store.State does
        // not hold it yet, and PreviousState, taken on the drainer at the reset, does. A snapshot captured by the handler
        // would lose it.
        await using var h = Circuit();
        await h.InitializeAsync();
        var raised = false;
        var writer = h.Middleware.Writers["counter"];
        h.Log.OnEntry = entry =>
        {
            if (!raised && entry.Contains("Increment", StringComparison.Ordinal))
            {
                raised = true;
                h.Store.Dispatch(new Increment());
                _auth.Change("bob");

                // The reset is still queued behind this drain: the read must wait for it. A read that did not would be posted
                // inside Change; held here until the loop takes it, it runs before the final write, and get:Bob is not last.
                if (writer.ReadPending)
                {
                    _cache.GetBegan($"get:{Bob}").Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
                }
            }
        };

        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _cache.Attempts.ShouldNotContain($"set:{Bob}");
        _cache.Attempts.Last().ShouldBe($"get:{Bob}");
    }

    [Fact]
    public async Task ScopeSwitch_SameScopeReasserted_FinalWriteNotRolledBack()
    {
        // The same user re-notified, the scope resolving asynchronously, so it stays on the switch path, with the final write
        // in flight: the read of that very key runs after it and restores the last change, which is not rolled back.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));
        var again = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _scope = again.Task;
        _auth.Change("alice");
        again.SetResult("alice");
        await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(1));
        _cache.Attempts.Count(static c => c == $"get:{Alice}").ShouldBe(1); // the read waits behind the in-flight write
        inFlight.SetResult();
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        h.Store.State.Get<Counter>().ShouldBe(new Counter(2));
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}", $"get:{Alice}"]);

        // A->B->A: the second visit's read of Alice's key runs after the old scope's final write, held in flight meanwhile.
        var finalWrite = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Count(static c => c == $"set:{Alice}") == 3);
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var back = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = bob.Task;
        _auth.Change("bob");
        _scope = back.Task;
        _auth.Change("alice");
        back.SetResult("alice");
        await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(3));
        finalWrite.SetResult();
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 3, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        h.Store.State.Get<Counter>().ShouldBe(new Counter(3));
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":3}""");
        _cache.Attempts.Last().ShouldBe($"get:{Alice}");
        _cache.Attempts.Count(static c => c == $"set:{Alice}").ShouldBe(3);
    }

    [Fact]
    public async Task ScopeSwitch_FinalOldScopeWriteFailsTransiently_RetriedUnderOldKey()
    {
        // The queued snapshot's write fails: it stays at the head of the queue and is retried with the backoff, that snapshot
        // under that key, never Store.State; the switch read waits behind it.
        _cache.Store(Bob, Envelope(9));
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        _cache.Fail($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 1);
        inFlight.SetResult();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        (await h.NextFailureAsync()).ErrorType.ShouldBe(nameof(TimeoutException));
        _cache.Attempts.ShouldNotContain($"get:{Bob}");
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");

        h.Time.Advance(TimeSpan.FromSeconds(1));
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}", $"set:{Alice}", $"get:{Bob}"]);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(9));
        _cache.Stored(Bob).ShouldBe(Envelope(9));
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
    }

    [Fact]
    public async Task ScopeSwitch_SkipReadTheResetBeforeItsSnapshot_NoInitialStateWrittenUnderNewKey()
    {
        // (non-normative) The writer reads the reset's own state before AfterReduce hands the snapshot over: it skips and defers
        // the old change. The snapshot then covers that change, so the terminal releases nothing, and the new key, which holds
        // nothing, gets no write of the initial state.
        var inFlight = _cache.Hold($"set:{Alice}");
        var deferred = new ManualResetEventSlim();
        await using var h = new WriterHarness(
            d => d.Persist<CounterSlice>(_server).AddBlazor(o =>
            {
                o.KeyPrefix = "app";
                o.AfterDeferHook = (_, point) =>
                {
                    if (point == DeferPoint.AfterAdd)
                    {
                        deferred.Set();
                    }
                };
            }),
            browser: false,
            early: d => d.Use<HoldReset>(),
            services: s => s.AddSingleton<IDistributedCache>(_cache)
                .AddSingleton<AuthenticationStateProvider>(_auth)
                .AddSingleton(new HoldResetPlan(() =>
                {
                    inFlight.SetResult();
                    deferred.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();
                })));
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        deferred.IsSet.ShouldBeTrue();
        h.Middleware.Deferred.ShouldBeEmpty();
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        _cache.Attempts.ShouldNotContain($"set:{Bob}");
    }

    [Fact]
    public async Task ScopeSwitch_ClearAfterResetWithWriteInFlight_DiscardedChangeNotWrittenUnderNewKey()
    {
        // (non-normative) INV-16: the clear comes after the reset, while the loop is busy, and after a change under the new
        // scope. It runs after the older final snapshot, so the old key gets its final state and the clear still discards the
        // change it follows: the new key is never written.
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));

        _auth.Change("bob");
        await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(1) && Persistence(h).ScopeEpoch == 1);
        h.Store.Dispatch(new Increment());
        h.Store.Dispatch(new ClearPersistedState());
        await h.Store.WhenIdleAsync(Ct);
        inFlight.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);
        await h.Store.WhenIdleAsync(Ct);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
        _cache.Stored(Bob).ShouldBeNull();
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ClearsOnBothSidesOfReset_BothKeysStayRemoved()
    {
        // (non-normative) INV-16: one clear before the reset and one right after it, with no change in between, so its stamp
        // equals the final snapshot's. The first clear already discarded all that snapshot holds, and the second one must not
        // hide that: the old user's cleared key stays gone, and so does the new user's.
        _cache.Store(Bob, Envelope(5));
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new ClearPersistedState());
        await h.Store.WhenIdleAsync(Ct);

        _auth.Change("bob");
        await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(1) && Persistence(h).ScopeEpoch == 1);
        h.Store.Dispatch(new ClearPersistedState());
        await h.Store.WhenIdleAsync(Ct);
        inFlight.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        _cache.Stored(Alice).ShouldBeNull();
        _cache.Stored(Bob).ShouldBeNull();
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_SameKeyClearAfterReset_FinalDoesNotBringKeyBack()
    {
        // (non-normative) INV-16 on A->A: the clear names the very key the older final snapshot writes. The final runs first,
        // the clear removes it, and the switch read finds nothing.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));
        var again = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _scope = again.Task;
        _auth.Change("alice");
        again.SetResult("alice");
        await WriterHarness.Until(() => h.Middleware.Scopes.ContainsKey(1) && Persistence(h).ScopeEpoch == 1);
        h.Store.Dispatch(new ClearPersistedState());
        await h.Store.WhenIdleAsync(Ct);
        inFlight.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        _cache.Stored(Alice).ShouldBeNull();
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}", $"get:{Alice}"]);
    }

    [Fact]
    public async Task ScopeSwitch_RestoreOfUnknownHydratingEpoch_WriterSurvives()
    {
        // (non-normative) The public IStore.Restore can name an epoch no switch made: no key set, no scope. The change waiting
        // behind a write defers until a release, the writer's loop survives and the write lands. Only a Hydration restore
        // closes a scope: this one hands no final snapshot over, whose write it would have triggered (INV-16).
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));

        h.Store.Restore(new Dictionary<string, object> { ["@ducky/persistence"] = new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 99 } }, Origin.DevTools);
        await h.Store.WhenIdleAsync(Ct);
        inFlight.SetResult();
        await WriterHarness.Until(() => h.Middleware.Deferred.ContainsKey("counter"));
        _cache.Attempts.Count(static c => c == $"set:{Alice}").ShouldBe(1);
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");

        h.Store.Restore(new Dictionary<string, object> { ["@ducky/persistence"] = new PersistenceState(PersistenceStatus.Hydrated) }, Origin.DevTools);
        await WriterHarness.Until(() => _cache.Attempts.Count(static c => c == $"set:{Alice}") == 2);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");

        // The drainer's side of the same epoch: a Hydration restore finds no attempt to tell, and AfterReduce does not throw.
        h.Store.Restore(new Dictionary<string, object> { ["@ducky/persistence"] = new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 98 } }, Origin.Hydration);
        await h.Store.WhenIdleAsync(Ct);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Exception != null);
    }

    [Fact]
    public async Task ScopeSwitch_SignOutCommitsBetweenSignalsAndSnapshot_FinalStillWritten()
    {
        // (non-normative) INV-16: a write has read the signals when a sign-out's reset and its null-scope terminal commit, and
        // only then reads Store.State, which names no key. That attempt must not mark the change written: the reset's final
        // snapshot, queued meanwhile, still writes the old user's last change under the old key.
        var atSnapshot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var go = new ManualResetEventSlim();
        await using var h = Circuit(d => d.AddBlazor(o => o.AfterDeferHook = (_, point) =>
        {
            if (point == DeferPoint.BeforeSnapshot && atSnapshot.TrySetResult())
            {
                // The writer's own pool thread blocks; the switch runs on the test's.
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded wait on another thread's progress
                go.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
#pragma warning restore VSTHRD002
            }
        }));
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await atSnapshot.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await h.Store.WhenIdleAsync(Ct);
        go.Set();
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        _cache.Stored(Alice).ShouldNotBeNull();
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ClearChangeAndSignOutWhileLoopReadsItsQueues_RemovalRunsBeforeFinal()
    {
        // (non-normative) INV-16: a clear, a change and a sign-out's reset commit after the loop read the (empty) heads of its
        // queues, so the attempt goes on to its write, which finds the final and goes back to the queues. The removal is older
        // than the final snapshot, so it runs first and the final then writes the change under the old key; served the other
        // way round, the removal would delete the old user's last change. The gate around the heads themselves is pinned by
        // ScopeSwitch_QueueHeadsAndFinalPosting_ShareTheGate.
        var armed = new ManualResetEventSlim();
        var peeked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var go = new ManualResetEventSlim();
        await using var h = Circuit(d => d.AddBlazor(o => o.AfterDeferHook = (_, point) =>
        {
            if (point == DeferPoint.Peeked && armed.IsSet && peeked.TrySetResult())
            {
#pragma warning disable VSTHRD002 // justification: the hook is synchronous by contract; bounded wait on another thread's progress
                go.Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
#pragma warning restore VSTHRD002
            }
        }));
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Alice) != null && !h.Middleware.Writers["counter"].Dirty);
        armed.Set();
        h.Store.Dispatch(new Increment());
        await peeked.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        h.Store.Dispatch(new ClearPersistedState());
        h.Store.Dispatch(new Increment());
        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await h.Store.WhenIdleAsync(Ct);
        go.Set();
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        _cache.Stored(Alice).ShouldNotBeNull();
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":3}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"set:{Alice}"]);
    }

    [Fact]
    public async Task ScopeSwitch_QueueHeadsAndFinalPosting_ShareTheGate()
    {
        // (non-normative) INV-16: a removal is posted ahead of a newer reset's final, so a loop that sees the final must see the
        // removal. Read apart, the removal's head can be read empty, then the clear and the reset posted, then the final's head
        // read: the final runs first and the older removal deletes it. Both heads, and the final's posting, sit under the gate
        // the removal is posted under: asserted where it must be held, between the two reads on the loop, and at the posting
        // on the drainer.
        WriterHarness? harness = null;
        ConcurrentQueue<(DeferPoint Point, bool Held)> seen = new();
        await using var h = Circuit(d => d.AddBlazor(o => o.AfterDeferHook = (_, point) =>
        {
            if (point is DeferPoint.BetweenPeeks or DeferPoint.PostingFinal)
            {
                seen.Enqueue((point, harness!.Middleware.Writers["counter"].GateHeld));
            }
        }));
        harness = h;
        await h.InitializeAsync();

        h.Store.Dispatch(new Increment());
        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);

        seen.ShouldContain((DeferPoint.BetweenPeeks, true));
        seen.ShouldContain((DeferPoint.PostingFinal, true));
        seen.ShouldAllBe(static s => s.Held);
        h.Middleware.Writers["counter"].GateHeld.ShouldBeFalse();
    }

    [Fact]
    public async Task ScopeSwitch_TokenCallbackThrowsAtDispose_FlushStillWrites()
    {
        // (non-normative) A callback a Scope delegate registered on its attempt's token throws when dispose ends that token. The
        // flush still runs: the key sleeping in retry backoff is written, not lost.
        var user = "alice";
        await using var h = Circuit(d => d.AddBlazor(o => o.Scope = (_, token) =>
        {
            if (user == "bob")
            {
                token.Register(static () => throw new InvalidOperationException("callback"));
            }

            return ValueTask.FromResult<string?>(user);
        }));
        await h.InitializeAsync();
        using var lost = new Microsoft.Extensions.Diagnostics.Metrics.Testing.MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        user = "bob";
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        _cache.Fail($"set:{Bob}");
        h.Store.Dispatch(new Increment());
        (await h.NextFailureAsync()).ErrorType.ShouldBe(nameof(TimeoutException));

        await h.DisposeStoreAsync();

        Payload(_cache.Stored(Bob)!).ShouldBe("""{"Value":1}""");
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task ScopeSwitch_QueuedReadOfSupersededAttempt_NoIoUnderItsKey()
    {
        // (non-normative) A read posted behind an in-flight write, its attempt superseded before the loop reaches it: no I/O
        // under the superseded user's key.
        await using var h = Circuit();
        await h.InitializeAsync();
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));

        _auth.Change("bob");
        await WriterHarness.Until(() => h.Middleware.Writers["counter"].ReadPending);
        _auth.Change("carol");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 2);
        inFlight.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);

        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"get:{Carol}"]);
    }

    [Fact]
    public async Task ScopeSwitch_NewerResetWhileReadInFlight_ReadWaitsForItsFinal()
    {
        // (non-normative) §11.5: switch reads run only once the snapshot queue is empty. The init-phase deadline ended the attempt whose read
        // (of a provider that ignores its token) stays in flight, a change follows, and a same-user switch queues a final
        // snapshot and its read behind it: that read sees the final write, so the change is not rolled back.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit();
        _cache.Hold($"get:{Alice}");
        var read = _cache.HoldIgnoringToken($"get:{Alice}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        _auth.Change("alice");
        await WriterHarness.Until(() => _cache.Attempts.Count(static c => c == $"get:{Alice}") == 2);
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });
        h.Store.Dispatch(new Increment());
        await h.Store.WhenIdleAsync(Ct);

        _auth.Change("alice");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 2 && h.Middleware.Writers["counter"].ReadPending);
        read.SetResult();
        await WriterHarness.Until(() => Persistence(h).Status == PersistenceStatus.Hydrated);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(1));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Alice}", $"set:{Alice}", $"get:{Alice}"]);
    }

    [Fact]
    public async Task ScopeSwitch_StaleScopesResolvedDuringInit_CarriedKeyReadOnce()
    {
        // (non-normative) §11.6 "no longer current, stop": two attempts superseded while resolving their scopes carry the
        // unscoped key on to the latest one, which alone reads it. Neither records its late scope nor reads anything.
        var alice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bob = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        _auth.Gate = alice.Task;
        var init = h.InitializeAsync();

        _auth.Gate = bob.Task;
        _auth.Change("bob");
        _auth.Gate = null;
        _auth.Change("carol");
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        alice.SetResult();
        bob.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Count(static r => r.Id.Id == DiscardedEventId) == 2);

        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [2] = "carol" });
        _cache.Attempts.ShouldBe([$"get:{Carol}"]);
        h.Js.Calls.Count(static c => c.Identifier == "storageGet").ShouldBe(1);
    }

    [Fact]
    public async Task ScopeSwitch_SupersededBetweenItsKeys_InitialAttemptReadsNoMore()
    {
        // (non-normative) §11.6 "no longer current, stop", between two keys: the initial attempt, superseded while it reads its
        // first key, leaves the second one unread.
        const string AliceDial = "app:alice:dial";
        const string BobDial = "app:bob:dial";
        await using var h = Circuit(static d => d.Persist<DialSlice>(_server));
        var read = _cache.Hold($"get:{AliceDial}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{AliceDial}"));

        _auth.Change("bob");
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        read.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));

        _cache.Attempts.ShouldBe([$"get:{AliceDial}", $"get:{BobDial}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_SupersededDuringInit_CurrentAttemptKeepsRunning()
    {
        // (non-normative) Supersession ends the old attempt's read through its token. That is no init deadline: the attempt now
        // current goes on, and its own read ends it.
        _cache.Store(Carol, Envelope(7));
        var alice = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = alice.Task;
        await using var h = Circuit(ScopeFromField);
        _cache.Hold($"get:{Bob}");
        var carolRead = _cache.Hold($"get:{Carol}");
        var init = h.InitializeAsync();

        _scope = Task.FromResult<string?>("bob");
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        _scope = Task.FromResult<string?>("carol");
        _auth.Change("carol");
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Carol}"));

        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrating);
        carolRead.SetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        h.Store.State.Get<Counter>().ShouldBe(new Counter(7));
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScopeSwitch_SupersededAttemptsDelegateThrows_NoErrorNoTerminal()
    {
        // (non-normative) INV-14: a superseded attempt issues nothing, so no Error says it ended in HydrationFailed.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

        _scope = bob.Task;
        _auth.Change("bob");
        _scope = Task.FromResult<string?>("carol");
        _auth.Change("carol");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        bob.SetException(new InvalidOperationException("no scope"));
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));

        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScopeSwitch_DeadlineEndedAttemptsDelegateThrows_NoSecondFailure()
    {
        // (non-normative) The init-phase deadline already ended the current switch attempt: its delegate failing later claims
        // nothing, so it is a Debug discard, not an Error about a failure that never reached the store.
        var alice = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = alice.Task;
        await using var h = Circuit(ScopeFromField);
        var init = h.InitializeAsync();
        _scope = bob.Task;
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 1);
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });

        bob.SetException(new InvalidOperationException("no scope"));
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));

        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
        h.Log.Entries.Count(static e => e.StartsWith("failed:", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ScopeSwitch_FinalSnapshotStillFailingAtDispose_CountedLost()
    {
        // (non-normative) §11.5: the dispose flush treats a queued snapshot like a dirty key: one immediate attempt, and it is
        // counted ducky.persistence.lost when that fails too.
        await using var h = Circuit();
        await h.InitializeAsync();
        using var lost = new Microsoft.Extensions.Diagnostics.Metrics.Testing.MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        var inFlight = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => h.Store.State.Get<Counter>() == new Counter(2));
        _cache.Fail($"set:{Alice}");
        _cache.Fail($"set:{Alice}");
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h).ScopeEpoch == 1);
        inFlight.SetResult();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));

        await h.DisposeStoreAsync();

        _cache.Attempts.Count(static c => c == $"set:{Alice}").ShouldBe(3);
        lost.GetMeasurementSnapshot().Sum(static m => m.Value).ShouldBe(1);
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
    }

    [Fact]
    public async Task ScopeSwitch_ClearJustBeforeSwitch_KeyStaysRemoved()
    {
        // (non-normative) INV-16: a clear committed just before the reset. The final snapshot holds no change the clear did not
        // discard, so it can't bring the cleared key back, though the clear reset the baseline it would dedupe against.
        await using var h = Circuit();
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Alice) is not null);

        h.Store.Dispatch(new ClearPersistedState());
        await h.Store.WhenIdleAsync(Ct);
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        _cache.Stored(Alice).ShouldBeNull();
        _cache.Attempts.Count(static c => c == $"set:{Alice}").ShouldBe(1);
    }

    [Fact]
    public async Task ScopeSwitch_ChangeAfterClearThenSwitch_WrittenUnderOldKey()
    {
        // (non-normative) The other side of that check: a change after the clear is what the final snapshot exists for. The
        // switch comes while its write is still pending, and it reaches the old key.
        await using var h = Circuit();
        await h.InitializeAsync();
        var held = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        h.Store.Dispatch(new ClearPersistedState());
        h.Store.Dispatch(new Increment());
        await h.Store.WhenIdleAsync(Ct);

        _auth.Change("bob");
        held.SetResult();
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty && !h.Middleware.Writers["counter"].RemovalPending);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":2}""");
    }

    [Fact]
    public async Task ScopeSwitch_ReReadFindsNothing_BaselineDropped()
    {
        // (non-normative) §11.5 step 6, "absent -> none": back on a key its stored value left meanwhile (expired, removed by
        // another store), the old baseline is gone, so a change equal to it is written, not deduped away.
        await using var h = Circuit();
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Alice) is not null);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await _cache.RemoveAsync(Alice, Ct);

        _auth.Change("alice");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Alice) is not null);

        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
    }

    [Fact]
    public async Task ScopeSwitch_RestoreSubscriberSwitches_SupersededAttemptIssuesNothing()
    {
        // (non-normative) INV-14: a switch raised by a subscriber of the winner's own restore, on its thread, between its claim
        // and its terminal. The superseded attempt issues nothing, and init ends with the superseding attempt's terminal.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit();
        h.Log.OnEntry = entry =>
        {
            if (entry == "restore:counter Hydrating:0")
            {
                _auth.Change("bob");
            }
        };

        await h.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(10), Ct);

        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("completed:True:0", StringComparison.Ordinal));
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("completed:False:0", StringComparison.Ordinal));
        h.Log.Entries.ShouldContain("completed:False:1:System Hydrated:1");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ScopeDelegateThrows_NeverEscapes_HydrationFailed_PersistenceResumes()
    {
        // (non-normative) §11.6: the handler never throws into the circuit. A throwing delegate is an Error log and the current
        // attempt's HydrationFailed, through the claim; the next switch works.
        var fail = false;
        await using var h = Circuit(d => d.AddBlazor(o => o.Scope = (_, _) => fail
            ? throw new InvalidOperationException("no scope")
            : ValueTask.FromResult<string?>("alice")));
        await h.InitializeAsync();
        fail = true;

        Should.NotThrow(() => _auth.Change("bob"));

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });
        h.Log.Entries.ShouldContain("failed:InvalidOperationException:1:System Failed:1");
        var error = _logs.GetSnapshot().Where(static r => r.Id.Id == SwitchFailedEventId).ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Exception.ShouldBeOfType<InvalidOperationException>();

        fail = false;
        _auth.Change("alice");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
    }

    [Fact]
    public async Task ScopeSwitch_ReadFails_HydrationFailed()
    {
        // (non-normative) §11.5 step 7: a provider failure on a switch read ends that attempt in HydrationFailed, not a retry,
        // and the writer's loop goes on. The Error log is the switch's: an initial attempt's failure has its terminal alone.
        _cache.Fail($"get:{Alice}");
        await using var h = Circuit();
        await h.InitializeAsync();
        Persistence(h).Status.ShouldBe(PersistenceStatus.Failed);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
        _cache.Fail($"get:{Bob}");

        _auth.Change("bob");

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });
        h.Log.Entries.ShouldContain("failed:TimeoutException:1:System Failed:1");
        _logs.GetSnapshot().Where(static r => r.Id.Id == SwitchFailedEventId).ShouldHaveSingleItem();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Bob) is not null);
    }

    [Fact]
    public async Task ScopeSwitch_UnscopedKey_GetsNoFinalSnapshot()
    {
        // (non-normative) §11.5: the final snapshot goes to scoped keys only. An unscoped key has no old scope to close: its
        // writer writes the current state, never the state of the reset.
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        await h.InitializeAsync();
        var held = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.OnSet = _ => held.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct); // bounded: a failed test never hangs in dispose
        h.Store.Dispatch(new SetDial(8));
        await h.WriteArrivedAsync();
        h.Store.Dispatch(new SetDial(9));
        await WriterHarness.Until(() => h.Store.State.Get<Dial>() == new Dial(9));

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Store.Dispatch(new SetDial(10));
        await WriterHarness.Until(() => h.Store.State.Get<Dial>() == new Dial(10));
        h.OnSet = static _ => true;
        held.SetResult(true);
        await WriterHarness.Until(() => !h.Middleware.Writers["dial"].Dirty);

        h.Written.Select(static w => w.Payload).ShouldBe(["""{"Value":8}""", """{"Value":10}"""]);
    }

    [Fact]
    public async Task ScopeSwitch_NothingScoped_NoSubscription()
    {
        // (non-normative) §11.6: no scope in use, so nothing follows the user: unscoped browser keys on the server, with a
        // provider registered, are neither reset nor read again.
        await using var h = new WriterHarness(
            static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.KeyPrefix = "app"),
            browser: false,
            services: s => s.AddSingleton<AuthenticationStateProvider>(_auth));
        await h.InitializeAsync();
        h.Store.Dispatch(new SetDial(8));
        await h.NextWriteAsync();

        _auth.Change("bob");
        await h.Store.WhenIdleAsync(Ct);

        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        h.Store.State.Get<Dial>().ShouldBe(new Dial(8));
        h.Js.Calls.Count(static c => c.Identifier == "storageGet").ShouldBe(1);
        _auth.Subscribers.ShouldBe(0);
    }

    [Fact]
    public async Task ScopeSwitch_DisposeDuringRead_AttemptEndsWithoutTerminal()
    {
        // (non-normative) Dispose phase 5a: the attempt's token ends its read and it issues nothing.
        await using var h = Circuit();
        await h.InitializeAsync();
        _cache.Hold($"get:{Alice}");
        _auth.Change("alice");
        await WriterHarness.Until(() => _cache.Attempts.Count(static c => c == $"get:{Alice}") == 2);

        await h.DisposeStoreAsync();

        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("completed:", StringComparison.Ordinal) && e.Contains(":1:", StringComparison.Ordinal));
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
    }

    [Fact]
    public async Task ScopeSwitch_ChangeDuringDisposeFlush_Unsubscribed_NoEpochTaken()
    {
        // (non-normative) Dispose phase 5a unsubscribes before the flush. Raised during the flush, while the handler could still
        // run to its epoch (after it, a subscribed handler only throws on the disposed token), the change takes none.
        await using var h = Circuit();
        await h.InitializeAsync();
        _auth.Subscribers.ShouldBe(1);
        var held = _cache.Hold($"set:{Alice}");
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Attempts.Contains($"set:{Alice}"));
        var dispose = h.DisposeStoreAsync();
        await WriterHarness.Until(() => h.Middleware.Writers["counter"].Flushing);
        _auth.Subscribers.ShouldBe(0);

        _auth.Change("bob");

        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(0);
        held.SetResult();
        await dispose;
    }

    [Fact]
    public async Task ScopeSwitch_NotificationInFlightAtDispose_TakesNoEpoch()
    {
        // (non-normative) §11.6 "never escapes": a notification whose invocation list was taken before dispose unsubscribed
        // still reaches the handler, here while the flush writes. It takes no epoch and touches no token source (dispose
        // disposes the switches' after the flush, where the same handler would otherwise fault unobserved).
        WriterHarness? harness = null;
        Task? dispose = null;
        var flushWrite = _cache.Hold($"set:{Alice}");
        _auth.AuthenticationStateChanged += _ =>
        {
            // Called before the store's handler, on this thread: dispose unsubscribes it, then flushes the debounced change.
            dispose = harness!.DisposeStoreAsync();
            _cache.GetBegan($"set:{Alice}").Wait(TimeSpan.FromSeconds(10), Ct).ShouldBeTrue();
        };
        await using var h = Circuit(debounce: _debounce); // subscribes after the handler above, so it is called after it
        harness = h;
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await h.Time.TimerAsync(_debounce);

        _auth.Change("bob");

        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(0);
        flushWrite.SetResult();
        await dispose.ShouldNotBeNull();
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
        h.Store.State.Get<PersistenceState>().ScopeEpoch.ShouldBe(0);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Exception != null);
    }

    [Fact]
    public async Task ScopeSwitch_FinalSnapshotOfUnreadScope_NotWrittenOverItsStoredData()
    {
        // (non-normative) §11.5: the hydration skip applies to the snapshot. A change committed while Bob's read is in flight
        // ends in a snapshot of an epoch whose key was never read: writing it would overwrite Bob's stored data.
        _cache.Store(Bob, Envelope(50));
        await using var h = Circuit();
        await h.InitializeAsync();
        _cache.HoldUntilCancelled($"get:{Bob}");

        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        h.Store.Dispatch(new Increment());
        await h.Store.WhenIdleAsync(Ct);
        _auth.Change("carol");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated } && !h.Middleware.Writers["counter"].Dirty);

        _cache.Attempts.ShouldNotContain($"set:{Bob}");
        _cache.Stored(Bob).ShouldBe(Envelope(50));
        _cache.Stored(Carol).ShouldBeNull();
    }

    [Fact]
    public async Task ScopeSwitch_ToAnonymous_FinalWakesItsLoop_NothingLostAtDispose()
    {
        // (non-normative) A sign-out has no read to wake the key's loop, and no change follows: the final snapshot alone must, or
        // the key stays dirty and dispose counts a false ducky.persistence.lost.
        await using var h = Circuit();
        await h.InitializeAsync();
        using var lost = new Microsoft.Extensions.Diagnostics.Metrics.Testing.MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");

        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        await h.DisposeStoreAsync();

        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task ScopeSwitch_ScopedKeyDebounced_ReadWaitsOutTheDebounce()
    {
        // (non-normative) §11.5 loop order: wait for a signal, debounce, then the commands. A read posted while the loop is in
        // the debounce of a change waits for it, behind the old key's final write; only a backoff ends early (INV-16).
        _cache.Store(Bob, Envelope(5));
        await using var h = Circuit(debounce: _debounce);
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await h.Time.TimerAsync(_debounce);

        _auth.Change("bob");
        await WriterHarness.Until(() => h.Middleware.Writers["counter"].ReadPending);

        _cache.Attempts.ShouldBe([$"get:{Alice}"]);
        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrating);
        h.Time.Advance(_debounce);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ProviderCannotBeResolved_NoSubscription_HydrationGoesOn()
    {
        // (non-normative) The resolution runs inside a component's Select in the browser, so it must not throw (§6.10): a
        // provider whose factory throws is an Error log, and with an explicit Scope the store simply never hears of a change.
        await using var h = new WriterHarness(
            static d => d.Persist<CounterSlice>(_server).AddBlazor(static o => (o.KeyPrefix, o.Scope) = ("app", static (_, _) => ValueTask.FromResult<string?>("alice"))),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(_cache)
                .AddScoped<AuthenticationStateProvider>(static _ => throw new InvalidOperationException("no provider"))
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs))));

        await h.InitializeAsync();

        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrated);
        var error = _logs.GetSnapshot().Where(static r => r.Id.Id == SubscribeFailedEventId).ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task ScopeSwitch_ExplicitScopeWithoutProvider_NoSubscription_NoErrorLogged()
    {
        // (non-normative) An explicit Scope needs no AuthenticationStateProvider: with none registered, scoped keys hydrate,
        // nothing subscribes and nothing is logged as failed, at init or at dispose.
        await using (var h = new WriterHarness(
            static d => d.Persist<CounterSlice>(_server).AddBlazor(static o => (o.KeyPrefix, o.Scope) = ("app", static (_, _) => ValueTask.FromResult<string?>("alice"))),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(_cache)
                .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs)))))
        {
            await h.InitializeAsync();

            Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrated);
            _cache.Attempts.ShouldBe([$"get:{Alice}"]);
        }

        _logs.GetSnapshot().ShouldNotContain(static r => r.Exception != null || r.Level >= LogLevel.Error);
    }

    [Fact]
    public async Task ScopeSwitch_InitDeadlineEndsSwitchAttempt_HungReadFreesWriterLoop()
    {
        // (non-normative) §11.5 "A failure leaves persistence running": the init-phase deadline ends the current switch attempt
        // and cancels its token, so a read that honours it frees the key's loop and a later change is written.
        await using var h = Circuit();
        _cache.HoldUntilCancelled($"get:{Alice}");
        _cache.HoldUntilCancelled($"get:{Bob}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));

        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });
        h.Store.Dispatch(new Increment());

        await WriterHarness.Until(() => _cache.Stored(Bob) is not null);
        Payload(_cache.Stored(Bob)!).ShouldBe("""{"Value":1}""");
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
        h.Log.Entries.Count(static e => e.StartsWith("failed:", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ScopeSwitch_InitDeadlineEndsSwitchAttempt_NoFurtherReads()
    {
        // (non-normative) §11.6 "no longer current, stop", for an attempt that already ended: its read is abandoned at the
        // deadline, even from a provider that ignores its token, and the attempt reads no other key.
        await using var h = Circuit(static d => d.Persist<DialSlice>(_server));
        _cache.HoldUntilCancelled("get:app:alice:dial");
        var read = _cache.HoldIgnoringToken("get:app:bob:dial");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains("get:app:alice:dial"));
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains("get:app:bob:dial"));

        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));

        _cache.Attempts.ShouldBe(["get:app:alice:dial", "get:app:bob:dial"]);
        read.SetResult();
    }

    [Fact]
    public async Task ScopeSwitch_SupersededInitialAttemptsReadFails_Discarded()
    {
        // (non-normative) Attempt 0's token is the init deadline's, which a switch does not cancel: its read failing after the
        // switch reaches the claim and loses it, a Debug discard and no failure of the attempt that is current.
        await using var h = Circuit();
        var alice = _cache.Hold($"get:{Alice}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        _auth.Change("bob");
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });

        alice.SetException(new TimeoutException("late"));

        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
        Persistence(h).Status.ShouldBe(PersistenceStatus.Hydrated);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static PersistenceState Persistence(WriterHarness h) => h.Store.State.Get<PersistenceState>();

    private static string Payload(string envelope) => System.Text.Json.JsonDocument.Parse(envelope).RootElement.GetProperty("s").GetRawText();

    private static string Envelope(int value) => EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    // The scope set before each notification, however often the delegate is called for it: on the server it runs on the
    // notifying thread, before any await, and §11.6's re-assertion check calls it once more ahead of the switch.
    private void ScopeFromField(DuckyBuilder d) => d.AddBlazor(o => o.Scope = (_, _) => new(_scope));

    private WriterHarness Circuit(Action<DuckyBuilder>? more = null, TimeSpan? debounce = null) => new(
        d =>
        {
            d.Persist<CounterSlice>(o => (o.Storage, o.Debounce) = (PersistStorage.Server, debounce)).AddBlazor(static o => o.KeyPrefix = "app");
            more?.Invoke(d);
        },
        browser: false,
        services: s => s.AddSingleton<IDistributedCache>(_cache)
            .AddSingleton<AuthenticationStateProvider>(_auth)
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs))));

    public void Dispose() => _cache.Dispose();
}

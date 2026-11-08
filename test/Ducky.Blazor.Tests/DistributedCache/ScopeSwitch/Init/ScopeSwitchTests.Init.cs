using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.5 step 7 and "Before StoreInitialized", §11.6 step 1: a switch during init and the initial hydration (INV-14,
// INV-17). It carries the unscoped keys its superseded attempt did not restore, and it has exactly one live deadline: the
// init-phase one while _initPhase is set, its own HydrationTimeout once the init phase has ended.
public sealed partial class ScopeSwitchTests
{
    private const int DispatchAfterDisposeEventId = 1002;
    private static readonly TimeSpan _tick = TimeSpan.FromTicks(1);

    [Fact]
    public async Task ScopeSwitch_AuthChangeDuringInitialScopeResolution_NeverWritesUnderOldScope()
    {
        // Subscribed before the epoch-0 resolution starts: a sign-in change while the provider still resolves the old user is a
        // switch that supersedes attempt 0. The old user's late scope is never recorded, nothing is read or written under its
        // key, and a later change goes to the new user's key.
        _cache.Store(Alice, Envelope(3));
        _cache.Store(Bob, Envelope(5));
        var alice = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var h = Circuit();
        _auth.Gate = alice.Task;
        var init = h.InitializeAsync();
        _auth.Gate = null;

        _auth.Change("bob");
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        alice.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        h.Store.Dispatch(new Increment());

        await WriterHarness.Until(() => _cache.Stored(Bob) is { } stored && Payload(stored) == """{"Value":6}""");
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [1] = "bob" });
        _cache.Attempts.ShouldBe([$"get:{Bob}", $"set:{Bob}"]);
        _cache.Stored(Alice).ShouldBe(Envelope(3));
    }

    [Fact]
    public async Task ScopeSwitch_DuringInit_TerminalPrecedesStoreInitialized_WithinOneHydrationTimeout()
    {
        // HydrationTimeout 8 s, a switch at 4 s whose read hangs: the switch arms no timer of its own, and the init-phase
        // deadline ends the attempt current when it fires, at 8 s, so its terminal comes before StoreInitialized.
        await using var h = Circuit(static d => d.AddBlazor(static o => o.HydrationTimeout = TimeSpan.FromSeconds(8)));
        _cache.HoldUntilCancelled($"get:{Alice}");
        _cache.HoldUntilCancelled($"get:{Bob}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        h.Time.Advance(TimeSpan.FromSeconds(4));
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        h.Time.DueTimes.Count(static d => d == TimeSpan.FromSeconds(8)).ShouldBe(1); // the init-phase deadline's alone

        h.Time.Advance(TimeSpan.FromSeconds(4) - _tick);
        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 1 });
        init.IsCompleted.ShouldBeFalse();
        h.Time.Advance(_tick);
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => h.Log.Entries.Any(static e => e.StartsWith("initialized ", StringComparison.Ordinal)));

        h.Log.Entries.Where(static e => e.StartsWith("failed:", StringComparison.Ordinal) || e.StartsWith("initialized ", StringComparison.Ordinal))
            .ShouldBe(["failed:TimeoutException:1:System Failed:1", "initialized Failed:1"]);
        h.Log.Terminals.ShouldHaveSingleItem().ShouldBe(new HydrationFailed(nameof(TimeoutException), "Hydration did not end within HydrationTimeout (00:00:08).") { ScopeEpoch = 1 });

        // Exactly one terminal: nothing more when a timer armed at the switch would have fired.
        h.Time.Advance(TimeSpan.FromSeconds(10));
        h.Log.Entries.Count(static e => e.StartsWith("failed:", StringComparison.Ordinal) || e.StartsWith("completed:", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task ScopeSwitch_DuringInit_InitAborted_FailedForCurrentEpoch()
    {
        // Init aborted (an init-buffer overflow) while a switch made during init reads: the init-token callback ends the
        // attempt current then, epoch 1, with an aborted HydrationFailed before StoreInitialized (§11.5 step 7, §6.7).
        await using var h = Circuit(static d => d.InitBufferCapacity = 1);
        _cache.HoldUntilCancelled($"get:{Alice}");
        _cache.HoldUntilCancelled($"get:{Bob}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));

        h.Store.Dispatch(new Increment());
        h.Store.Dispatch(new Increment());
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => h.Log.Entries.Any(static e => e.StartsWith("initialized ", StringComparison.Ordinal)));

        h.Log.Entries.Where(static e => e.StartsWith("failed:", StringComparison.Ordinal) || e.StartsWith("initialized ", StringComparison.Ordinal))
            .ShouldBe(["failed:OperationCanceledException:1:System Failed:1", "initialized Failed:1"]);
        h.Log.Terminals.ShouldHaveSingleItem().ShouldBe(new HydrationFailed(nameof(OperationCanceledException), "Store init was aborted before hydration ended.") { ScopeEpoch = 1 });
    }

    [Fact]
    public async Task ScopeSwitch_DuringInitialHydration_UnscopedKeysStillRestored()
    {
        // A switch while attempt 0 reads carries the unscoped key attempt 0 had not restored: the switch reads it again, so
        // the unscoped Local slice next to the NameIdentifier-scoped Server slice is restored. The old user's slow read is
        // discarded, and the baselines leave nothing to write.
        _cache.Store(Alice, Envelope(3));
        _cache.Store(Bob, Envelope(5));
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        h.Storage[("local", "app:dial")] = EnvelopeWriter.Write("""{"Value":4}""", 1, h.Time.GetUtcNow());
        var aliceRead = _cache.Hold($"get:{Alice}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));

        _auth.Change("bob");
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });

        h.Store.State.Get<Dial>().ShouldBe(new Dial(4));
        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        h.Log.Entries.ShouldContain("completed:True:1:System Hydrated:1");
        h.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial", "app:dial"]);
        aliceRead.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        await h.IterationsAsync("dial", 1);
        h.Written.ShouldBeEmpty();
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_ToAnonymousDuringInitialHydration_UnscopedKeysStillRestored()
    {
        // A sign-out during the initial hydration drops only the scoped keys: the carried unscoped key is still read (through
        // its writer loop) and restored, and its writer, which deferred a change while Hydrating, gets the read's baseline, so
        // it never writes in-memory state over the stored value. No I/O under any scoped key after the switch.
        await using var h = Circuit(static d => d.Persist<DialSlice>(static o => o.Debounce = TimeSpan.Zero));
        h.Storage[("local", "app:dial")] = EnvelopeWriter.Write("""{"Value":4}""", 1, h.Time.GetUtcNow());
        var aliceRead = _cache.Hold($"get:{Alice}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        h.Line.Send!(new SetDial(8), false);
        await WriterHarness.Until(() => h.Middleware.Deferred.ContainsKey("dial"));

        _auth.Change(null);
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });

        h.Store.State.Get<Dial>().ShouldBe(new Dial(4));
        h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        h.Log.Entries.ShouldContain("completed:True:1:System Hydrated:1");
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice", [1] = null });
        h.Js.Calls.Where(static c => c.Identifier == "storageGet").Select(static c => c.Args[1]).ShouldBe(["app:dial", "app:dial"]);
        await WriterHarness.Until(() => h.Middleware.Deferred.IsEmpty && !h.Middleware.Writers["dial"].Dirty);
        h.Written.ShouldBeEmpty();
        Payload(h.Storage[("local", "app:dial")]).ShouldBe("""{"Value":4}""");
        aliceRead.SetResult();
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        _cache.Attempts.ShouldBe([$"get:{Alice}"]);
    }

    [Fact]
    public async Task ScopeSwitch_JustAfterInitialTerminal_OwnTimerArmed_NotFailedByInitDeadline()
    {
        // The initial terminal at 4.9 s ends the init phase and disarms its deadline; a switch just after it arms its own
        // HydrationTimeout. At 5 s nothing fails, and a 3 s read completes at 7.9 s inside the switch's own 5 s.
        await using var h = Circuit();
        var aliceRead = _cache.Hold($"get:{Alice}");
        var init = h.InitializeAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5)); // the init-phase deadline's
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        h.Time.Advance(TimeSpan.FromSeconds(4.9));
        aliceRead.SetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var bobRead = _cache.Hold($"get:{Bob}");

        _auth.Change("bob");
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5)); // the switch's own
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        h.Time.Advance(TimeSpan.FromSeconds(0.1));
        h.Time.Advance(TimeSpan.FromSeconds(2.9));
        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 1 });
        bobRead.SetResult();

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Log.Entries.ShouldContain("completed:False:1:System Hydrated:1");
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScopeSwitch_BetweenInitialTerminalAndInitReturn_HungRead_FailsAtOwnTimeout()
    {
        // A switch raised inside the drain of the initial terminal's commit (at 2 s), before init returns: the init phase ended
        // with that terminal, so the switch arms its own timer, and its hung read ends in HydrationFailed at its own 5 s, at
        // 7 s, not at the init-phase deadline's 5 s. The end cancels the read, which frees the key's loop: a later change is
        // written under the new key.
        await using var h = Circuit();
        var aliceRead = _cache.Hold($"get:{Alice}");
        _cache.HoldUntilCancelled($"get:{Bob}");
        var switched = false;
        h.Log.OnEntry = entry =>
        {
            if (!switched && entry.StartsWith("completed:False:0:", StringComparison.Ordinal))
            {
                switched = true;
                _auth.Change("bob");
            }
        };
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        h.Time.Advance(TimeSpan.FromSeconds(2));
        aliceRead.SetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        h.Log.Entries.ShouldContain("initialized Hydrating:1");

        h.Time.Advance(TimeSpan.FromSeconds(5) - _tick);
        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 1 });
        h.Time.Advance(_tick);

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Failed });
        h.Log.Entries.ShouldContain("failed:TimeoutException:1:System Failed:1");
        h.Log.Terminals.Last().ShouldBe(new HydrationFailed(nameof(TimeoutException), "Hydration did not end within HydrationTimeout (00:00:05).") { ScopeEpoch = 1 });
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Bob) is not null);
        Payload(_cache.Stored(Bob)!).ShouldBe("""{"Value":1}""");
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == SwitchFailedEventId);
    }

    [Fact]
    public async Task ScopeSwitch_SupersededAttemptsOwnTimer_Discarded()
    {
        // (non-normative) §11.5 step 7: a superseded attempt's own timer is discarded when it reaches _issue (its scope delegate
        // ignores the token, so the attempt is still running and its timer still live at 5 s), and the attempt now current
        // keeps its full HydrationTimeout and ends at its own deadline, with exactly one terminal.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        var bob = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = bob.Task;
        _auth.Change("bob");
        h.Time.Advance(TimeSpan.FromSeconds(2));
        _cache.HoldUntilCancelled($"get:{Carol}");
        _scope = Task.FromResult<string?>("carol");
        _auth.Change("carol");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Carol}"));

        h.Time.Advance(TimeSpan.FromSeconds(5) - _tick);
        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrating) { ScopeEpoch = 2 });
        h.Time.Advance(_tick);

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Failed });
        h.Log.Entries.Where(static e => e.StartsWith("failed:", StringComparison.Ordinal)).ShouldBe(["failed:TimeoutException:2:System Failed:2"]);
        bob.SetResult("bob");
        await WriterHarness.Until(() => _logs.GetSnapshot().Any(static r => r.Id.Id == DiscardedEventId));
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice", [2] = "carol" });
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Carol}"]);
    }

    [Fact]
    public async Task ScopeSwitch_OwnTimerAfterDispose_IssuesNothing()
    {
        // (non-normative) A switch whose scope delegate ignores its token is still current and open when the store is disposed:
        // its own deadline, still armed, issues no terminal after DisposeAsync, not even one the disposed store would drop.
        await using var h = Circuit(ScopeFromField);
        await h.InitializeAsync();
        _scope = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
        _auth.Change("bob");
        h.Time.DueTimes.Count(static d => d == TimeSpan.FromSeconds(5)).ShouldBe(2); // the init-phase deadline's, the switch's own

        await h.DisposeStoreAsync();
        h.Time.Advance(TimeSpan.FromSeconds(5));

        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("failed:", StringComparison.Ordinal));
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == DispatchAfterDisposeEventId && r.Message.Contains(nameof(HydrationFailed), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ScopeSwitch_CompletedAttemptsOwnTimer_NoSecondTerminal()
    {
        // (non-normative) Exactly one terminal per attempt: once a switch attempt has completed, its own deadline does nothing.
        // The deadline fires inside the drain of the completion terminal's commit, while the switch still holds its timer (the
        // switch disposes it only once HydrateAsync returns): the attempt has completed, and the timer finds it ended.
        _cache.Store(Bob, Envelope(5));
        await using var h = Circuit();
        await h.InitializeAsync();
        var fired = false;
        h.Log.OnEntry = entry =>
        {
            if (!fired && entry.StartsWith("completed:True:1:", StringComparison.Ordinal))
            {
                fired = true;
                h.Time.Advance(TimeSpan.FromSeconds(5));
            }
        };

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Time.Advance(TimeSpan.FromSeconds(10));
        fired.ShouldBeTrue();

        h.Log.Entries.Where(static e => e.StartsWith("failed:", StringComparison.Ordinal) || e.StartsWith("completed:", StringComparison.Ordinal))
            .ShouldBe(["completed:False:0:System Hydrated:0", "completed:True:1:System Hydrated:1"]);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
    }
}

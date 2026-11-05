using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.Extensions.Logging;

namespace Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;

// SPEC §11.5 "A command ends the backoff sleep" at a switch, and §11.6 "Re-assertion check"; INV-16, INV-17.
public sealed partial class ScopeSwitchTests
{
    private const int ReassertedEventId = 2045;

    [Fact]
    public async Task ScopeSwitch_WriterInRetryBackoff_SwitchReadCompletesWithinHydrationTimeout()
    {
        // Writes fail until the backoff reaches 30 s (a circuit disconnect), then the provider heals and the user changes. The
        // final old-scope snapshot ends the sleep: it is written at once, the new scope's value restores and the attempt ends,
        // all before the clock moves, so far inside its 5 s HydrationTimeout.
        _cache.Store(Bob, Envelope(9));
        await using var h = Circuit();
        await h.InitializeAsync();
        for (var i = 0; i < 6; i++)
        {
            _cache.Fail($"set:{Alice}");
        }

        h.Store.Dispatch(new Increment());
        foreach (var seconds in new[] { 1, 2, 4, 8, 16 })
        {
            await h.Time.TimerAsync(TimeSpan.FromSeconds(seconds));
            h.Time.Advance(TimeSpan.FromSeconds(seconds));
        }

        await h.Time.TimerAsync(TimeSpan.FromSeconds(30));
        _cache.Stored(Alice).ShouldBeNull();
        var at = h.Time.GetUtcNow();

        _auth.Change("bob");

        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Time.GetUtcNow().ShouldBe(at);
        h.Log.Entries.ShouldContain("completed:True:1:System Hydrated:1");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(9));
        Payload(_cache.Stored(Alice)!).ShouldBe("""{"Value":1}""");
        _cache.Attempts.ShouldBe([$"get:{Alice}", .. Enumerable.Repeat($"set:{Alice}", 7), $"get:{Bob}"]);
        await WriterHarness.Until(() => !h.Middleware.Writers["counter"].Dirty);
        _cache.Attempts.ShouldNotContain($"set:{Bob}");
    }

    [Fact]
    public async Task ScopeSwitch_CircuitReconnectSameUser_NoResetNoReRead()
    {
        // Every reconnect re-notifies the same user, whose scope resolves synchronously, after the attempt ended: absorbed. No
        // epoch, no reset (the slice keeps its value, no Hydrating restore), no read, one Debug log. A real change still switches.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit();
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => _cache.Stored(Alice) is { } stored && Payload(stored) == """{"Value":4}""");

        _auth.Change("alice");

        Persistence(h).ShouldBe(new PersistenceState(PersistenceStatus.Hydrated));
        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(0);
        h.Store.State.Get<Counter>().ShouldBe(new Counter(4));
        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("restore:", StringComparison.Ordinal) && e.EndsWith("Hydrating:1", StringComparison.Ordinal));
        var absorbed = _logs.GetSnapshot().Where(static r => r.Id.Id == ReassertedEventId).ShouldHaveSingleItem();
        absorbed.Level.ShouldBe(LogLevel.Debug);
        absorbed.Message.ShouldContain("epoch 0");
        await h.Store.WhenIdleAsync(Ct);
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}"]);

        _auth.Change("bob");
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"set:{Alice}", $"get:{Bob}"]);
    }

    [Fact]
    public async Task ScopeSwitch_AnonymousReconnect_Absorbed_UnknownScopeIsNot()
    {
        // (non-normative) A null scope recorded for the epoch is a known scope: the same anonymous user re-notified is absorbed,
        // a whitespace result included (it is null). An epoch whose scope never resolved (its terminal a timeout) is unknown, so
        // a null result there is no re-assertion, and neither is an incomplete result against a recorded null.
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = pending.Task;
        await using var h = Circuit(ScopeFromField);
        var init = h.InitializeAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(5));
        h.Time.Advance(TimeSpan.FromSeconds(5));
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        Persistence(h).Status.ShouldBe(PersistenceStatus.Failed);
        h.Middleware.Scopes.ShouldBeEmpty();

        _scope = Task.FromResult<string?>(null);
        _auth.Change(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [1] = null });

        _scope = Task.FromResult<string?>(" ");
        _auth.Change(null);

        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(1);
        _logs.GetSnapshot().Where(static r => r.Id.Id == ReassertedEventId).ShouldHaveSingleItem().Message.ShouldContain("epoch 1");

        // An incomplete result is none, even against a recorded null scope: the switch runs.
        var later = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scope = later.Task;
        _auth.Change(null);
        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(2);
        later.SetResult(null);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [1] = null, [2] = null });

        // Releases attempt 0's abandoned delegate before dispose; nothing is asserted on its late result.
        pending.SetResult("alice");
    }

    [Fact]
    public async Task ScopeSwitch_SameUserWhileAttemptInFlight_RunsTheSwitch()
    {
        // (non-normative) The scope is recorded but the attempt's read is still in flight: no terminal yet, so the same user
        // re-notified is a switch, which supersedes the read and reads the key again through its writer.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit();
        var held = _cache.Hold($"get:{Alice}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        h.Middleware.Scopes.ShouldBe(new Dictionary<int, string?> { [0] = "alice" });

        _auth.Change("alice");

        h.Middleware.Current.ShouldNotBeNull().Epoch.ShouldBe(1);
        held.SetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        h.Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Alice}"]);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == ReassertedEventId);
    }

    [Fact]
    public async Task ScopeSwitch_SameUserFromWinnersRestore_ClaimedNotIssued_RunsTheSwitch()
    {
        // (non-normative) Re-notified by a subscriber of the winner's own restore, on its thread: the terminal is claimed but not
        // issued yet, so this is no re-assertion. The switch supersedes the winner, which issues nothing.
        _cache.Store(Alice, Envelope(3));
        await using var h = Circuit();
        h.Log.OnEntry = entry =>
        {
            if (entry == "restore:counter Hydrating:0")
            {
                _auth.Change("alice");
            }
        };

        await h.InitializeAsync().WaitAsync(TimeSpan.FromSeconds(10), Ct);

        h.Log.Entries.ShouldNotContain(static e => e.StartsWith("completed:True:0", StringComparison.Ordinal));
        h.Log.Entries.ShouldContain("completed:True:1:System Hydrated:1");
        h.Store.State.Get<Counter>().ShouldBe(new Counter(3));
        _cache.Attempts.ShouldBe([$"get:{Alice}", $"get:{Alice}"]);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == ReassertedEventId);
    }

    [Fact]
    public async Task ScopeSwitch_ReassertionCheckFaults_SwitchRunsAndNothingEscapes()
    {
        // (non-normative) A delegate that throws, or returns a faulted task, is no result of the check: the switch runs, and its
        // own resolution meets the same failure (HydrationFailed). Nothing escapes into the circuit.
        var calls = 0;
        await using var h = Circuit(d => d.AddBlazor(o => o.Scope = (_, _) => (++calls) switch
        {
            1 => ValueTask.FromResult<string?>("alice"),
            2 => throw new InvalidOperationException("no scope"),
            3 => ValueTask.FromResult<string?>("alice"),
            _ => ValueTask.FromException<string?>(new InvalidOperationException("faulted")),
        }));
        await h.InitializeAsync();

        Should.NotThrow(() => _auth.Change("alice"));
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        Should.NotThrow(() => _auth.Change("alice"));
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Failed });

        h.Log.Entries.ShouldContain("failed:InvalidOperationException:2:System Failed:2");
        calls.ShouldBe(5);
        _logs.GetSnapshot().ShouldNotContain(static r => r.Id.Id == ReassertedEventId);
    }
}

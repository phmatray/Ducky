using System.Collections.Concurrent;
using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.DistributedCache.ScopeSwitch;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Blazor.Tests.DistributedCache.Races;

// SPEC §11.6 "Scope switch" step 1, §17.6: INV-14, INV-17. The deterministic twins of the two SampleParallel races of
// §17.3 row 6 (ScopeSwitchInterleavingTests in Ducky.Concurrency.Tests): each ordering of the superseded attempt's read
// against the switch that supersedes it, driven through the gated cache. Whatever the order, the old user's value is
// restored only under its own epoch, the newest attempt restores its own user's value, and nothing is ever written: every
// key holds what was read from it. A circuit store over the real MemoryDistributedCache.
public sealed class ScopeSwitchRaceTests : IDisposable
{
    private const int DiscardedEventId = 2041;
    private const string Alice = "app:alice:counter";
    private const string Bob = "app:bob:counter";
    private const string Carol = "app:carol:counter";
    private readonly FakeLogCollector _logs = new();
    private readonly GatedCache _cache = new();
    private readonly SwitchingAuth _auth = new("alice");
    private readonly CounterRestores _restores = new();
    private readonly HookedTime _time = new();

    public enum Order
    {
        /// <summary>The read lands and its attempt ends before the switch.</summary>
        ReadThenSwitch,

        /// <summary>The switch first: its supersession ends the read through the attempt's token.</summary>
        SwitchThenRead,

        /// <summary>The switch first, the provider ignoring the token: the read lands after the switch, or during its read.</summary>
        SwitchThenLateRead,

        /// <summary>The read lands, then the switch supersedes its attempt before the attempt reaches its claim.</summary>
        ReadLandsThenSwitchBeforeClaim,
    }

    [Theory]
    [InlineData(Order.ReadThenSwitch)]
    [InlineData(Order.SwitchThenRead)]
    [InlineData(Order.SwitchThenLateRead)]
    [InlineData(Order.ReadLandsThenSwitchBeforeClaim)]
    public async Task DistributedCache_StaleAttemptAfterSecondSwitch_NeverRestoredOrWrittenUnderNewScope_Deterministic(Order order)
    {
        StoreValues();
        await using var h = Circuit();
        await h.InitializeAsync();
        var bobRead = order == Order.SwitchThenLateRead ? _cache.HoldIgnoringToken($"get:{Bob}") : _cache.Hold($"get:{Bob}");
        _auth.Change("bob");
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        if (order == Order.ReadThenSwitch)
        {
            bobRead.SetResult();
            await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        }
        else if (order == Order.ReadLandsThenSwitchBeforeClaim)
        {
            // The middleware reads the clock right after the read landed, outside _issue: the switch comes there, so Bob's
            // result is discarded at its claim (§11.6 step 1), not through its token.
            _time.OnNextNow(() => _auth.Change("carol"));
            bobRead.SetResult();
            await WriterHarness.Until(() => Discards == 1);
        }

        if (order != Order.ReadLandsThenSwitchBeforeClaim)
        {
            _auth.Change("carol");
        }

        if (order is Order.SwitchThenRead or Order.SwitchThenLateRead)
        {
            // Bob's attempt is discarded once its token ends its wait for the read, before the read is released.
            await WriterHarness.Until(() => Discards == 1);
        }

        if (order == Order.SwitchThenLateRead)
        {
            // A provider ignoring its token holds the key's loop: Carol's read waits behind Bob's.
            _cache.Attempts.ShouldNotContain($"get:{Carol}");
            (Persistence(h).Status, Persistence(h).ScopeEpoch).ShouldBe((PersistenceStatus.Hydrating, 2));
        }

        bobRead.TrySetResult();
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 2, Status: PersistenceStatus.Hydrated });
        await h.DisposeStoreAsync();

        h.Store.State.Get<Counter>().ShouldBe(new Counter(7));
        _restores.Entries.ShouldBe(order == Order.ReadThenSwitch ? ["3:0", "0:1", "5:1", "0:2", "7:2"] : ["3:0", "0:1", "7:2"]);
        Terminals(h).ShouldBe(order == Order.ReadThenSwitch ? ["completed:True:0", "completed:True:1", "completed:True:2"] : ["completed:True:0", "completed:True:2"]);
        Discards.ShouldBe(order == Order.ReadThenSwitch ? 0 : 1);
        NothingWritten();
    }

    [Theory]
    [InlineData(Order.ReadThenSwitch)]
    [InlineData(Order.SwitchThenRead)]
    [InlineData(Order.SwitchThenLateRead)]
    public async Task DistributedCache_UserChangeDuringInitialHydration_OldScopeValuesNeverRestored_Deterministic(Order order)
    {
        // Attempt 0's token is the init deadline's, which a switch does not cancel: its read always lands, and a superseded
        // one is discarded at its claim, after the new attempt ended (SwitchThenRead) or while it reads (SwitchThenLateRead).
        StoreValues();
        await using var h = Circuit();
        var aliceRead = _cache.Hold($"get:{Alice}");
        var bobRead = _cache.Hold($"get:{Bob}");
        var init = h.InitializeAsync();
        await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Alice}"));
        if (order == Order.ReadThenSwitch)
        {
            aliceRead.SetResult();
            await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        }

        _auth.Change("bob");
        if (order == Order.SwitchThenRead)
        {
            bobRead.SetResult();
            await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        }
        else if (order == Order.SwitchThenLateRead)
        {
            await WriterHarness.Until(() => _cache.Attempts.Contains($"get:{Bob}"));
        }

        aliceRead.TrySetResult();
        if (order != Order.ReadThenSwitch)
        {
            await WriterHarness.Until(() => Discards == 1);
        }

        if (order == Order.SwitchThenLateRead)
        {
            (Persistence(h).Status, Persistence(h).ScopeEpoch).ShouldBe((PersistenceStatus.Hydrating, 1));
            h.Store.State.Get<Counter>().ShouldBe(new Counter(0));
        }

        bobRead.TrySetResult();
        await init.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await WriterHarness.Until(() => Persistence(h) is { ScopeEpoch: 1, Status: PersistenceStatus.Hydrated });
        await h.DisposeStoreAsync();

        h.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        _restores.Entries.ShouldBe(order == Order.ReadThenSwitch ? ["3:0", "0:1", "5:1"] : ["5:1"]);
        Terminals(h).ShouldBe(order == Order.ReadThenSwitch ? ["completed:True:0", "completed:True:1"] : ["completed:True:1"]);
        h.Log.Entries.ShouldContain(order == Order.ReadThenSwitch ? "initialized Hydrated:0" : "initialized Hydrated:1");
        Discards.ShouldBe(order == Order.ReadThenSwitch ? 0 : 1);
        NothingWritten();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private int Discards => _logs.GetSnapshot().Count(static r => r.Id.Id == DiscardedEventId);

    private static PersistenceState Persistence(WriterHarness h) => h.Store.State.Get<PersistenceState>();

    private static List<string> Terminals(WriterHarness h) => [.. h.Log.Entries
        .Where(static e => e.StartsWith("completed:", StringComparison.Ordinal) || e.StartsWith("failed:", StringComparison.Ordinal))
        .Select(static e => string.Join(':', e.Split(':')[..3]))];

    private static string Envelope(int value) => EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));

    private void StoreValues()
    {
        _cache.Store(Alice, Envelope(3));
        _cache.Store(Bob, Envelope(5));
        _cache.Store(Carol, Envelope(7));
    }

    // After the store's dispose flush, so a late write would be seen: no write was ever attempted, and every key still
    // holds its own user's value.
    private void NothingWritten()
    {
        _cache.Attempts.ShouldNotContain(static c => c.StartsWith("set:", StringComparison.Ordinal));
        (Payload(Alice), Payload(Bob), Payload(Carol)).ShouldBe(("""{"Value":3}""", """{"Value":5}""", """{"Value":7}"""));
    }

    private string Payload(string key) => System.Text.Json.JsonDocument.Parse(_cache.Stored(key)!).RootElement.GetProperty("s").GetRawText();

    private WriterHarness Circuit() => new(
        static d =>
        {
            d.Persist<CounterSlice>(static o => (o.Storage, o.Debounce) = (PersistStorage.Server, TimeSpan.Zero)).AddBlazor(static o => o.KeyPrefix = "app");
            d.Use<CounterRestoreRecorder>();
        },
        browser: false,
        services: s => s.AddSingleton<IDistributedCache>(_cache)
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<AuthenticationStateProvider>(_auth)
            .AddSingleton(_restores)
            .AddLogging(b => b.SetMinimumLevel(LogLevel.Debug).AddProvider(new FakeLoggerProvider(_logs))));

    public void Dispose() => _cache.Dispose();

    // Registered after the harness's own clock, so it wins. Runs a one-shot callback on its next GetUtcNow.
    internal sealed class HookedTime() : FakeTimeProvider(new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero))
    {
        private Action? _onNow;

        public void OnNextNow(Action callback) => Volatile.Write(ref _onNow, callback);

        public override DateTimeOffset GetUtcNow()
        {
            Interlocked.Exchange(ref _onNow, null)?.Invoke();
            return base.GetUtcNow();
        }
    }

    // Every Hydration restore of the counter, as value:epoch of the state it committed.
    internal sealed class CounterRestores
    {
        private readonly ConcurrentQueue<string> _entries = new();

        public IReadOnlyList<string> Entries => [.. _entries];

        public void Add(string entry) => _entries.Enqueue(entry);
    }

    // Registered after AddBlazor: sees each restore after its reduce, on the drainer.
    internal sealed class CounterRestoreRecorder(CounterRestores restores) : Middleware
    {
        public override void AfterReduce(ActionContext context)
        {
            if (context.Origin == Origin.Hydration && context.ChangedKeys.Contains("counter"))
            {
                restores.Add($"{context.State.Get<Counter>().Value}:{context.State.Get<PersistenceState>().ScopeEpoch}");
            }
        }
    }
}

using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text;
using CsCheck;
using Ducky.Blazor;
using Ducky.ConcurrencyTests;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Ducky.Concurrency.Tests;

// SPEC §11.6 "Scope switch" step 1, §17.3 row 6: INV-14, INV-17. A switch supersedes the attempt still in flight, and its
// read lands whenever it lands: a slow read of the previous user is never restored into the new user's store nor written
// under the new user's key. A circuit store over the real MemoryDistributedCache, the held user's read gated by a TCS.
// The deterministic twins are ScopeSwitchRaceTests.…_Deterministic in Ducky.Blazor.Tests.
[Collection(nameof(Interleaving))]
public sealed class ScopeSwitchInterleavingTests
{
    // Bob's read (the first switch's) completes while a second switch, to Carol, supersedes it.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public Task DistributedCache_StaleAttemptAfterSecondSwitch_NeverRestoredOrWrittenUnderNewScope(int repeat)
    {
        _ = repeat;
        return Check(static () => new Race(afterFirstSwitch: true));
    }

    // Alice's initial read completes while a sign-in as Bob supersedes attempt 0, during init.
    [Theory]
    [MemberData(nameof(Interleaving.Repeat), MemberType = typeof(Interleaving))]
    public Task DistributedCache_UserChangeDuringInitialHydration_OldScopeValuesNeverRestored(int repeat)
    {
        _ = repeat;
        return Check(static () => new Race(afterFirstSwitch: false));
    }

    private static Task Check(Func<Race> race)
    {
        var read = Gen.Int.Operation<Race, Model>(_ => "CompleteRead", (a, _) => a.CompleteRead(), (_, _) => { });
        var change = Gen.Int.Operation<Race, Model>(_ => "ChangeUser", (a, _) => a.ChangeUser(), (_, _) => { });

        // Bounded by progress (Interleaving.WithinProperty): every blocking step goes through Interleaving.Wait.
        return Interleaving.WithinProperty(Task.Run(
            () => Gen.Const(() => (race(), new Model())).SampleParallel(
                read,
                change,
                equal: static (a, _) => a.NoCrossScopeValue,
                // Two distinct, idempotent operations: more parallel slots add threads, not interleavings, and none runs
                // sequentially first, so a sample with both is always a real race.
                maxSequentialOperations: 0,
                maxParallelOperations: 2),
            TestContext.Current.CancellationToken));
    }

    // One circuit per sample, stored values alice 3, bob 5, carol 7. Epoch e is the scope of _users[e]; the held user's read
    // waits at the gate, and the change makes the last user current. Each operation does its part once, whichever thread
    // gets there first; the check does whatever the run left undone, waits for the newest attempt's terminal, disposes the
    // circuit (its flush writes whatever is still to write) and then looks at every restore and every write.
    private sealed class Race
    {
        private static readonly Dictionary<string, int> _stored = new() { ["alice"] = 3, ["bob"] = 5, ["carol"] = 7 };
        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero));
        private readonly Recorder _recorder = new();
        private readonly Auth _auth = new("alice");
        private readonly Cache _cache;
        private readonly AsyncServiceScope _scope;
        private readonly DuckyStore _store;
        private readonly Task _initialized;
        private readonly string[] _users;
        private readonly Lazy<bool> _verdict;
        private int _changed;

        public Race(bool afterFirstSwitch)
        {
            _verdict = new(Verify, LazyThreadSafetyMode.ExecutionAndPublication);
            _users = afterFirstSwitch ? ["alice", "bob", "carol"] : ["alice", "bob"];
            _cache = new(Key(afterFirstSwitch ? "bob" : "alice"));
            foreach (var (user, value) in _stored)
            {
                _cache.Store(Key(user), EnvelopeWriter.Write($$"""{"Value":{{value}}}""", version: 1, _time.GetUtcNow()));
            }

            // Never disposed but by the check: a sample the shrinker drops dies with it, its timers on a clock that never moves.
            var provider = new ServiceCollection()
                .AddSingleton<TimeProvider>(_time)
                .AddSingleton<IDistributedCache>(_cache)
                .AddSingleton<AuthenticationStateProvider>(_auth)
                .AddSingleton(_recorder)
                .AddDucky(static d =>
                {
                    d.UseJson(ConcurrencyJson.Default).AddSlice<SavedSlice>()
                        .AddBlazor(static o => (o.IsBrowser, o.KeyPrefix) = (false, "app"))
                        .Persist<SavedSlice>(static o => (o.Storage, o.Debounce) = (PersistStorage.Server, TimeSpan.Zero));
                    d.Use<Recording>();
                })
                .BuildServiceProvider();
            _scope = provider.CreateAsyncScope();
            _store = (DuckyStore)_scope.ServiceProvider.GetRequiredService<IStore>();
            _initialized = _store.InitializeAsync(TestContext.Current.CancellationToken);
            if (afterFirstSwitch)
            {
                Interleaving.Wait(_initialized);
                _auth.Change("bob");
            }

            Interleaving.Wait(_cache.HeldReadBegan.Task);
        }

        // CsCheck calls equal concurrently, once per linearization tried: the first caller runs the check once, the others wait
        // for its verdict, so none can look at the store before the dispose flush has written.
        public bool NoCrossScopeValue => _verdict.Value;

        public void CompleteRead() => _cache.Release.TrySetResult();

        public void ChangeUser()
        {
            if (Interlocked.Exchange(ref _changed, 1) == 0)
            {
                _auth.Change(_users[^1]);
            }
        }

        public override string ToString() => $"{string.Join(", ", _recorder.Entries)}; writes: {string.Join(", ", _cache.Sets)}";

        private static string Key(string user) => $"app:{user}:{new SavedSlice().Key}";

        private bool Verify()
        {
            CompleteRead();
            ChangeUser();
            var newest = _users.Length - 1;
            Interleaving.Wait(_initialized);
            Until(() => _store.State.Get<PersistenceState>() is { Status: PersistenceStatus.Hydrated } state && state.ScopeEpoch == newest);
            Interleaving.Wait(_scope.DisposeAsync().AsTask());

            var entries = _recorder.Entries;
            var terminals = entries.Where(static e => e.Terminal is not null).ToList();

            // A restore of epoch e carries the reset's initial state or the value of e's own user, never another user's. Each
            // epoch ends at most once, the newest exactly once, with its user's value restored, and nothing is ever written:
            // what each key holds was read from it, or is a reset the writers skip.
            return entries.Where(static e => e.Terminal is null).All(e => e.Value == 0 || e.Value == _stored[_users[e.Epoch]])
                && terminals.GroupBy(static e => e.Epoch).All(static g => g.Count() == 1)
                && terminals.Single(e => e.Epoch == newest).Terminal is HydrationCompleted { StateRestored: true }
                && _store.State.Get<Saved>().Value == _stored[_users[newest]]
                && _cache.Sets.IsEmpty;
        }

        // Progress-bounded (Interleaving.Wait), spun on a dedicated thread.
        private static void Until(Func<bool> condition) => Interleaving.Wait(Task.Factory.StartNew(
            () =>
            {
                var spin = default(SpinWait);
                while (!condition())
                {
                    spin.SpinOnce();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default));
    }

    private sealed class Model
    {
        public override string ToString() => "no cross-scope value";
    }

    // One restore of the slice (Value) or one terminal, with the epoch of the state it committed.
    private sealed record Entry(int Epoch, int Value, object? Terminal)
    {
        public override string ToString() => Terminal is null ? $"restore {Value}:{Epoch}" : $"{Terminal.GetType().Name}:{Epoch}";
    }

    private sealed class Recorder
    {
        private readonly ConcurrentQueue<Entry> _entries = new();

        public IReadOnlyList<Entry> Entries => [.. _entries];

        public void Add(Entry entry) => _entries.Enqueue(entry);
    }

    // Registered after AddBlazor: every Hydration restore of the slice and every terminal, after its reduce, on the drainer.
    private sealed class Recording(Recorder recorder) : Middleware
    {
        public override void AfterReduce(ActionContext context)
        {
            var epoch = context.State.Get<PersistenceState>().ScopeEpoch;
            if (context.Action is HydrationCompleted or HydrationFailed)
            {
                recorder.Add(new(epoch, 0, context.Action));
            }
            else if (context.Origin == Origin.Hydration && context.ChangedKeys.Contains(Store.Slices.OfType<SavedSlice>().Single().Key))
            {
                recorder.Add(new(epoch, context.State.Get<Saved>().Value, null));
            }
        }
    }

    // The signed-in user, changed the way a circuit does: the provider notifies its subscribers on the changing thread.
    private sealed class Auth(string user) : AuthenticationStateProvider
    {
        private volatile string _user = user;

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, _user)], "test"))));

        public void Change(string next)
        {
            _user = next;
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    // The real MemoryDistributedCache, as-is (§17.2); the held key's first read waits for Release, honouring its token as a
    // real provider does, and every write is noted. Release has inline continuations: the releasing thread runs the read's.
    private sealed class Cache(string held) : IDistributedCache
    {
        private readonly MemoryDistributedCache _inner = new(Options.Create(new MemoryDistributedCacheOptions()));
        private int _holding;

        public TaskCompletionSource Release { get; } = new();

        public TaskCompletionSource HeldReadBegan { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConcurrentQueue<string> Sets { get; } = new();

        public void Store(string key, string value) => _inner.Set(key, Encoding.UTF8.GetBytes(value), new());

        public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
        {
            if (key == held && Interlocked.Exchange(ref _holding, 1) == 0)
            {
                HeldReadBegan.TrySetResult();
                await Release.Task.WaitAsync(token);
            }

            return await _inner.GetAsync(key, token);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Sets.Enqueue($"{key}={Encoding.UTF8.GetString(value)}");
            return _inner.SetAsync(key, value, options, token);
        }

        public Task RemoveAsync(string key, CancellationToken token = default) => _inner.RemoveAsync(key, token);

        public Task RefreshAsync(string key, CancellationToken token = default) => throw new NotSupportedException();

        public byte[]? Get(string key) => throw new NotSupportedException();

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw new NotSupportedException();

        public void Refresh(string key) => throw new NotSupportedException();

        public void Remove(string key) => throw new NotSupportedException();
    }
}

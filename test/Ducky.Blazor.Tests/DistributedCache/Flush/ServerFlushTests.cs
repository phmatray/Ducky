using Ducky.Blazor.Tests.Core;
using Ducky.Blazor.Tests.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Ducky.Blazor.Tests.DistributedCache.Flush;

// SPEC §11.5 "Failed writes are retried" and "Flush on DisposeAsync", §6.11 (a drain that overruns DisposeTimeout), §11.6
// (NonInteractive stores flush on request-scope dispose; one store, not one user), §23 (last-writer-wins and prerender-flush
// ceilings); INV-16, INV-17, INV-29. Server storage needs no JS: a provider failure is retried with the 1 s to 30 s backoff
// while the store is alive, and the dispose flush ends any debounce or backoff and makes one attempt per dirty key.
public sealed class ServerFlushTests
{
    private static readonly TimeSpan _bound = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_DisposeFlushesPending(bool yield)
    {
        // A request store's pending writes flush on request-scope dispose: a change coalesced behind an in-flight write and
        // a change inside its debounce both reach the scoped cache before the container disposes it, and nothing is lost.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(yield) { SetGate = gate };
        var h = Server(cache, "alice", more: static d => d.Persist<DialSlice>(static o => (o.Storage, o.Debounce) = (PersistStorage.Server, TimeSpan.FromSeconds(1))), scoped: true);
        await using var _ = h;
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => cache.Calls.Contains("set:app:alice:counter")); // held by the gate
        var writer = h.Middleware.Writers["counter"];
        h.Store.Dispatch(new Increment()); // coalesced behind it
        h.Store.Dispatch(new SetDial(3));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));

        var disposing = h.DisposeScopeAsync();
        await WriterHarness.Until(() => writer.Flushing); // so the coalesced write is the flush's, not the ordinary loop's
        gate.SetResult();
        await disposing.AsTask().WaitAsync(_bound, Ct);

        cache.Disposed.ShouldBeTrue();
        Payload(cache.Stored("app:alice:counter")).ShouldBe("""{"Value":2}""");
        Payload(cache.Stored("app:alice:dial")).ShouldBe("""{"Value":3}""");
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
        h.Js.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_ChangeInsideDebounce_FlushedImmediately_ServerStorage(bool yield)
    {
        // A Server-storage change still inside its debounce: the flush skips the debounce and writes it at once, without
        // the clock moving, and counts nothing lost.
        var cache = new TestCache(yield);
        await using var h = Server(cache, "alice", static o => o.Debounce = TimeSpan.FromSeconds(30));
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await h.Time.TimerAsync(TimeSpan.FromSeconds(30));
        cache.Stored("app:alice:counter").ShouldBeNull();

        await h.DisposeStoreAsync().WaitAsync(_bound, Ct);

        Payload(cache.Stored("app:alice:counter")).ShouldBe("""{"Value":1}""");
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_TwoCircuitsSameUser_LastWriterWins(bool yield)
    {
        // Documented ceiling (§23): two circuits of one user write the same {prefix}:{user}:{slice}; there is no cross-circuit
        // sync and no compare-and-swap, so each write replaces the whole slice, whichever store it comes from, and a
        // circuit never re-reads what the other wrote.
        var cache = new TestCache(yield);
        cache.Store("app:alice:counter", Envelope("""{"Value":5}"""));
        await using var first = Server(cache, "alice");
        await using var second = Server(cache, "alice");
        await first.InitializeAsync();
        await second.InitializeAsync();

        first.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")) == """{"Value":6}""");
        for (var i = 0; i < 3; i++)
        {
            second.Store.Dispatch(new Increment()); // from its own stale 5: the first circuit's 6 is overwritten
        }

        await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")) == """{"Value":8}""");
        first.Store.Dispatch(new Increment()); // and back: the last writer wins
        await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")) == """{"Value":7}""");

        first.Store.State.Get<Counter>().ShouldBe(new Counter(7));
        second.Store.State.Get<Counter>().ShouldBe(new Counter(8));
        cache.Calls.Count(static c => c.StartsWith("get:", StringComparison.Ordinal)).ShouldBe(2); // each read once, at init
        await using var next = Server(cache, "alice");
        await next.InitializeAsync();
        next.Store.State.Get<Counter>().ShouldBe(new Counter(7));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_PrerenderFlushAfterCircuitWrite_Documented(bool yield)
    {
        // Documented ceiling (§23): the prerender store's flush on request-scope dispose can land after the circuit store
        // already read and wrote the same key, and overwrites that newer state with its own.
        var cache = new TestCache(yield);
        cache.Store("app:alice:counter", Envelope("""{"Value":5}"""));
        var prerender = Server(cache, "alice", static o => o.Debounce = TimeSpan.FromSeconds(1));
        await using var _ = prerender;
        await prerender.InitializeAsync();
        prerender.Store.Dispatch(new Increment()); // 6, pending in its debounce
        await prerender.Time.TimerAsync(TimeSpan.FromSeconds(1));

        await using var circuit = Server(cache, "alice");
        await circuit.InitializeAsync();
        circuit.Store.State.Get<Counter>().ShouldBe(new Counter(5));
        for (var i = 0; i < 4; i++)
        {
            circuit.Store.Dispatch(new Increment());
        }

        await WriterHarness.Until(() => Payload(cache.Stored("app:alice:counter")) == """{"Value":9}""");

        await prerender.DisposeScopeAsync().AsTask().WaitAsync(_bound, Ct);

        Payload(cache.Stored("app:alice:counter")).ShouldBe("""{"Value":6}""");
        circuit.Store.State.Get<Counter>().ShouldBe(new Counter(9));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DistributedCache_TransientCacheFailure_WriteRetriedUntilSuccess(bool yield)
    {
        // A cache that times out twice (a transient Redis timeout): the key stays dirty with its baseline untouched, one
        // PersistenceFailed opens the streak, and the write is retried after 1 s, then 2 s on the TimeProvider (a change
        // meanwhile keeps the backoff); the retry that succeeds writes the state current then and ends the streak.
        var cache = new TestCache(yield);
        var failures = 2;
        cache.SetFault = () => Interlocked.Decrement(ref failures) >= 0 ? new TimeoutException("Redis timeout") : null;
        await using var h = Server(cache, "alice");
        await h.InitializeAsync();

        var start = h.Time.GetUtcNow();
        h.Store.Dispatch(new Increment());
        var failed = await h.NextFailureAsync();
        (failed.SliceKey, failed.ErrorType, failed.Message).ShouldBe(("counter", nameof(TimeoutException), "Redis timeout"));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(1));
        h.Middleware.LastKnownPayload.ShouldBeEmpty();
        h.Middleware.Writers["counter"].Dirty.ShouldBeTrue();

        h.Time.Advance(TimeSpan.FromSeconds(1));
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2));
        h.Store.Dispatch(new Increment());
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, Ct); // real time: room for a retry the change wrongly woke to show
        h.Time.Advance(TimeSpan.FromSeconds(2) - TimeSpan.FromTicks(1));
        cache.Stored("app:alice:counter").ShouldBeNull();

        h.Time.Advance(TimeSpan.FromTicks(1));
        await WriterHarness.Until(() => cache.Stored("app:alice:counter") is not null);
        Payload(cache.Stored("app:alice:counter")).ShouldBe("""{"Value":2}""");
        await h.IterationsAsync("counter", 1);
        h.Middleware.LastKnownPayload["app:alice:counter"].ShouldBe("""{"Value":2}""");
        // Stamped at the attempt: a retry woken early by the change would carry an earlier time.
        System.Text.Json.JsonDocument.Parse(cache.Stored("app:alice:counter")!).RootElement.GetProperty("at").GetDateTimeOffset().ShouldBe(start + TimeSpan.FromSeconds(3));
        cache.Calls.Count(static c => c == "set:app:alice:counter").ShouldBe(3);
        h.Inbox.Failures.Reader.TryRead(out _).ShouldBeFalse();
        h.Js.Calls.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_KeyInRetryBackoff_FlushWritesImmediately_ServerStorage(bool yield)
    {
        // A key whose writes keep failing sleeps in its 30 s backoff, 25 s of it still to go, when the cache heals and the
        // store is disposed: the flush ends the backoff and writes at once, without the clock moving, and nothing is lost.
        var cache = new TestCache(yield);
        var failing = true;
        cache.SetFault = () => Volatile.Read(ref failing) ? new TimeoutException("Redis timeout") : null;
        await using var h = Server(cache, "alice");
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        foreach (var seconds in new[] { 1, 2, 4, 8, 16 })
        {
            await h.Time.TimerAsync(TimeSpan.FromSeconds(seconds));
            h.Time.Advance(TimeSpan.FromSeconds(seconds));
        }

        await h.Time.TimerAsync(TimeSpan.FromSeconds(30));
        h.Time.Advance(TimeSpan.FromSeconds(5));
        Volatile.Write(ref failing, false);

        await h.DisposeStoreAsync().WaitAsync(_bound, Ct);

        Payload(cache.Stored("app:alice:counter")).ShouldBe("""{"Value":1}""");
        cache.Calls.Count(static c => c == "set:app:alice:counter").ShouldBe(7);
        lost.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_DrainOverrunsTimeout_ServerScope_DisposeAsyncCompletesAtBound_LateFlushCountsLost(bool providerFault)
    {
        // The circuit scope ends while one Process step outlasts DisposeTimeout: the dispose hook's DisposeAsync completes
        // at that bound, not before, and the container goes on, disposing a scoped cache. The flush runs only once the drain
        // exits, chained after that: each of the two dirty keys meets the disposed cache, which throws ObjectDisposedException
        // (or a live one that fails, any other provider exception), and is counted ducky.persistence.lost, one key's failure
        // never skipping the other's attempt. A user middleware whose chained disposal throws (a scoped dependency already
        // gone) is the logged Error 1015, also only once the drain exits.
        var cache = new TestCache(yield: false);
        if (providerFault)
        {
            cache.SetFault = static () => new TimeoutException("Redis timeout");
        }

        var logs = new FakeLogCollector();
        await using var h = new WriterHarness(
            static d => d.Persist<CounterSlice>(Slow).Persist<DialSlice>(Slow).AddBlazor(static o => o.KeyPrefix = "app").Use<ThrowsOnDispose>(),
            browser: false,
            services: s => (providerFault ? s.AddSingleton<IDistributedCache>(cache) : s.AddScoped<IDistributedCache>(_ => cache))
                .AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice"))
                .AddLogging(b => b.AddProvider(new FakeLoggerProvider(logs))));
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        var middleware = h.Middleware;
        h.Store.Dispatch(new Increment());
        await h.Time.TimerAsync(TimeSpan.FromHours(1));

        var boundArmed = false;
        var completedEarly = false;
        var completedAtBound = false;
        var attemptsInDrain = -1;
        var errorsInDrain = -1;
        h.Log.OnEntry = entry =>
        {
            if (entry.StartsWith(typeof(SetDial).FullName!, StringComparison.Ordinal))
            {
                // Inside the drain: the scope ends, the store's wait for this drain expires at DisposeTimeout (2 s), and
                // the container then disposes the cache while this step is still running. With the drain blocked here, that
                // wait is the only possible 2 s timer, so a shorter or missing bound never arms it. The fake clock has not
                // moved yet, so correct code cannot complete during the 100 ms look: only a dispose that skips the wait can.
                var scopeDisposal = h.DisposeScopeAsync().AsTask();
#pragma warning disable VSTHRD002 // justification: the drain step must outlast the dispose; bounded waits on the store's and the container's progress
                boundArmed = h.Time.TimerAsync(TimeSpan.FromSeconds(2)).Wait(_bound, Ct);
                completedEarly = scopeDisposal.Wait(TimeSpan.FromMilliseconds(100), Ct);
                h.Time.Advance(TimeSpan.FromSeconds(2));
                completedAtBound = scopeDisposal.Wait(_bound, Ct);
#pragma warning restore VSTHRD002
                attemptsInDrain = cache.Calls.Count(static c => c.StartsWith("set:", StringComparison.Ordinal));
                errorsInDrain = logs.GetSnapshot().Count(static r => r.Id.Id == 1015);
            }
        };

        h.Store.Dispatch(new SetDial(1));

        boundArmed.ShouldBeTrue();
        completedEarly.ShouldBeFalse();
        completedAtBound.ShouldBeTrue();
        attemptsInDrain.ShouldBe(0);
        errorsInDrain.ShouldBe(0);
        cache.Disposed.ShouldBe(!providerFault);
        await WriterHarness.Until(() => lost.GetMeasurementSnapshot().Sum(static m => m.Value) == 2);
        cache.Calls.Where(static c => c.StartsWith("set:", StringComparison.Ordinal)).Order(StringComparer.Ordinal).ShouldBe(["set:app:alice:counter", "set:app:alice:dial"]);
        cache.Stored("app:alice:counter").ShouldBeNull();
        middleware.Writers.Values.ShouldAllBe(static w => w.Dirty);
        var error = logs.GetSnapshot().Where(static r => r.Id.Id == 1015).ShouldHaveSingleItem();
        error.Level.ShouldBe(LogLevel.Error);
        error.Message.ShouldContain(nameof(ThrowsOnDispose));
        error.Exception.ShouldBeOfType<ObjectDisposedException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_ProviderIgnoresItsToken_KeysStillCountedLost(bool yield)
    {
        // (non-normative) INV-16 "every key still dirty when the flush ends is counted": the flush's one bound ends at the
        // store's own (§6.11 5a), so the count lands there, with both calls still held, while the root IMeterFactory is
        // alive. One key's hung call holds neither its own count nor another key's.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(yield) { SetGate = gate };
        await using var h = Server(cache, "alice", more: static d => d.Persist<DialSlice>(static o => o.Storage = PersistStorage.Server));
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        h.Store.Dispatch(new SetDial(1));
        await WriterHarness.Until(() => cache.Calls.Contains("set:app:alice:counter") && cache.Calls.Contains("set:app:alice:dial"));

        var disposal = h.DisposeStoreAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the flush's bound
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the store's bound over this middleware's DisposeAsync
        h.Time.Advance(TimeSpan.FromSeconds(2));

        await WriterHarness.Until(() => lost.GetMeasurementSnapshot().Sum(static m => m.Value) == 2);
        await disposal.WaitAsync(_bound, Ct);
        gate.SetResult();
    }

    [Fact]
    public async Task Dispose_ProviderIgnoresItsToken_ModuleDisposedAtSecondBound()
    {
        // (non-normative) §11.5: the stopped loops are awaited before ducky.js is disposed, bounded too: a hung call holds
        // the module's disposal for one more DisposeTimeout past the count, never for as long as the call hangs.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(yield: false) { SetGate = gate };
        await using var h = new WriterHarness(
            static d => d.Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server).Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.KeyPrefix = "app"),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(cache).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice")));
        using var lost = new MetricCollector<long>(h.Meters, "Ducky", "ducky.persistence.lost");
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync(); // imports ducky.js
        await h.IterationsAsync("level", 1);
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => cache.Calls.Contains("set:app:alice:counter"));

        var disposal = h.DisposeStoreAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the flush's bound
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the store's bound over this middleware's DisposeAsync
        h.Time.Advance(TimeSpan.FromSeconds(2));
        await WriterHarness.Until(() => lost.GetMeasurementSnapshot().Sum(static m => m.Value) == 1);
        await disposal.WaitAsync(_bound, Ct);
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the bound over the stopped loops
        h.Js.Disposed.ShouldBe(0);

        h.Time.Advance(TimeSpan.FromSeconds(2));

        await WriterHarness.Until(() => h.Js.Disposed == 1);
        gate.SetResult();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Dispose_CallReturningAfterTheStop_NoAttemptFollows(bool fails)
    {
        // (non-normative) §11.5: a call that outlasts the flush's bound and returns only once the loops' own tokens were
        // cancelled ends its loop, whatever its result: no backoff and no attempt follow it, not even for a change made
        // while it was held. The loop's end then releases the module's disposal, before the bound over the stopped loops.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(yield: false) { SetGate = gate };
        await using var h = new WriterHarness(
            static d => d.Persist<CounterSlice>(static o => o.Storage = PersistStorage.Server).Persist<LevelSlice>(static o => o.Debounce = TimeSpan.Zero).AddBlazor(static o => o.KeyPrefix = "app"),
            browser: false,
            services: s => s.AddSingleton<IDistributedCache>(cache).AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider("alice")));
        await h.InitializeAsync();
        h.Store.Dispatch(new SetLevel(1));
        await h.NextWriteAsync(); // imports ducky.js
        await h.IterationsAsync("level", 1);
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => cache.Calls.Contains("set:app:alice:counter"));
        h.Store.Dispatch(new Increment()); // signalled while the write is held
        await WriterHarness.Until(() => h.Middleware.Writers["counter"].Signals == 2);

        var disposal = h.DisposeStoreAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the flush's bound
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the store's bound over this middleware's DisposeAsync
        h.Time.Advance(TimeSpan.FromSeconds(2));
        await disposal.WaitAsync(_bound, Ct);
        await WriterHarness.Until(() => cache.LastSetToken.IsCancellationRequested);
        var disposedAtReturn = -1;
        cache.SetFault = () =>
        {
            disposedAtReturn = h.Js.Disposed;
            return fails ? new InvalidOperationException("cache down") : null;
        };
        gate.SetResult();

        await WriterHarness.Until(() => h.Js.Disposed == 1);
        disposedAtReturn.ShouldBe(0);
        cache.Calls.Count(static call => call == "set:app:alice:counter").ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_WriteHeldThroughFlush_TokenCancelledOnlyAtTheBound()
    {
        // (non-normative) §11.5 "only then are the loops' own CTSs cancelled": a held call's token stays live while the flush
        // waits for it, then is cancelled at the flush's bound, so a provider honouring it can't land late over newer state.
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cache = new TestCache(yield: false) { SetGate = gate };
        await using var h = Server(cache, "alice");
        await h.InitializeAsync();
        h.Store.Dispatch(new Increment());
        await WriterHarness.Until(() => cache.Calls.Contains("set:app:alice:counter"));
        var token = cache.LastSetToken;

        var disposal = h.DisposeStoreAsync();
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the flush's bound
        await h.Time.TimerAsync(TimeSpan.FromSeconds(2)); // the store's bound over this middleware's DisposeAsync
        var flushing = h.Middleware.Writers["counter"].Flushing;
        var cancelledInFlush = token.IsCancellationRequested;

        h.Time.Advance(TimeSpan.FromSeconds(2)); // before asserting: a held dispose would hang the test's own cleanup

        flushing.ShouldBeTrue();
        cancelledInFlush.ShouldBeFalse();
        await WriterHarness.Until(() => token.IsCancellationRequested);
        gate.SetResult();
        await disposal.WaitAsync(_bound, Ct);
    }

    // A slice written only by the flush in practice: its debounce never ends under the FakeTimeProvider.
    private static void Slow(PersistOptions o) => (o.Storage, o.Debounce) = (PersistStorage.Server, TimeSpan.FromHours(1));

    private static string? Payload(string? envelope) =>
        envelope is null ? null : System.Text.Json.JsonDocument.Parse(envelope).RootElement.GetProperty("s").GetRawText();

    private static string Envelope(string payload) => EnvelopeWriter.Write(payload, version: 1, new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero));

    // A server store of `user` over `cache`, the counter in Server storage (then `counter`, then `more`), the cache scoped when
    // `scoped` (the container then disposes it with the scope). JS refuses every call, as the prerender runtime does.
    private static WriterHarness Server(TestCache cache, string user, Action<PersistOptions>? counter = null, Action<DuckyBuilder>? more = null, bool scoped = false)
    {
        var harness = new WriterHarness(
            d =>
            {
                d.Persist<CounterSlice>(o =>
                {
                    o.Storage = PersistStorage.Server;
                    counter?.Invoke(o);
                }).AddBlazor(static o => o.KeyPrefix = "app");
                more?.Invoke(d);
            },
            browser: false,
            services: s => (scoped ? s.AddScoped<IDistributedCache>(_ => cache) : s.AddSingleton<IDistributedCache>(cache))
                .AddSingleton<AuthenticationStateProvider>(new TestAuthenticationStateProvider(user)));
        harness.Js.Respond = static (_, _) => throw new InvalidOperationException("JavaScript interop calls cannot be issued at this time.");
        return harness;
    }
}

// A user middleware whose disposal meets a scoped dependency the container already disposed.
internal sealed class ThrowsOnDispose : Middleware
{
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        throw new ObjectDisposedException("a scoped dependency");
    }
}

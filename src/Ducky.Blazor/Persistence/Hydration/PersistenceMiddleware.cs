using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// SPEC §11.5: one per store, registered by AddBlazor right after the prerender handoff, inert without Persist<T> slices.
// Every restore and terminal it issues is issued under _issue together with the decision behind it, so they reach the
// queue in decision order (an inline drain under it is allowed: the lock is reentrant and never taken under _gate).
internal sealed partial class PersistenceMiddleware : Middleware
{
    private readonly Lock _issue = new();
    private readonly BlazorRegistration _registration;
    private readonly PersistenceSlice _persistence;
    private readonly IServiceProvider _services;
    private readonly JsBridge _bridge;
    private readonly BrowserStorageProvider _browser;
    private readonly DistributedCacheProvider _cache;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly SafeTelemetry _telemetry;
    private readonly SafeLogger _log;
    private readonly TaskCompletionSource _initDone = new(TaskCreationOptions.RunContinuationsAsynchronously); // the init task
    private ImmutableDictionary<int, string?> _scopes = ImmutableDictionary<int, string?>.Empty;
    private bool _initPhase; // under _issue: set with attempt 0, cleared with the init-phase terminal
    private CancellationToken _initToken;
    private CancellationTokenSource? _deadline;
    private CancellationTokenSource? _hydration;
    private bool _nullScopeLogged; // under _issue: the null-scope Info log is once per store (§11.6)

    public PersistenceMiddleware(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_registration, _persistence, _services) = (registration, persistence, services);
        _logger = (ILogger?)services.GetService<ILogger<PersistenceMiddleware>>() ?? NullLogger.Instance;
        _log = new(_logger);

        // The store scope's runtime (none in a host without JS) until a component hands over its renderer's (§6.10).
        _bridge = persistence.Gate.CreateBridge(services.GetService<IJSRuntime>(), _logger);
        _browser = new(_bridge, registration.Options, _logger);
        _cache = new(services, registration.Options);
        _time = services.GetRequiredService<TimeProvider>();
        _telemetry = new(_log, services.GetService<IMeterFactory>());
        persistence.Middleware = this;
    }

    /// <summary>
    /// The current hydration attempt (§11.5), replaced only by a scope switch; null when nothing is readable, except in a
    /// browser store whose persisted slices are all inert Server storage, where it is an attempt with nothing to read.
    /// </summary>
    internal Attempt? Current { get; private set; }

    /// <summary>The scope recorded per epoch (§11.5): swapped under <c>_issue</c>, read lock-free by the writers.</summary>
    internal ImmutableDictionary<int, string?> Scopes => Volatile.Read(ref _scopes);

    /// <summary>
    /// The last payload known to be stored, per full storage key (§11.5): always the local serialization of the state,
    /// never the stored text, so the writer's dedupe compares like with like.
    /// </summary>
    internal ConcurrentDictionary<string, string> LastKnownPayload { get; } = new();

    // The synchronous prefix decides, before any await, what is readable and opens attempt 0 (§11.5 steps 1-3).
    public override ValueTask InitializeAsync(CancellationToken cancellationToken)
    {
        var persisted = Store.Slices
            .Select(slice => (Slice: slice, Options: _registration.Persist.GetValueOrDefault(slice.GetType())?.Options))
            .Where(static persisted => persisted.Options is not null)
            .Select(static persisted => new Persisted(persisted.Slice, persisted.Options!))
            .ToArray();

        // §11.6: in the browser Server storage is inert (one Info log): such a slice counts as not persisted, so with nothing
        // else persisted nothing happens and Status stays Hydrated (§11.5).
        var inert = _registration.Options.IsBrowser ? persisted.Where(static p => p.Options.Storage == PersistStorage.Server).ToArray() : [];
        if (inert.Length > 0)
        {
            string[] keys = [.. inert.Select(static p => p.Slice.Key)];
            Log.ServerStorageInert(_log, keys);
        }

        var slices = persisted.Except(inert).ToArray();
        _resets = [.. persisted.Where(p => Scoped(p.Options)).Select(static p => p.Slice.Key)];
        if (slices.Length == 0)
        {
            // Nothing to hydrate, but with a resolvable provider the inert keys put a scope in use (§11.6): an attempt 0 with
            // nothing to read and no terminal (Status stays Hydrated), which a user change supersedes like any other, so its
            // reset restores them. No scope is ever recorded for it, so no notification is a re-assertion.
            if (inert.Length > 0)
            {
                Current = new();
                SubscribeToUserChanges(persisted);
            }

            return default;
        }

        // Browser storage is readable only in an interactive store (probing if nothing decided yet, §11.4); Server storage
        // outside the browser always (the NonInteractive rule, §11.6), so a store holding only Server storage never probes.
        // Nothing readable (prerender or static SSR with browser-only slices): no attempt, so no terminal.
        if (slices.Any(static p => p.Options.Storage != PersistStorage.Server)
            && _persistence.Gate.Resolve(_registration.Options.IsBrowser, _bridge) != InteractivityMode.Interactive)
        {
            slices = [.. slices.Where(static p => p.Options.Storage == PersistStorage.Server)];
        }

        if (slices.Length == 0)
        {
            lock (_issue)
            {
                Store.Restore(Status(new(PersistenceStatus.NotStarted)), Origin.Hydration);
            }

            return default;
        }

        // Attempt 0: epoch 0 (no scope switch yet), its key set every readable key. The init-phase deadline and the init
        // token each end the attempt if it has not ended (step 7); the linked token only bounds the scope wait and the reads.
        var attempt = new Attempt { Keys = [.. slices.Select(static persisted => persisted.Slice.Key)] };
        _readable = slices;
        _writers = slices.ToDictionary(static persisted => persisted.Slice.Key, persisted => new PersistenceWriter(this, persisted.Slice, persisted.Options));
        _initToken = cancellationToken;
        _deadline = new(_registration.Options.HydrationTimeout, _time);
        _hydration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
        lock (_issue)
        {
            (Current, _initPhase) = (attempt, true);
            RecordAttempt(attempt);
            Store.Restore(Status(new(PersistenceStatus.Hydrating) { ScopeEpoch = attempt.Epoch }), Origin.Hydration);

            // §6.7: registered here, in the synchronous prefix, after the Hydrating restore it must follow. On the init token
            // itself, never only through the linked token: once the deadline is cancelling that one on another thread, an
            // abort's Cancel() of it returns at once, and Abort would reach MarkReady before this callback took _issue.
            cancellationToken.UnsafeRegister(static self => ((PersistenceMiddleware)self!).EndInitPhase(), this);
            _deadline.Token.UnsafeRegister(static self => ((PersistenceMiddleware)self!).EndInitPhase(), this);
        }

        // Inert keys included: in the browser they put a scope in use when a provider is resolvable, never join a read (§11.6).
        SubscribeToUserChanges(persisted);

        // §11.7: the one storage registration of a store that persists a Local or Session slice, issued before the reads and
        // ordered before them on the module task. Server storage is no browser storage: no notification names it.
        Persisted[] browser = [.. slices.Where(static p => p.Options.Storage != PersistStorage.Server)];
        if (browser.Length > 0)
        {
            _crossTab = new(this, browser);
            _crossTab.Watch();
        }

        // The init task is the init-phase terminal, not HydrateAsync (§11.5 "Before StoreInitialized"): a Scope delegate that
        // ignores its token keeps HydrateAsync running past the deadline, and its late scope is still recorded for epoch 0
        // (§11.6). HydrateAsync never faults: its try catches every failure, and its tail (restore, terminal) does not throw.
        _ = HydrateAsync(slices, attempt, _hydration.Token);
        return new(_initDone.Task);
    }

    public override async ValueTask DisposeAsync()
    {
        // Phase 5a, after init ended (§6.11): a HydrateAsync still awaiting a late scope delegate holds an already-cancelled
        // token. A switch attempt's scope wait and reads end here too. The flush (§11.5) runs before the module is disposed.
        await StopSwitchesAsync().ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        _hydration?.Dispose();
        _deadline?.Dispose();
        if (_crossTab is not null)
        {
            await _crossTab.StopAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        }

        await FlushAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        _stopped.Dispose();
        if (_crossTab is not null)
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await _crossTab.DisposeAsync().ConfigureAwait(false);
        }

        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await _bridge.DisposeAsync().ConfigureAwait(false);
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await base.DisposeAsync().ConfigureAwait(false);
    }

    // §11.5 steps 4-7: resolve the scope, read every key of the attempt (attempt 0 directly, nothing was written before it;
    // a switch through each key's writer loop), wait for the seed restore so it stays first in the queue (ADR-0011), then
    // claim the terminal, set the baselines, one restore of what was read and the one terminal. A failure claims a HydrationFailed instead; once the attempt's token is cancelled, the
    // deadline or the init abort ends the attempt (whichever of its callback and this catch comes first). The task never
    // completes before the attempt's terminal is queued.
    private async Task HydrateAsync(Persisted[] slices, Attempt attempt, CancellationToken token)
    {
        Dictionary<string, object> values = [];
        Dictionary<string, (string Key, string? Payload)> baselines = [];
        var sequences = ImmutableDictionary<string, long>.Empty;
        try
        {
            var reader = new EnvelopeReader(Store.Json, _logger);
            await ResolveScopeAsync(slices, attempt, token).ConfigureAwait(ConfigureAwaitOptions.None);

            // A scope delegate that ignored its token can resolve after the attempt ended: recorded, but nothing is read.
            token.ThrowIfCancellationRequested();

            // §11.5 step 6: a key whose cross-tab sequence number moved after this point holds a newer value than its read; one
            // received before (an unscoped key is accepted during the scope wait) is older (CrossTab_ReceiptDuringScopeWait_LaterReadStands).
            sequences = Volatile.Read(ref _sequences);

            foreach (var (slice, options) in slices)
            {
                // §11.6 "no longer current, stop": no more I/O for results that would be discarded. A superseded switch attempt's
                // token says so too; the initial attempt's is the init deadline's, so it asks here.
                lock (_issue)
                {
                    if (attempt != Current)
                    {
                        Log.HydrationResultDiscarded(_log, attempt.Epoch);
                        return;
                    }
                }

                // A null scope: no I/O for scoped keys, never a fallback to a shared key (§11.6). A plain local, not a pattern
                // variable: a mutant of the test then can't leave it unassigned (Stryker Safe Mode, §17.8).
                var key = StorageKey(slice.Key, options, attempt.Epoch);
                if (key is null)
                {
                    continue;
                }

                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                var text = await ReadForAttemptAsync(attempt, slice, options, key, token).ConfigureAwait(false);

                // A slice that fails to read (malformed, newer, expired, a throwing constructor) keeps its state; the reader
                // logs it naming the key, and the other slices still restore. The baseline is the local serialization of
                // what was read (absent, or an unserializable state: none, so an older baseline of this key goes).
                baselines[slice.Key] = (key, null);
                if (text is not null && reader.TryRead(text, key, slice.StateType, options.Version, options.Migrations, options.MaxAge, _time.GetUtcNow(), out var state))
                {
                    values[slice.Key] = state;
                    if (Store.Json.TrySerialize(state, slice.StateType, out var payload))
                    {
                        baselines[slice.Key] = (key, payload);
                    }
                }
            }

#pragma warning disable VSTHRD003 // justification: the store's own RunContinuationsAsynchronously TCS, which the handoff always completes
            await _persistence.SeedSettled.Task.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        }
#pragma warning disable CA1031 // justification: every failure of the attempt becomes its HydrationFailed terminal (§11.5 step 7)
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The initial attempt's cancelled token is the deadline or the init abort. Its callback may not have run yet
            // (callbacks run in reverse registration order, so a read's continuation can come first), and this task's
            // completion can let StoreInitialized through: end the attempt here, before returning. A switch attempt's is its
            // supersession, dispose, or EndInitPhase having ended it already: nothing left to end.
            if (token.IsCancellationRequested)
            {
                if (attempt.Epoch == 0)
                {
                    EndInitPhase();
                }
                else
                {
                    Log.HydrationResultDiscarded(_log, attempt.Epoch);
                }

                return;
            }

            lock (_issue)
            {
                // Only a failure that reaches the store is reported as one (INV-14).
                if (!Claim(attempt, new HydrationFailed(exception.GetType().Name, exception.Message) { ScopeEpoch = attempt.Epoch }))
                {
                    Log.HydrationResultDiscarded(_log, attempt.Epoch);
                }
                else if (attempt.Epoch > 0)
                {
                    Log.ScopeSwitchFailed(_log, exception, attempt.Epoch);
                }

                IssueTerminal(attempt);
            }

            return;
        }

        lock (_issue)
        {
            // §11.4: a pause seed's dirty key keeps the seed's value over storage, so it is no key restored from storage. Its
            // baseline is still the stored value, and IssueTerminal defers it, so its writer rewrites storage with the seed's.
            foreach (var key in _persistence.SeedDirty)
            {
                values.Remove(key);
            }

            // A cross-tab value received since the read began is newer (CrossTab_EventDuringHydration_NewestWins): its
            // restore stays, and so does its baseline. It was a Hydration restore of this attempt, so it counts as restored.
            // ponytail: every bump before the terminal is a value restore; count only those once removals bump (M7-02b).
            string[] overtaken = [.. _sequences.Where(entry => attempt.Keys.Contains(entry.Key) && sequences.GetValueOrDefault(entry.Key) != entry.Value).Select(static entry => entry.Key)];
            foreach (var key in overtaken)
            {
                values.Remove(key);
                baselines.Remove(key);
            }

            var seeded = TakeScopedSeed(attempt, values, overtaken);

            // Lost to the deadline, the init abort or a newer switch, which owns the terminal: the results are discarded.
            if (!Claim(attempt, new HydrationCompleted(values.Count > 0 || overtaken.Length > 0) { ScopeEpoch = attempt.Epoch }))
            {
                Log.HydrationResultDiscarded(_log, attempt.Epoch);
                return;
            }

            attempt.Restored = true;
            foreach (var key in seeded)
            {
                Deferred.TryAdd(key, 0);
            }

            foreach (var (key, payload) in baselines.Values)
            {
                if (payload is null)
                {
                    LastKnownPayload.TryRemove(key, out _);
                }
                else
                {
                    LastKnownPayload[key] = payload;
                }
            }

            if (values.Count > 0)
            {
                Store.Restore(values, Origin.Hydration);
            }

            IssueTerminal(attempt);
        }
    }

    // §11.5 step 6, under _issue: the scoped keys a pause seed handed over. Only attempt 0, still current, under a scope
    // that hashes to the seed's, takes them: a dirty browser key's seed value in place of the stored one, any other only
    // when storage held no valid envelope for it; a key a cross-tab value overtook keeps that newer value. Otherwise they
    // stay for IssueTerminal, whichever attempt issues it, to drop (Debug 2017). Returns the dirty keys applied, deferred
    // once the claim is won (current, so it is), so their writers rewrite storage after the terminal (baseline from the read).
    private List<string> TakeScopedSeed(Attempt attempt, Dictionary<string, object> values, string[] overtaken)
    {
        List<string> seeded = [];
        var seed = _persistence.SeedScoped;

        // A seed that recorded no scope matches none, a null one included.
        if (seed is null || attempt.Epoch != 0 || attempt != Current || seed.Scope is null || SeedWriter.ScopeHash(Scopes.GetValueOrDefault(0)) != seed.Scope)
        {
            return seeded;
        }

        _persistence.SeedScoped = null;

        foreach (var (key, value) in seed.Values.Where(entry => !overtaken.Contains(entry.Key)))
        {
            if (seed.Dirty.Contains(key))
            {
                values[key] = value;
                seeded.Add(key);
            }
            else
            {
                values.TryAdd(key, value);
            }
        }

        return seeded;
    }

    // When any key of the attempt is scoped (§11.6), resolves its epoch's scope (Scope ?? NameIdentifier) and records it,
    // possibly null, unless a newer switch took over meanwhile. In the browser the delegate gets the renderer's services, so it waits for the hand-off (§11.6); the
    // attempt's token bounds that wait, the resolution and the reads by the same HydrationTimeout. A delegate that ignores
    // the token only delays its own recording: init ends with the deadline's terminal all the same.
    private async Task ResolveScopeAsync(Persisted[] slices, Attempt attempt, CancellationToken token)
    {
        var options = _registration.Options;
        if (!slices.Any(p => Scoped(p.Options)))
        {
            return;
        }

        var resolve = options.Scope ?? DuckyScopes.NameIdentifier;
        var services = _services;
        if (options.IsBrowser)
        {
            var handedOver = new TaskCompletionSource<IServiceProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
            _persistence.Gate.OnFirstRegistration(() => handedOver.TrySetResult(_persistence.Gate.Services!));
            services = await handedOver.Task.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.None);
        }

        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var scope = await resolve(services, token).ConfigureAwait(false);

        scope = Normalized(scope);
        lock (_issue)
        {
            if (attempt != Current)
            {
                return;
            }

            if (scope is null && !_nullScopeLogged)
            {
                _nullScopeLogged = true;
                Log.NullScope(_log);
            }

            Volatile.Write(ref _scopes, _scopes.SetItem(attempt.Epoch, scope));

            // The release of case (b) (§11.5 "A skipped key stays dirty"), published map first. Only once the attempt's
            // terminal is issued: before it, that terminal releases every deferred key after this map, and a release here
            // could let a key deferred before the synchronous prefix be written ahead of the read, from a snapshot in which
            // the prefix's Hydrating restore (queued behind a drain in progress) is not reduced yet (SPEC §11.5 step 4;
            // pinned by Write_ChangeBeforeFirstRead_ScopeResolvedSynchronously_NotWrittenBeforeRead, init started from
            // inside the change's drain).
            if (attempt.Terminal == 2)
            {
                ReleaseDeferred();
            }
        }
    }

    // §11.5 "Switch reads": a switch attempt reads through the key's writer loop, after its final snapshot and in-flight write.
    private async ValueTask<string?> ReadForAttemptAsync(Attempt attempt, Slice slice, PersistOptions options, string key, CancellationToken token)
    {
        if (attempt.Epoch == 0)
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            return await ReadAsync(options, key, token).ConfigureAwait(false);
        }

#pragma warning disable VSTHRD003 // justification: the attempt's own RunContinuationsAsynchronously TCS; dispose ends the wait through the token
        await attempt.ResetCommitted.Task.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        return await _writers[slice.Key].ReadAsync(key, token).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // Server storage reads the cache; Local and Session go through ducky.js.
    private ValueTask<string?> ReadAsync(PersistOptions options, string key, CancellationToken token) => options.Storage == PersistStorage.Server
        ? _cache.GetAsync(key, token)
        : _browser.GetAsync(options.Storage, key, token);

    // §11.5 step 7: the init-phase deadline and the init abort end the attempt current during init, once, under _issue,
    // whether or not they win the claim: an abort raised re-entrantly while the winner restores finds the winner's terminal
    // claimed but not issued, and issues it, so it is queued before StoreInitialized. After the init phase: nothing.
    private void EndInitPhase()
    {
        lock (_issue)
        {
            if (!_initPhase)
            {
                return;
            }

            var attempt = Current!;
            var failure = _initToken.IsCancellationRequested
                ? new HydrationFailed(nameof(OperationCanceledException), "Store init was aborted before hydration ended.") { ScopeEpoch = attempt.Epoch }
                : TimedOut(attempt.Epoch);
            Claim(attempt, failure);
            IssueTerminal(attempt);

            // A switch attempt's token is not the deadline's: its reads stop here, and a hung one frees its key's loop (§11.5
            // "A failure leaves persistence running"). CancelAsync, as a supersession does: no callback runs under this lock.
            _ = attempt.Cancellation?.CancelAsync();
        }
    }

    // Under _issue: the first claim (0 -> 1) of the current attempt decides its terminal; a superseded attempt issues nothing.
    private bool Claim(Attempt attempt, object terminal)
    {
        if (attempt != Current || attempt.Terminal != 0)
        {
            return false;
        }

        (attempt.Terminal, attempt.Pending) = (1, terminal);
        return true;
    }

    // Under _issue: issues the claimed terminal once (1 -> 2), which ends the init phase and disarms its timer (§11.5 step 7).
    // Disarmed, not disposed: this can run inside that timer's own callback, and the linked _hydration still holds its token.
    // A switch raised from a subscriber of the winner's own restore supersedes it between its claim and this call.
    private void IssueTerminal(Attempt attempt)
    {
        if (attempt != Current || attempt.Terminal != 1)
        {
            return;
        }

        if (_initPhase)
        {
            _initPhase = false;
            _deadline!.CancelAfter(Timeout.InfiniteTimeSpan);
        }

        // §11.4: a pause seed's dirty keys, restored by the handoff, are rewritten after whichever terminal comes first, a
        // failed one included (the release of the deferred keys), and only once: a later attempt reads them like any key.
        foreach (var key in _persistence.SeedDirty)
        {
            Deferred.TryAdd(key, 0);
        }

        _persistence.SeedDirty = [];

        // §11.4: scoped keys step 6 did not take (another scope, a superseded or failed attempt 0) are dropped, once.
        var scoped = _persistence.SeedScoped;
        if (scoped is not null)
        {
            _persistence.SeedScoped = null;
            string[] keys = [.. scoped.Values.Keys];
            Log.ScopedSeedDropped(_log, keys);
        }

        attempt.Terminal = 2;
        DispatchSystem(attempt.Pending!, isFailure: attempt.Pending is HydrationFailed);
        _initDone.TrySetResult();
    }

    private Dictionary<string, object> Status(PersistenceState state) => new() { [_persistence.Key] = state };

    /// <summary>One hydration attempt (§11.5): its epoch, its key set and its terminal claim, all read under <c>_issue</c>.</summary>
    internal sealed class Attempt
    {
        /// <summary>The scope epoch the attempt reads for, and the epoch of its terminal.</summary>
        public int Epoch { get; set; }

        /// <summary>The slice keys it reads, which the writers' hydration skip leaves alone while it is Hydrating.</summary>
        public ImmutableHashSet<string> Keys { get; set; } = [];

        /// <summary>Whether it claimed its results: until then a superseding switch carries its unscoped keys (§11.6).</summary>
        public bool Restored { get; set; }

        /// <summary>
        /// A switch attempt's token source, linked to the store's, which ends it with the store: so never disposed on its own. Its
        /// superseding switch or the end of the init phase cancels it. The initial attempt has none: its token is the init deadline's.
        /// </summary>
        public CancellationTokenSource? Cancellation { get; init; }

        /// <summary>A switch attempt's own HydrationTimeout, armed iff the init phase had ended (§11.5 step 7); else none.</summary>
        public ITimer? Deadline { get; set; }

        /// <summary>Completed on the drainer once a switch's reset committed and its final snapshots were handed over.</summary>
        public TaskCompletionSource ResetCommitted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>0 open, 1 claimed, 2 issued.</summary>
        public int Terminal { get; set; }

        /// <summary>The claimed terminal action.</summary>
        public object? Pending { get; set; }
    }

    internal sealed record Persisted(Slice Slice, PersistOptions Options);
}

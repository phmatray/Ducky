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

    public PersistenceMiddleware(BlazorRegistration registration, PersistenceSlice persistence, IServiceProvider services)
    {
        (_registration, _persistence, _services) = (registration, persistence, services);
        _logger = (ILogger?)services.GetService<ILogger<PersistenceMiddleware>>() ?? NullLogger.Instance;
        _log = new(_logger);

        // The store scope's runtime (none in a host without JS) until a component hands over its renderer's (§6.10).
        _bridge = persistence.Gate.CreateBridge(services.GetService<IJSRuntime>(), _logger);
        _browser = new(_bridge, registration.Options, _logger);
        _time = services.GetRequiredService<TimeProvider>();
        _telemetry = new(_log, services.GetService<IMeterFactory>());
        persistence.Middleware = this;
    }

    /// <summary>The current hydration attempt (§11.5), null when nothing is readable; replaced only by a scope switch.</summary>
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
        // Server storage never goes through ducky.js: the cache provider reads it (§11.6). With nothing else persisted,
        // nothing happens and Status stays Hydrated.
        var slices = Store.Slices
            .Select(slice => (Slice: slice, Options: _registration.Persist.GetValueOrDefault(slice.GetType())?.Options))
            .Where(static persisted => persisted.Options is { Storage: not PersistStorage.Server })
            .Select(static persisted => new Persisted(persisted.Slice, persisted.Options!))
            .ToArray();
        if (slices.Length == 0)
        {
            return default;
        }

        // Browser storage is readable only in an interactive store (probing if nothing decided yet, §11.4). Otherwise
        // (prerender, static SSR) nothing is readable: no attempt, so no terminal.
        if (_persistence.Gate.Resolve(_registration.Options.IsBrowser, _bridge) != InteractivityMode.Interactive)
        {
            lock (_issue)
            {
                Store.Restore(Status(new(PersistenceStatus.NotStarted)), Origin.Hydration);
            }

            return default;
        }

        // Attempt 0: epoch 0 (no scope switch yet), its key set every readable key. The init-phase deadline and the init
        // token each end the attempt if it has not ended (step 7); the linked token only bounds the scope wait and the reads.
        var attempt = new Attempt(0, [.. slices.Select(static persisted => persisted.Slice.Key)]);
        _writers = slices.ToDictionary(static persisted => persisted.Slice.Key, persisted => new PersistenceWriter(this, persisted.Slice, persisted.Options));
        _initToken = cancellationToken;
        _deadline = new(_registration.Options.HydrationTimeout, _time);
        _hydration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _deadline.Token);
        lock (_issue)
        {
            (Current, _initPhase) = (attempt, true);
            Store.Restore(Status(new(PersistenceStatus.Hydrating) { ScopeEpoch = attempt.Epoch }), Origin.Hydration);

            // §6.7: registered here, in the synchronous prefix, after the Hydrating restore it must follow. On the init token
            // itself, never only through the linked token: once the deadline is cancelling that one on another thread, an
            // abort's Cancel() of it returns at once, and Abort would reach MarkReady before this callback took _issue.
            cancellationToken.UnsafeRegister(static self => ((PersistenceMiddleware)self!).EndInitPhase(), this);
            _deadline.Token.UnsafeRegister(static self => ((PersistenceMiddleware)self!).EndInitPhase(), this);
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
        // token. The flush (§11.5) runs before the module is disposed.
        _hydration?.Dispose();
        _deadline?.Dispose();
        await FlushAsync().ConfigureAwait(ConfigureAwaitOptions.None);

        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await _bridge.DisposeAsync().ConfigureAwait(false);
        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        await base.DisposeAsync().ConfigureAwait(false);
    }

    // §11.5 steps 4-7: resolve the scope, read every key of the attempt (nothing was written before it), wait for the seed
    // restore so it stays first in the queue (ADR-0011), then claim the terminal, set the baselines, one restore of what
    // was read and the one terminal. A failure claims a HydrationFailed instead; once the attempt's token is cancelled, the
    // deadline or the init abort ends the attempt (whichever of its callback and this catch comes first). The task never
    // completes before the attempt's terminal is queued.
    private async Task HydrateAsync(Persisted[] slices, Attempt attempt, CancellationToken token)
    {
        Dictionary<string, object> values = [];
        Dictionary<string, string> baselines = [];
        try
        {
            var reader = new EnvelopeReader(Store.Json, _logger);
            var prefix = await KeyPrefixAsync(attempt, token).ConfigureAwait(ConfigureAwaitOptions.None);

            // A scope delegate that ignored its token can resolve after the attempt ended: recorded, but nothing is read.
            token.ThrowIfCancellationRequested();

            // A null scope: no I/O for scoped keys, never a fallback to a shared key (§11.6).
            foreach (var (slice, options) in prefix is null ? [] : slices)
            {
                var key = $"{prefix}:{slice.Key}";
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                var text = await _browser.GetAsync(options.Storage, key, token).ConfigureAwait(false);

                // A slice that fails to read (malformed, newer, expired, a throwing constructor) keeps its state; the reader
                // logs it naming the key, and the other slices still restore. The baseline is the local serialization of
                // what was read (an unserializable state gets none).
                if (text is not null && reader.TryRead(text, key, slice.StateType, options.Version, options.Migrations, options.MaxAge, _time.GetUtcNow(), out var state))
                {
                    values[slice.Key] = state;
                    if (Store.Json.TrySerialize(state, slice.StateType, out var payload))
                    {
                        baselines[key] = payload;
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
            // A cancelled token is the deadline or the init abort. Its callback may not have run yet (callbacks run in
            // reverse registration order, so a read's continuation can come first), and this task's completion can let
            // StoreInitialized through: end the attempt here, before returning.
            if (token.IsCancellationRequested)
            {
                EndInitPhase();
                return;
            }

            lock (_issue)
            {
                Claim(attempt, new HydrationFailed(exception.GetType().Name, exception.Message) { ScopeEpoch = attempt.Epoch });
                IssueTerminal(attempt);
            }

            return;
        }

        // ponytail: attempt 0 is always current until scope switches (§11.6) arrive with their supersession check.
        lock (_issue)
        {
            // Lost to the deadline or the init abort: their HydrationFailed is the terminal, the results are discarded.
            if (!Claim(attempt, new HydrationCompleted(values.Count > 0) { ScopeEpoch = attempt.Epoch }))
            {
                Log.HydrationResultDiscarded(_log, attempt.Epoch);
                return;
            }

            foreach (var (key, payload) in baselines)
            {
                LastKnownPayload[key] = payload;
            }

            if (values.Count > 0)
            {
                Store.Restore(values, Origin.Hydration);
            }

            IssueTerminal(attempt);
        }
    }

    // {prefix} for unscoped keys; with a Scope, {prefix}:{scope} for the scope resolved for epoch 0 and recorded, or null
    // when it resolved null. In the browser the delegate gets the renderer's services, so it waits for the hand-off (§11.6);
    // the attempt's token bounds that wait, the resolution and the reads by the same HydrationTimeout. A delegate that ignores
    // the token only delays its own recording: init ends with the deadline's terminal all the same.
    private async Task<string?> KeyPrefixAsync(Attempt attempt, CancellationToken token)
    {
        var options = _registration.Options;
        if (options.Scope is not { } resolve)
        {
            return options.KeyPrefix;
        }

        var services = _services;
        if (options.IsBrowser)
        {
            var handedOver = new TaskCompletionSource<IServiceProvider>(TaskCreationOptions.RunContinuationsAsynchronously);
            _persistence.Gate.OnFirstRegistration(() => handedOver.TrySetResult(_persistence.Gate.Services!));
            services = await handedOver.Task.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.None);
        }

        // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
        var scope = await resolve(services, token).ConfigureAwait(false);
        lock (_issue)
        {
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

        return scope is null ? null : $"{options.KeyPrefix}:{scope}";
    }

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
                ? new HydrationFailed(nameof(OperationCanceledException), "Store init was aborted before hydration ended.")
                : new HydrationFailed(nameof(TimeoutException), $"Hydration did not end within HydrationTimeout ({_registration.Options.HydrationTimeout}).");
            Claim(attempt, failure with { ScopeEpoch = attempt.Epoch });
            IssueTerminal(attempt);
        }
    }

    // Under _issue: the first claim (0 -> 1) decides the attempt's terminal.
    private static bool Claim(Attempt attempt, object terminal)
    {
        if (attempt.Terminal != 0)
        {
            return false;
        }

        (attempt.Terminal, attempt.Pending) = (1, terminal);
        return true;
    }

    // Under _issue: issues the claimed terminal once (1 -> 2), which ends the init phase and disarms its timer (§11.5 step 7).
    // Disarmed, not disposed: this can run inside that timer's own callback, and the linked _hydration still holds its token.
    private void IssueTerminal(Attempt attempt)
    {
        if (attempt.Terminal != 1)
        {
            return;
        }

        if (_initPhase)
        {
            _initPhase = false;
            _deadline!.CancelAfter(Timeout.InfiniteTimeSpan);
        }

        attempt.Terminal = 2;
        DispatchSystem(attempt.Pending!, isFailure: attempt.Pending is HydrationFailed);
        _initDone.TrySetResult();
    }

    private Dictionary<string, object> Status(PersistenceState state) => new() { [_persistence.Key] = state };

    /// <summary>One hydration attempt (§11.5): its epoch, its key set and its terminal claim, all read under <c>_issue</c>.</summary>
    internal sealed class Attempt(int epoch, ImmutableHashSet<string> keys)
    {
        /// <summary>The scope epoch the attempt reads for, and the epoch of its terminal.</summary>
        public int Epoch => epoch;

        /// <summary>The slice keys it reads, which the writers' hydration skip leaves alone while it is Hydrating.</summary>
        public ImmutableHashSet<string> Keys => keys;

        /// <summary>0 open, 1 claimed, 2 issued.</summary>
        public int Terminal { get; set; }

        /// <summary>The claimed terminal action.</summary>
        public object? Pending { get; set; }
    }

    private sealed record Persisted(Slice Slice, PersistOptions Options);
}

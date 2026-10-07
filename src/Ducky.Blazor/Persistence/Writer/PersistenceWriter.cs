using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Ducky.Blazor;

// SPEC §11.5 "Writes": AfterReduce signals, one PersistenceWriter per persisted key writes (INV-16).
internal sealed partial class PersistenceMiddleware
{
    // §11.1: browser storage written from the server waits this long by default; in the browser nothing waits.
    private static TimeSpan ServerBrowserStorageDebounce => TimeSpan.FromMilliseconds(250);

    // §11.7: the store's one id for every JS registration, its writes included.
    private readonly string _id = Guid.NewGuid().ToString("N");
    private Dictionary<string, PersistenceWriter> _writers = [];
    private bool _signalling; // drainer only: set once AfterReduce has seen the Hydrating restore of the synchronous prefix

    /// <summary>The writer of each persisted key readable in this host, created by the synchronous init prefix.</summary>
    internal IReadOnlyDictionary<string, PersistenceWriter> Writers => _writers;

    /// <summary>The keys whose signal waits for a terminal (§11.5 "A skipped key stays dirty").</summary>
    internal ConcurrentDictionary<string, byte> Deferred { get; } = new();

    // Runs on the drainer. A terminal releases the deferred keys; a change of any origin but a restore signals its writer,
    // once the synchronous prefix has run (nothing is written before the first read: earlier changes are only deferred).
    public override void AfterReduce(ActionContext context)
    {
        if (context.State.Get<PersistenceState>().Status == PersistenceStatus.Hydrating)
        {
            _signalling = true;
        }
        else if (context.PreviousState.Get<PersistenceState>().Status == PersistenceStatus.Hydrating)
        {
            ReleaseDeferred();
        }

        if (context.Origin is Origin.Hydration or Origin.CrossTab or Origin.DevTools)
        {
            return;
        }

        foreach (var key in context.ChangedKeys)
        {
            if (_signalling)
            {
                Signal(key);
            }
            else
            {
                Deferred.TryAdd(key, 0);
            }
        }
    }

    private void Signal(string key) => _writers.GetValueOrDefault(key)?.Signal();

    // The two releases of _deferred (a terminal, a scope recording): each key is taken with TryRemove and signalled only by
    // the side that took it, so against a writer's own recheck it is signalled exactly once.
    private void ReleaseDeferred()
    {
        foreach (var key in Deferred.Keys)
        {
            if (Deferred.TryRemove(key, out _))
            {
                Signal(key);
            }
        }
    }

    // {prefix} for unscoped keys; with a Scope, {prefix}:{scope} for the scope recorded for the epoch, or null when that
    // scope is null or not recorded yet: no I/O for a key that can't be named.
    // A scope not recorded yet never reaches it: ShouldSkip defers that key (case (b)).
    private string? WritePrefix(int epoch)
    {
        var options = _registration.Options;
        if (options.Scope is null)
        {
            return options.KeyPrefix;
        }

        return Scopes.GetValueOrDefault(epoch) is { } scope ? $"{options.KeyPrefix}:{scope}" : null;
    }

    // §11.5 "hydration skip": the one predicate of the skip and of its recheck. (a) a read in flight owns the key: Hydrating,
    // and the key in the key set of the snapshot's epoch; (b) a scoped key whose epoch scope is not recorded yet can't be
    // named. Lock-free: Scopes is published before the drain that releases (b).
    // ponytail: every writer's key is in attempt 0's key set, so (a) is the Status alone and the predicate takes no key;
    // per-epoch key sets (and the key argument) come with scope switches (§11.6).
    private bool ShouldSkip(StateSnapshot snap)
    {
        var persistence = snap.Get<PersistenceState>();
        return persistence.Status == PersistenceStatus.Hydrating
            || (_registration.Options.Scope is not null && !Scopes.ContainsKey(persistence.ScopeEpoch));
    }

    /// <summary>
    /// One persisted key's write loop (§11.5): the channel carries a wake-up only, so signals raised during a debounce or a
    /// write coalesce into one more iteration, which reads the state current when it runs. One write per key is in flight.
    /// </summary>
    internal sealed class PersistenceWriter : IAsyncDisposable
    {
        private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly CancellationTokenSource _stop = new(); // its own: not linked to the store's lifetime
        private readonly PersistenceMiddleware _owner;
        private readonly Slice _slice;
        private readonly PersistOptions _options;
        private readonly TimeSpan _debounce;
        private readonly Task _loop;
        // PersistenceFailed.ErrorType of a state that could not be serialized.
        internal const string SerializationFailed = nameof(SerializationFailed);

        private bool _failing; // loop only: a failure streak has been reported
        private int _iterations;

        public PersistenceWriter(PersistenceMiddleware owner, Slice slice, PersistOptions options)
        {
            (_owner, _slice, _options) = (owner, slice, options);
            _debounce = options.Debounce ?? (owner._registration.Options.IsBrowser ? TimeSpan.Zero : ServerBrowserStorageDebounce);
            _loop = RunAsync(_stop.Token);
        }

        /// <summary>Whether a signal is waiting for the next iteration.</summary>
        internal bool Signalled => _signal.Reader.Count > 0;

        /// <summary>The iterations ended so far.</summary>
        internal int Iterations => Volatile.Read(ref _iterations);

        public void Signal() => _signal.Writer.TryWrite(true);

        // ponytail: dispose cancels the loop; the phase-5a flush of dirty keys comes with M6-11.
        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable VSTHRD003 // justification: the loop this writer started; it never faults and ends once its token is cancelled
            await _loop.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
            _stop.Dispose();
        }

        private async Task RunAsync(CancellationToken token)
        {
            try
            {
                while (true)
                {
                    // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                    await _signal.Reader.ReadAsync(token).ConfigureAwait(false);
                    if (_debounce > TimeSpan.Zero)
                    {
                        await Task.Delay(_debounce, _owner._time, token).ConfigureAwait(ConfigureAwaitOptions.None);
                    }

                    await WriteAsync(token).ConfigureAwait(ConfigureAwaitOptions.None);
                    Interlocked.Increment(ref _iterations);
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped.
            }
        }

        // One snapshot gives both the value and the key, so the last write is the final committed state (INV-07, INV-16).
        private async Task WriteAsync(CancellationToken token)
        {
            var snap = _owner.Store.State;
            if (_owner.ShouldSkip(snap))
            {
                Defer();
                return;
            }

            if (_owner.WritePrefix(snap.Get<PersistenceState>().ScopeEpoch) is not { } prefix)
            {
                return;
            }

            // The same state would fail again: reported, and the signal is consumed, so the next change is written (§10).
            // TrySerialize swallows the exception (§10) and R-PKG-2 forbids InternalsVisibleTo, so the error type is this
            // documented constant, not a .NET type name; DuckyJson's Debug log carries the exception itself.
            if (!_owner.Store.Json.TrySerialize(snap.Get(_slice.Key), _slice.StateType, out var payload))
            {
                Fail(SerializationFailed, $"The state of '{_slice.Key}' could not be serialized as {_slice.StateType.Name}.");
                return;
            }

            var key = $"{prefix}:{_slice.Key}";
            // Storage already holds this state: the key is clean, so a failure streak ends here as on a success.
            if (_owner.LastKnownPayload.TryGetValue(key, out var last) && last == payload)
            {
                _failing = false;
                return;
            }

            try
            {
                // ponytail: an interrupted write (a disconnect, an interop timeout) keeps the key dirty until its next change;
                // the retry with backoff comes with M6-11.
                var envelope = EnvelopeWriter.Write(payload, _options.Version, _owner._time.GetUtcNow());
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                if (!await _owner._browser.SetAsync(_options.Storage, key, envelope, _owner._id, token).ConfigureAwait(false))
                {
                    return;
                }
            }
#pragma warning disable CA1031 // justification: any provider failure is reported as PersistenceFailed and keeps the baseline (§11.5)
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Fail(exception.GetType().Name, exception.Message);
                return;
            }

            _owner.LastKnownPayload[key] = payload;
            _failing = false;
        }

        // §11.5 "A skipped key stays dirty": the skip consumed the signal, so the key joins _deferred.
        private void Defer()
        {
            var hook = _owner._registration.Options.AfterDeferHook;
            hook?.Invoke(_slice.Key, DeferPoint.BeforeAdd);
            _owner.Deferred.TryAdd(_slice.Key, 0);
            hook?.Invoke(_slice.Key, DeferPoint.AfterAdd);

            // Add-then-recheck: a release that ran between the snapshot read and the add found nothing to take. If the skip
            // no longer holds, take the key back; only a successful TryRemove signals, so a release that took it first is
            // the one signal.
            if (!_owner.ShouldSkip(_owner.Store.State) && _owner.Deferred.TryRemove(_slice.Key, out _))
            {
                Signal();
            }
        }

        // Once per failure streak, as a failure action (INV-12); a successful write ends the streak.
        private void Fail(string errorType, string message)
        {
            if (_failing)
            {
                return;
            }

            _failing = true;
            _owner.DispatchSystem(new PersistenceFailed(_slice.Key, errorType, message), isFailure: true);
        }
    }
}

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

    /// <summary>
    /// The keys whose signal waits for a terminal (§11.5 "A skipped key stays dirty"), each with its writer's clear count
    /// when it was deferred: a clear posted since discards it.
    /// </summary>
    internal ConcurrentDictionary<string, long> Deferred { get; } = new();

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

        if (context.Action is ClearPersistedState)
        {
            Clear(context.State);
        }

        foreach (var key in context.ChangedKeys)
        {
            if (_signalling)
            {
                Signal(key);
            }
            else
            {
                Defer(key, _writers.GetValueOrDefault(key)?.Clears ?? 0);
            }
        }
    }

    private void Signal(string key) => _writers.GetValueOrDefault(key)?.Signal();

    // The newest deferral wins: one made after a clear is never replaced by one a clear discards.
    private void Defer(string key, long clears) =>
        Deferred.AddOrUpdate(key, static (_, clears) => clears, static (_, old, clears) => Math.Max(old, clears), clears);

    // §11.5 ClearPersistedState: a removal to each persisted key's writer, keyed from the epoch of the snapshot the clear was
    // reduced against; a key whose scope is null or not recorded yet can't be named and is skipped. No prefix clear.
    private void Clear(StateSnapshot state)
    {
        var prefix = WritePrefix(state.Get<PersistenceState>().ScopeEpoch);
        foreach (var (key, writer) in _writers)
        {
            if (prefix is null)
            {
                Log.ClearSkipped(_log, key);
            }
            else
            {
                writer.Remove($"{prefix}:{key}");
            }
        }
    }

    // The two releases of _deferred (a terminal, a scope recording): each key is taken with TryRemove and signalled only by
    // the side that took it, so against a writer's own recheck it is signalled exactly once; a deferral older than the
    // writer's last clear is dropped instead (§11.5: the clear discards the signals raised before it).
    private void ReleaseDeferred()
    {
        foreach (var key in Deferred.Keys)
        {
            if (Deferred.TryRemove(key, out var clears))
            {
                _writers.GetValueOrDefault(key)?.Release(clears);
            }
        }
    }

    // {prefix} for unscoped keys; with a Scope, {prefix}:{scope} for the scope recorded for the epoch, or null when that
    // scope is null or not recorded yet: no I/O for a key that can't be named.
    // A write never meets a scope not recorded yet: ShouldSkip defers that key (case (b)).
    internal string? WritePrefix(int epoch)
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

    // §11.5 "Flush on DisposeAsync", phase 5a: the deferred keys are released, every loop skips its debounce and backoff
    // and makes one attempt per dirty key, bounded by DisposeTimeout; then the loops stop, and each key still dirty (or
    // still deferred) is counted ducky.persistence.lost.
    private async Task FlushAsync()
    {
        // The drain has exited: nothing else takes deferred keys any more. Released only once the Hydrating restore was
        // reduced: a restore detached by dispose step 1 left Store.State at the initial Hydrated status, so a key deferred
        // before the first read would be written over storage never read. Unreleased, it stays deferred and is counted lost;
        // released, its attempt meets the hydration skip again (or writes, once the read has ended).
        if (_signalling)
        {
            ReleaseDeferred();
        }

        var writers = _writers.ToArray();
        var flushed = Task.WhenAll(writers.Select(static writer => writer.Value.FlushAsync()));
        await flushed.WaitAsync(DisposeTimeout, _time).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        foreach (var (key, writer) in writers)
        {
            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
            await writer.DisposeAsync().ConfigureAwait(false);
            if (writer.Dirty || writer.RemovalPending || Deferred.ContainsKey(key))
            {
                _telemetry.Add(_telemetry.Lost);
            }
        }
    }

    // §11.5: a failed write is retried after 1 s, doubling up to 30 s.
    private static TimeSpan FirstBackoff => TimeSpan.FromSeconds(1);

    private static TimeSpan MaxBackoff => TimeSpan.FromSeconds(30);

    // How one attempt ended: the key is clean, or it stays dirty until a terminal signals it (the hydration skip), or until
    // a retry (a provider failure).
    private enum Outcome
    {
        Clean,
        Deferred,
        Failed,
    }

    /// <summary>
    /// One persisted key's write loop (§11.5): the channel carries a wake-up only, so signals raised during a debounce or a
    /// write coalesce into one more iteration, which reads the state current when it runs. One write per key is in flight.
    /// A failed write is retried with a backoff of 1 s doubling to 30 s; the dispose flush ends the debounce and the
    /// backoff, makes no further retry, and the loop ends once no signal is left.
    /// </summary>
    internal sealed class PersistenceWriter : IAsyncDisposable
    {
        private readonly Channel<bool> _signal = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
        private readonly CancellationTokenSource _stop = new(); // its own: not linked to the store's lifetime
        private readonly CancellationTokenSource _hurry; // the dispose flush: ends every wait (linked to _stop)
        private readonly PersistenceMiddleware _owner;
        private readonly Slice _slice;
        private readonly PersistOptions _options;
        private readonly TimeSpan _debounce;
        private readonly Task _loop;
        // PersistenceFailed.ErrorType of a state that could not be serialized.
        internal const string SerializationFailed = nameof(SerializationFailed);

        private readonly ConcurrentQueue<Removal> _removals = new(); // the pending removal commands, oldest first
        private readonly Lock _gate = new(); // orders a release against a removal's stamp
        private long _clears; // removal commands posted so far
        private TaskCompletionSource? _woken; // completed by the first command posted since the current attempt began

        private bool _failing; // loop only: a failure streak has been reported
        private int _iterations;
        private long _signals; // signals raised so far
        private long _clean; // the signals the last clean attempt covered: the key is dirty while they differ

        public PersistenceWriter(PersistenceMiddleware owner, Slice slice, PersistOptions options)
        {
            (_owner, _slice, _options) = (owner, slice, options);
            _debounce = options.Debounce ?? (owner._registration.Options.IsBrowser ? TimeSpan.Zero : ServerBrowserStorageDebounce);
            _hurry = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            _loop = RunAsync(_stop.Token, _hurry.Token);
        }

        /// <summary>Whether a signal is waiting for the next iteration.</summary>
        internal bool Signalled => _signal.Reader.Count > 0;

        /// <summary>The iterations ended so far.</summary>
        internal int Iterations => Volatile.Read(ref _iterations);

        /// <summary>Whether a change signalled to this key has not been written (or found already stored) yet.</summary>
        internal bool Dirty => Interlocked.Read(ref _signals) != Interlocked.Read(ref _clean);

        /// <summary>The removal commands posted so far: a deferral made before the last one is discarded.</summary>
        internal long Clears => Interlocked.Read(ref _clears);

        /// <summary>Whether a removal has not succeeded yet: storage may still hold the cleared value.</summary>
        internal bool RemovalPending => !_removals.IsEmpty;

        /// <summary>Whether the dispose flush has begun.</summary>
        internal bool Flushing => _hurry.IsCancellationRequested;

        // Counted before the wake-up: an attempt that reads the count and then the state covers every change it counted.
        public void Signal()
        {
            Interlocked.Increment(ref _signals);
            _signal.Writer.TryWrite(true);
        }

        // §11.5 ClearPersistedState, on the drainer: a removal of the exact key, stamped with the signals raised so far, which
        // it discards. Enqueued before _woken is read, and the loop publishes _woken before it reads the queue (both fenced):
        // the attempt serves the removal, or the backoff after it ends at once.
        public void Remove(string key)
        {
            lock (_gate)
            {
                Interlocked.Increment(ref _clears);
                _removals.Enqueue(new(key, Interlocked.Read(ref _signals)));
            }

            Volatile.Read(ref _woken)?.TrySetResult();
            _signal.Writer.TryWrite(true);
        }

        // A deferral's release (a terminal, a scope recording, the recheck): signalled unless a clear was posted since the
        // deferral. Under the gate, a release either comes before a clear, which then stamps its signal and discards it, or
        // after it, and is dropped: a change deferred before a clear never brings the key back.
        public void Release(long clears)
        {
            lock (_gate)
            {
                if (clears == _clears)
                {
                    Signal();
                }
            }
        }

        // The flush: every wait ends at once, and the loop ends after its next attempt, or at once when it has nothing left.
        public async Task FlushAsync()
        {
            await _hurry.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable VSTHRD003 // justification: the loop this writer started; it never faults
            await _loop.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        }

        public async ValueTask DisposeAsync()
        {
            await _stop.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable VSTHRD003 // justification: the loop this writer started; it never faults and ends once its token is cancelled
            await _loop.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
            _hurry.Dispose();
            _stop.Dispose();
        }

        private async Task RunAsync(CancellationToken stop, CancellationToken hurry)
        {
            try
            {
                while (await WaitAsync(hurry).ConfigureAwait(ConfigureAwaitOptions.None))
                {
                    await PauseAsync(_debounce, hurry).ConfigureAwait(ConfigureAwaitOptions.None);
                    var backoff = FirstBackoff;
                    // Definitely assigned up front: a mutant that removes the attempt's assignment must still compile, or
                    // Stryker's safe mode turns every mutant of this method into an unscored CompileError (SPEC §17.8).
                    var outcome = Outcome.Clean;
                    while (true)
                    {
                        // Once the flush has begun, this attempt is the key's last: no backoff follows it.
                        var last = hurry.IsCancellationRequested;
                        var woken = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Interlocked.Exchange(ref _woken, woken);
                        outcome = await AttemptAsync(stop).ConfigureAwait(ConfigureAwaitOptions.None);
                        if (outcome != Outcome.Failed || last)
                        {
                            break;
                        }

                        // A sleep a command ended resumes at the step it had reached.
                        if (await BackoffAsync(backoff, woken, hurry).ConfigureAwait(ConfigureAwaitOptions.None))
                        {
                            backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));
                        }
                    }

                    Interlocked.Increment(ref _iterations);

                    // A failure ends the retries only on the flush's last attempt: the key stays dirty and is done. A clean or
                    // deferred key goes back to WaitAsync, which still takes a signal raised meanwhile (a write's change, or
                    // Defer's recheck once the skip no longer holds) and otherwise ends the loop.
                    if (outcome == Outcome.Failed)
                    {
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stopped.
            }
        }

        // The next signal; once the flush has begun (ReadAsync then refuses at once), only one already raised, if any.
        private async Task<bool> WaitAsync(CancellationToken hurry)
        {
            try
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                await _signal.Reader.ReadAsync(hurry).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                return _signal.Reader.TryRead(out _);
            }
        }

        // A debounce or a backoff on the TimeProvider, which the flush ends early; once the loop is stopped, nothing follows.
        private async Task PauseAsync(TimeSpan delay, CancellationToken hurry)
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, _owner._time, hurry).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }

            _stop.Token.ThrowIfCancellationRequested();
        }

        // A retry backoff, which the flush ends, and so does a command posted since the failed attempt began (woken): the
        // next attempt then runs at once, the removal first. True when the full step was slept.
        private async Task<bool> BackoffAsync(TimeSpan delay, TaskCompletionSource woken, CancellationToken hurry)
        {
            using var sleep = CancellationTokenSource.CreateLinkedTokenSource(hurry);
            var slept = Task.Delay(delay, _owner._time, sleep.Token);
#pragma warning disable VSTHRD003 // justification: this loop's own RunContinuationsAsynchronously TCS, completed by a command or never
            var first = await Task.WhenAny(slept, woken.Task).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
            // Stryker disable once Statement : frees the timer of a sleep a command ended; left running, it only completes unobserved
            await sleep.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);
            _stop.Token.ThrowIfCancellationRequested();
            return first == slept;
        }

        // From the top: the pending removals in order, each discarding the signals raised before its clear and resetting the
        // baseline to absent, then the key if still dirty, so a change signalled before a clear never brings the key back.
        private async Task<Outcome> AttemptAsync(CancellationToken token)
        {
            while (_removals.TryPeek(out var removal))
            {
                if (!await DeliverAsync(() => _owner._browser.RemoveAsync(_options.Storage, removal.Key, _owner._id, token)).ConfigureAwait(ConfigureAwaitOptions.None))
                {
                    return Outcome.Failed;
                }

                _removals.TryDequeue(out _);
                _owner.LastKnownPayload.TryRemove(removal.Key, out _);
                Interlocked.Exchange(ref _clean, removal.Stamp);
                _failing = false;
            }

            return Dirty ? await WriteAsync(token).ConfigureAwait(ConfigureAwaitOptions.None) : Outcome.Clean;
        }

        // One snapshot gives both the value and the key, so the last write is the final committed state (INV-07, INV-16).
        private async Task<Outcome> WriteAsync(CancellationToken token)
        {
            var signals = Interlocked.Read(ref _signals);
            var clears = Clears; // read before the snapshot: a clear reduced after it makes this deferral stale
            var snap = _owner.Store.State;
            if (_owner.ShouldSkip(snap))
            {
                Defer(clears);
                return Outcome.Deferred;
            }

            var outcome = await WriteAsync(snap, token).ConfigureAwait(ConfigureAwaitOptions.None);
            if (outcome == Outcome.Clean)
            {
                Interlocked.Exchange(ref _clean, signals);
            }

            return outcome;
        }

        private async Task<Outcome> WriteAsync(StateSnapshot snap, CancellationToken token)
        {
            if (_owner.WritePrefix(snap.Get<PersistenceState>().ScopeEpoch) is not { } prefix)
            {
                return Outcome.Clean;
            }

            // The same state would fail again: reported, and the signal is consumed, so the next change is written (§10).
            // TrySerialize swallows the exception (§10) and R-PKG-2 forbids InternalsVisibleTo, so the error type is this
            // documented constant, not a .NET type name; DuckyJson's Debug log carries the exception itself.
            if (!_owner.Store.Json.TrySerialize(snap.Get(_slice.Key), _slice.StateType, out var payload))
            {
                Fail(SerializationFailed, $"The state of '{_slice.Key}' could not be serialized as {_slice.StateType.Name}.");
                return Outcome.Clean;
            }

            var key = $"{prefix}:{_slice.Key}";
            // Storage already holds this state: the key is clean, so a failure streak ends here as on a success.
            if (_owner.LastKnownPayload.TryGetValue(key, out var last) && last == payload)
            {
                _failing = false;
                return Outcome.Clean;
            }

            var started = _owner._time.GetTimestamp();
            var envelope = EnvelopeWriter.Write(payload, _options.Version, _owner._time.GetUtcNow());
            if (!await DeliverAsync(() => _owner._browser.SetAsync(_options.Storage, key, envelope, _owner._id, token)).ConfigureAwait(ConfigureAwaitOptions.None))
            {
                return Outcome.Failed;
            }

            // Telemetry first: a throwing listener never skips the baseline (SafeTelemetry, INV-03).
            _owner._telemetry.Record(_owner._telemetry.SaveDuration, _owner._time.GetElapsedTime(started).TotalMilliseconds);
            _owner._telemetry.Add(_owner._telemetry.Saves);
            _owner.LastKnownPayload[key] = payload;
            _failing = false;
            return Outcome.Clean;
        }

        // One provider call (a write or a removal): false when it failed, which keeps the baseline (§11.5). A disconnect or an
        // interop timeout is not delivered (Debug only, no PersistenceFailed); any other failure starts a streak.
        private async Task<bool> DeliverAsync(Func<ValueTask<bool>> call)
        {
            try
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                return await call().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // justification: any provider failure is reported as PersistenceFailed and keeps the baseline (§11.5)
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Fail(exception.GetType().Name, exception.Message);
                return false;
            }
        }

        // §11.5 "A skipped key stays dirty": the skip consumed the signal, so the key joins _deferred.
        private void Defer(long clears)
        {
            var hook = _owner._registration.Options.AfterDeferHook;
            hook?.Invoke(_slice.Key, DeferPoint.BeforeAdd);
            _owner.Defer(_slice.Key, clears);
            hook?.Invoke(_slice.Key, DeferPoint.AfterAdd);

            // Add-then-recheck: a release that ran between the snapshot read and the add found nothing to take. If the skip
            // no longer holds, take the key back; only a successful TryRemove signals, so a release that took it first is
            // the one signal.
            if (!_owner.ShouldSkip(_owner.Store.State) && _owner.Deferred.TryRemove(_slice.Key, out var taken))
            {
                Release(taken);
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

        // A removal command: the exact storage key, and the signals raised before the clear.
        private sealed record Removal(string Key, long Stamp);
    }
}

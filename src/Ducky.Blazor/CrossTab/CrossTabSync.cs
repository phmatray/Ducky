using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

// SPEC §11.7 (.NET CrossTabSync), INV-18, INV-23: owned by PersistenceMiddleware, not a registered middleware, because its
// restores and sequence bumps are issued under persistence's _issue, atomically with the hydration decisions (§11.5).
internal sealed partial class PersistenceMiddleware
{
    // The per-slice cross-tab sequence numbers (§11.5): swapped under _issue, read lock-free when a hydration read begins,
    // so step 6 drops a read that a newer cross-tab value overtook.
    private ImmutableDictionary<string, long> _sequences = ImmutableDictionary<string, long>.Empty;
    private CrossTabSync? _crossTab;

    /// <summary>
    /// The store's ducky.js storage registration (§11.7): one <c>watchStorage</c> per store, under the store id, before the
    /// first read, removed on dispose; <see cref="OnStorageChangedAsync"/> receives other tabs' storage events and other
    /// stores' writes in this document.
    /// </summary>
    internal sealed class CrossTabSync : IAsyncDisposable
    {
        private readonly PersistenceMiddleware _owner;
        private readonly Persisted[] _slices;
        private readonly EnvelopeReader _reader;
        private readonly DotNetObjectReference<CrossTabSync> _reference;
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<string, long> _latest = new(); // per storage key, its newest notification
        private long _notifications;

        public CrossTabSync(PersistenceMiddleware owner, Persisted[] slices)
        {
            (_owner, _slices) = (owner, slices);
            _reader = new(owner.Store.Json, owner._logger);
            _reference = DotNetObjectReference.Create(this);
            Volatile.Write(ref owner._bridge.Reimported, Watch);
        }

        /// <summary>
        /// How long ducky.js keeps an id whose deliveries keep failing (§11.7): beyond the default 3-minute
        /// DisconnectedCircuitRetentionPeriod; passed to <c>watchStorage</c>, so the module holds no magic number.
        /// </summary>
        internal static TimeSpan DeliveryPruneAfter => TimeSpan.FromMinutes(10);

        private static TimeSpan Debounce => TimeSpan.FromMilliseconds(50);

        // Issued, not awaited, so the synchronous prefix never waits on JS; on the same module task as the reads that follow.
        // Lost to a failed import (a disconnect during circuit init), it is issued again once a re-import succeeds (§11.5).
        public void Watch() => _ = WatchAsync();

        // All six arguments: ducky.js rejects a call without maxInlineBytes, the budget of every notification of this id
        // (§11.9). The outcome is observed: a rejection or any other fault is a Warning, never an unobserved fault. An
        // interrupted call is only the bridge's Debug log: this re-issues the registration after a failed import alone; one
        // lost to an interrupted watchStorage itself is repaired by the next write or removal, which ducky.js answers with
        // false for an unknown id (§11.7, M7-02b).
        private async Task WatchAsync()
        {
            var options = _owner._registration.Options;
            try
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                await _owner._bridge.TryInvokeAsync<bool>(
                    "watchStorage", CancellationToken.None, _owner._id, JsArg.Ref(_reference), options.KeyPrefix,
                    Array.Exists(_slices, static persisted => persisted.Options.SyncAcrossTabs),
                    (long)DeliveryPruneAfter.TotalMilliseconds, options.InlinePayloadBytes).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // justification: an issued, unawaited call; every fault is logged, none is left unobserved
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Log.CrossTabWatchFailed(_owner._log, exception, _owner._id);
            }
        }

        /// <summary>
        /// One storage notification (§11.7): <paramref name="source"/> "tab" (another tab's storage event, localStorage only)
        /// or "doc" (another store of this document wrote); a null <paramref name="key"/> re-reads every accepted key; a null
        /// <paramref name="value"/> is a removal unless <paramref name="tooLarge"/>, which pulls the value by stream. Never
        /// throws: a notification it can't use is ignored with a log.
        /// </summary>
        [JSInvokable("OnStorageChanged")]
        public Task OnStorageChangedAsync(string source, string area, string? key, string? value, bool tooLarge)
        {
            var accepted = Accepted(source, area);
            if (key is null)
            {
                return Task.WhenAll(accepted.Select(entry => ReceiveAsync(entry.Slice, entry.Key, token => _owner._browser.ReadAsync(entry.Slice.Options.Storage, entry.Key, token))));
            }

            // A plain local, not a pattern variable: no mutant of the test can leave it unassigned (Stryker Safe Mode, §17.8).
            // Persisted is a reference type, so a key no accepted entry names finds the default entry's null slice.
            var slice = Array.Find(accepted, entry => entry.Key == key).Slice;
            if (slice is null)
            {
                Log.CrossTabKeyIgnored(_owner._log, key);
                return Task.CompletedTask;
            }

            return ReceiveAsync(slice, key, tooLarge ? token => _owner._browser.PullAsync(area, key, token) : _ => new(value));
        }

        // Before the flush: no notification is restored once disposal began, and a re-import during the flush re-registers
        // nothing under the id DisposeAsync removes.
        public async Task StopAsync()
        {
            Volatile.Write(ref _owner._bridge.Reimported, null);
            await _stop.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);
        }

        // After the flush, whose writes still name this id (§11.7: storageSet answers false for an unknown id). A rejected
        // unwatchStorage (a failed re-import, a stale module) is logged: the reference and the module are still released.
        public async ValueTask DisposeAsync()
        {
            try
            {
                // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                await _owner._bridge.TryInvokeAsync<bool>("unwatchStorage", CancellationToken.None, _owner._id).ConfigureAwait(false);
            }
#pragma warning disable CA1031 // justification: a disposal never throws (§6.11); ducky.js prunes the id after DeliveryPruneAfter
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Log.CrossTabUnwatchFailed(_owner._log, exception, _owner._id);
            }

            _reference.Dispose();
        }

        // The exact current storage keys a notification may name: "tab" reaches synced slices, "doc" every browser-persisted
        // slice; both in the notification's area, under the scope recorded for the current epoch, read under _issue (a scoped
        // key names nothing while that scope is null or not recorded yet).
        private (Persisted Slice, string Key)[] Accepted(string source, string area)
        {
            var epoch = CurrentEpoch();
            return [.. _slices
                .Where(persisted => BrowserStorageProvider.Area(persisted.Options.Storage) == area && (source != "tab" || persisted.Options.SyncAcrossTabs))
                .Select(persisted => (Slice: persisted, Key: _owner.StorageKey(persisted.Slice.Key, persisted.Options, epoch)))
                .Where(static entry => entry.Key is not null)
                .Select(static entry => (entry.Slice, entry.Key!))];
        }

        // Returned from inside the lock: no mutant of an assignment can leave the epoch unassigned (Stryker Safe Mode, §17.8).
        private int CurrentEpoch()
        {
            lock (_owner._issue)
            {
                return _owner.Current!.Epoch;
            }
        }

        // 50 ms per key on the TimeProvider, then the prerender seed's settle (INV-15: once it is set the seed restore is
        // queued, so this one lands after it): a newer notification of the key supersedes this one, before its read and
        // again when it would be restored. Dispose ends both waits.
        private async Task ReceiveAsync(Persisted slice, string key, Func<CancellationToken, ValueTask<string?>> read)
        {
            var notification = Interlocked.Increment(ref _notifications);
            _latest[key] = notification;
            var token = _stop.Token;
            try
            {
                await Task.Delay(Debounce, _owner._time, token).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning disable VSTHRD003 // justification: the store's own RunContinuationsAsynchronously TCS, which the handoff always completes
                await _owner._persistence.SeedSettled.Task.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
                if (_latest[key] == notification)
                {
                    // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                    Apply(slice, key, notification, await read(token).ConfigureAwait(false));
                }
            }
#pragma warning disable CA1031 // justification: the stop ended the wait or the read (an interrupted read throws no OperationCanceledException)
            catch (Exception) when (token.IsCancellationRequested)
#pragma warning restore CA1031
            {
                // Disposed.
            }
#pragma warning disable CA1031 // justification: a [JSInvokable] handler never throws into ducky.js (§11.7); the notification is ignored
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Log.CrossTabNotificationFailed(_owner._log, exception, key);
            }
        }

        // A removal restores the initial state; a value stored but unreadable here (above MaxPayloadBytes, a rejected pull:
        // the provider logged it) is no removal, and is ignored. A value goes through the hydration EnvelopeReader (a newer version, an expired
        // or unreadable envelope, a throwing constructor: ignored, the reader logs it; so is a state this store can't
        // serialize back) and is dropped when its local re-serialization equals the baseline (an echo). Otherwise, under
        // _issue: bump the sequence number, restore, set the baseline, signal the writer, which dedupes a pending write of
        // the same state away. While the key's hydration read is open, the value is a Hydration restore: it does come from
        // storage, and step 6 drops that read.
        private void Apply(Persisted slice, string key, long notification, string? text)
        {
            var (type, options) = (slice.Slice.StateType, slice.Options);
            object? state = null;
            string? payload = null;
            if (text == BrowserStorageProvider.Unreadable)
            {
                return;
            }

            if (text is not null
                && !(_reader.TryRead(text, key, type, options.Version, options.Migrations, options.MaxAge, _owner._time.GetUtcNow(), out state)
                    && _owner.Store.Json.TrySerialize(state, type, out payload)))
            {
                return;
            }

            if (payload is not null && _owner.LastKnownPayload.TryGetValue(key, out var last) && last == payload)
            {
                return;
            }

            lock (_owner._issue)
            {
                if (_latest[key] != notification || _stop.IsCancellationRequested)
                {
                    return;
                }

                if (payload is null)
                {
                    _owner.Store.Restore(new Dictionary<string, object> { [slice.Slice.Key] = _owner.Store.InitialState.Get(slice.Slice.Key) }, Origin.CrossTab);
                    return;
                }

                // The key's read is open: the current attempt reads it and has claimed no terminal (§11.6 per-attempt key sets).
                // Stryker disable once Arithmetic : any step moves the number; step 6 compares it only for a change
                Volatile.Write(ref _owner._sequences, _owner._sequences.SetItem(slice.Slice.Key, _owner._sequences.GetValueOrDefault(slice.Slice.Key) + 1));
                _owner.Store.Restore(new Dictionary<string, object> { [slice.Slice.Key] = state! }, _owner.Current!.Terminal == 0 && _owner.Current.Keys.Contains(slice.Slice.Key) ? Origin.Hydration : Origin.CrossTab);
                _owner.LastKnownPayload[key] = payload;
                _owner.Signal(slice.Slice.Key);
            }
        }
    }
}

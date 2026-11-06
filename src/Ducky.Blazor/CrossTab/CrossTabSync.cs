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

    // The state each receipt restored with Origin.CrossTab, per slice key, until that restore reduces, committed or not. It can queue
    // behind another thread's drain, so its writer is signalled from AfterReduce, never when it is issued: a writer signalled
    // earlier would read the state from before the restore, find it unlike the new baseline, and write it back over the
    // value just received. Each receipt is its own object: a record is removed only by its own receipt.
    private readonly ConcurrentDictionary<string, Receipt> _received = new();

    // The receipts whose CrossTab restore was issued and has not reduced yet, in issue order (enqueued under _issue).
    private readonly ConcurrentQueue<Receipt> _restoring = new();

    // On the drainer, for a CrossTab restore (§11.7): restores reduce in issue order, so this is the oldest pending receipt's
    // when it committed that receipt's state, or when it committed nothing (a BeforeReduce threw, §6.4; a removal of a slice
    // already holding its initial state). Either way that receipt goes, and its key is signalled unless a newer receipt
    // recorded it since: a restore that commits nothing never holds the key's writes. A public restore with Origin.CrossTab
    // that commits its own state is not a receipt's.
    // ponytail: a public CrossTab restore that commits nothing is taken for the oldest receipt's, whose writer is then
    // signalled before its restore commits (the pre-M7-02b race); tag the receipts' actions if that ever matters.
    private void SignalReceived(ActionContext context)
    {
        if (_restoring.TryPeek(out var oldest)
            && (ReferenceEquals(context.State, context.PreviousState) || ReferenceEquals(context.State.Get(oldest.Key), oldest.State))
            && _restoring.TryDequeue(out _)
            && _received.TryRemove(new(oldest.Key, oldest)))
        {
            Signal(oldest.Key);
        }
    }

    // One receipt's CrossTab restore. A class, so compared by reference: a newer receipt of an equal state is another record.
    private sealed class Receipt(string key, object state)
    {
        public string Key => key;

        public object State => state;
    }

    /// <summary>
    /// The store's ducky.js storage registration (§11.7): one <c>watchStorage</c> per store, under the store id, before the
    /// first read, removed on dispose; <see cref="OnStorageChangedAsync(string, string, string, string, bool, string)"/>
    /// receives other tabs' storage events and other stores' writes in this document.
    /// </summary>
    internal sealed class CrossTabSync : IAsyncDisposable
    {
        private readonly PersistenceMiddleware _owner;
        private readonly Persisted[] _slices;
        private readonly EnvelopeReader _reader;
        private readonly DotNetObjectReference<CrossTabSync> _reference;
        private readonly CancellationTokenSource _stop = new();
        private readonly ConcurrentDictionary<string, long> _latest = new(); // per storage key, its newest notification

        // Per storage key, the baseline instance its last removal receipt set. Only that instance reads as "absent" (§11.7):
        // any write, read or value receipt stores a fresh serialization, so a stored value that merely serializes like the
        // initial state is no absence, and a removal still resets it (INV-18).
        private readonly ConcurrentDictionary<string, string> _removed = new();
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
        /// ducky.js delivers each notification of registration <paramref name="id"/> here (§11.7). A resumed circuit's old
        /// registration can hold the DotNetObject id the new circuit gave this store's reference, so its deliveries land on
        /// this store: one that names another id is rejected, so the module prunes that registration at its cap (INV-18)
        /// instead of delivering every event twice for good.
        /// </summary>
        [JSInvokable("OnStorageChanged")]
        public Task OnStorageChangedAsync(string source, string area, string? key, string? value, bool tooLarge, string id) =>
            id == _owner._id
                ? OnStorageChangedAsync(source, area, key, value, tooLarge)
                : Task.FromException(new InvalidOperationException($"A notification of registration '{id}' reached store '{_owner._id}'."));

        /// <summary>
        /// One storage notification (§11.7): <paramref name="source"/> "tab" (another tab's storage event, localStorage only)
        /// or "doc" (another store of this document wrote); a null <paramref name="key"/> re-reads every accepted key. A
        /// named key is re-read too once its debounce ends, by stream when <paramref name="tooLarge"/>: the forwarded
        /// <paramref name="value"/> may be stale by then (this store's own write, or a newer one, may have landed since), so
        /// it is never applied. Never throws: a notification it can't use is ignored with a log.
        /// </summary>
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

            return ReceiveAsync(slice, key, tooLarge ? token => _owner._browser.PullAsync(area, key, token) : token => _owner._browser.ReadAsync(slice.Options.Storage, key, token));
        }

        // Before the flush: no notification is restored once disposal began, and a re-import during the flush re-registers
        // nothing under the id DisposeAsync removes.
        public async Task StopAsync()
        {
            Volatile.Write(ref _owner._bridge.Reimported, null);
            await _stop.CancelAsync().ConfigureAwait(ConfigureAwaitOptions.None);

            // The drain has exited (INV-29): a restore still queued completed Disposed and never reduces, so no receipt's
            // record may defer the flush (the queue is never read again). A write that deferred to such a restore waits for
            // the signal its commit would have raised: each dirty writer whose record goes is signalled, once the records are
            // gone (a writer woken earlier would find its record and defer again), so the flush still takes it. A clean
            // writer is not: a signal would make it dirty, and the flush would write the state from before the receipt back
            // over the value just received. Under _issue: an Apply past its stop check has recorded its receipt by now.
            lock (_owner._issue)
            {
                var keys = _owner._received.Keys;
                _owner._received.Clear();
                foreach (var key in keys)
                {
                    if (_owner._writers.GetValueOrDefault(key) is { Dirty: true } writer)
                    {
                        writer.Signal();
                    }
                }
            }
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
        // slice; both in the notification's area, under the scope recorded for the current epoch, all read under _issue (a
        // scoped key names nothing while that scope is null or not recorded yet: a switch bumps the epoch under this lock).
        // Returned from inside the lock: no mutant of an assignment can leave the epoch unassigned (Stryker Safe Mode, §17.8).
        private (Persisted Slice, string Key)[] Accepted(string source, string area)
        {
            lock (_owner._issue)
            {
                var epoch = _owner.Current!.Epoch;
                return [.. _slices
                    .Where(persisted => BrowserStorageProvider.Area(persisted.Options.Storage) == area && (source != "tab" || persisted.Options.SyncAcrossTabs))
                    .Select(persisted => (Slice: persisted, Key: _owner.StorageKey(persisted.Slice.Key, persisted.Options, epoch)))
                    .Where(static entry => entry.Key is not null)
                    .Select(static entry => (entry.Slice, entry.Key!))];
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
                    // A read a call of this store overtook is read again at once: sent after that call, it finds what the call
                    // left. A read ambiguous while a call is unresolved is read again once it resolved. Each round follows a
                    // call of this store sent or resolved, so the loop ends when its writer is idle.
                    var (started, value) = Issue(key, read, token);
                    // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                    while (Apply(slice, key, notification, started, await value.ConfigureAwait(false)) is { } resolved)
                    {
                        if (!resolved.IsCompleted)
                        {
                            Log.CrossTabReceiptWaits(_owner._log, key);
                            // Stryker disable once Boolean : no SynchronizationContext is captured in tests; library awaits never resume on it
                            await resolved.WaitAsync(token).ConfigureAwait(false);
                        }

                        // A newer notification of the key superseded this one meanwhile: it reads the key itself.
                        if (_latest[key] != notification)
                        {
                            break;
                        }

                        (started, value) = Issue(key, read, token);
                    }
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

        // §11.7: the read is sent under _sends with the count of the calls this store's writers sent, which they send and
        // count under it too: the count names exactly the calls ducky.js runs before this read.
        private (long Started, Task<string?> Value) Issue(string key, Func<CancellationToken, ValueTask<string?>> read, CancellationToken token)
        {
            lock (_owner._sends)
            {
                return (_owner.Started.GetValueOrDefault(key), read(token).AsTask());
            }
        }

        // Absence is a payload (§11.7): a removal is the initial state, whose local serialization an absent key reads as. A
        // value stored but unreadable here (above MaxPayloadBytes, a rejected pull: the provider logged it) is no removal, and
        // is ignored. A value goes through the hydration EnvelopeReader (a newer version, an expired or unreadable envelope, a
        // throwing constructor: ignored, the reader logs it) and is dropped when its local re-serialization equals the
        // key's write in flight (its setItem ran before the read, and may have run before its result sets the baseline). A
        // value this store can't serialize back is ignored (no baseline could guard it). Then, under _issue: drop a
        // notification no longer current or whose key the current epoch no longer names (a scope switch since its
        // receipt); under _sends, read again at once when this store sent a call of the key after the read (a completed
        // task); when what was read is what the baseline names (a value equal to it, a removal over an absent baseline or
        // the one a removal receipt set), wait for a call of the key still in flight and read again (its task: delivered or
        // failed decides it), or with none in flight drop it as an echo (a value) or keep the slice (a removal, logged);
        // else bump the sequence number (a removal too, whatever its serialization, so it orders against a hydration
        // read), and restore, unless a removal finds an initial state it can't serialize (the serializer logs it); set
        // the baseline and signal the writer, which dedupes a pending write of the same state away.
        // While the key's hydration read is open, a value is a Hydration restore: it does come from storage, and step 6 drops
        // that read. A removal stays a CrossTab restore: the slice stays unrestored.
#pragma warning disable VSTHRD200 // justification: synchronous; returns what the receipt waits for before it reads again, or null when it is done
        private Task? Apply(Persisted slice, string key, long notification, long started, string? text)
#pragma warning restore VSTHRD200
        {
            var (sliceKey, type, options) = (slice.Slice.Key, slice.Slice.StateType, slice.Options);
            if (text == BrowserStorageProvider.Unreadable)
            {
                return null;
            }

            // A removal's serialization decides only its restore: its sequence bump does not depend on it (§11.7). An absence
            // read while this store's own removal is in flight is that removal, an echo: its result has yet to reset the baseline.
            var removal = text is null;
            if (removal && ReferenceEquals(_owner.InFlight.GetValueOrDefault(key), RemovalInFlight))
            {
                return null;
            }

            var state = removal ? _owner.Store.InitialState.Get(sliceKey) : null;
            string? payload = null;
            var serialized = removal
                ? _owner.Store.Json.TrySerialize(state!, type, out payload)
                : _reader.TryRead(text!, key, type, options.Version, options.Migrations, options.MaxAge, _owner._time.GetUtcNow(), out state)
                    && _owner.Store.Json.TrySerialize(state, type, out payload);
            if (!removal && (!serialized || _owner.InFlight.GetValueOrDefault(key) == payload))
            {
                return null;
            }

            lock (_owner._issue)
            {
                var attempt = _owner.Current!;

                if (_latest[key] != notification || _stop.IsCancellationRequested)
                {
                    return null;
                }

                if (_owner.StorageKey(sliceKey, options, attempt.Epoch) != key)
                {
                    Log.CrossTabKeyIgnored(_owner._log, key);
                    return null;
                }

                // The key's read is open: the current attempt reads it and has claimed no terminal (§11.6 per-attempt key sets).
                // Its writer defers while that read is open, so it is signalled at once; a CrossTab restore's once committed.
                // That restore is recorded before the baseline is set: a writer that read the new baseline sees the record
                // until the commit, and the committed slot after it (PersistenceWriter.WriteAsync).
                var hydration = !removal && attempt.Terminal == 0 && attempt.Keys.Contains(sliceKey);

                // Under _sends from the comparison to the baseline, so no call is sent in between: a write sent after it went
                // over the baseline this receipt set, and its success replaces it (PersistenceWriter.Acknowledge).
                lock (_owner._sends)
                {
                    // A write or removal this store sent after the read lands after the value read, which is stale: read
                    // again. A call sent after the comparison is a write in flight during the receipt, rewritten once its
                    // restore reduced.
                    if (_owner.Started.GetValueOrDefault(key) != started)
                    {
                        return Task.CompletedTask;
                    }

                    // What the baseline names: a value equal to it, or a removal over an absent baseline (or the one a
                    // removal receipt set: the key is still absent). With no call in flight, an echo. With one in flight (sent
                    // before the read, as the count did not move) it is ambiguous until the call resolves: if it was
                    // delivered, what was read landed after it, a newer change of another store; if it failed (a quota error,
                    // an interop timeout on a dropped circuit), storage still holds the baseline. The receipt waits for the
                    // call, then reads again over the baseline its result left.
                    var echo = removal
                        ? !_owner.LastKnownPayload.TryGetValue(key, out var last) || ReferenceEquals(last, _removed.GetValueOrDefault(key))
                        : _owner.LastKnownPayload.GetValueOrDefault(key) == payload;
                    if (echo && _owner.InFlight.ContainsKey(key))
                    {
#pragma warning disable VSTHRD003 // justification: the store's own RunContinuationsAsynchronously TCS, which the writer completes once it handled the call's result
                        return _owner.Resolved[key].Task;
#pragma warning restore VSTHRD003
                    }

                    if (echo && !removal)
                    {
                        return null;
                    }

                    // Stryker disable once Arithmetic : any step moves the number; step 6 compares it only for a change
                    Volatile.Write(ref _owner._sequences, _owner._sequences.SetItem(sliceKey, _owner._sequences.GetValueOrDefault(sliceKey) + 1));
                    if (removal && !serialized)
                    {
                        return null;
                    }

                    // The removal is an echo, and discards no local change still waiting for its first write.
                    if (echo)
                    {
                        Log.CrossTabRemovalKept(_owner._log, key);
                        return null;
                    }

                    if (hydration)
                    {
                        attempt.Received = attempt.Received.Add(sliceKey);
                        _owner.Signal(sliceKey);
                    }
                    else
                    {
                        var receipt = new Receipt(sliceKey, state!);
                        _owner._received[sliceKey] = receipt;
                        _owner._restoring.Enqueue(receipt);
                    }

                    if (removal)
                    {
                        _removed[key] = payload!;
                    }

                    // A write still in flight was sent before the read (the count did not move): what was read landed after
                    // it, so storage no longer holds that write, and a later read of its payload is another store's write.
                    _owner.LastKnownPayload[key] = payload!;
                    _owner.InFlight.TryRemove(key, out _);
                }

                _owner.Store.Restore(new Dictionary<string, object> { [sliceKey] = state! }, hydration ? Origin.Hydration : Origin.CrossTab);
            }

            return null;
        }

        // §11.7: storageSet or storageRemove answered false, so ducky.js pruned this id (its deliveries failed past
        // DeliveryPruneAfter) and the circuit came back: register again with the same six arguments (idempotent), then re-read
        // every key of each registered area, standing in for the notifications lost meanwhile. "doc" accepts every
        // browser-persisted key of the area, so a lost relay of an unsynced key is re-read too. Nothing once disposal began:
        // the flush's own writes name an id about to be removed.
        public void Repair()
        {
            if (_stop.IsCancellationRequested)
            {
                return;
            }

            Log.CrossTabReRegistered(_owner._log, _owner._id);
            Watch();
            foreach (var area in _slices.Select(static persisted => BrowserStorageProvider.Area(persisted.Options.Storage)).Distinct())
            {
                _ = OnStorageChangedAsync("doc", area, null, null, tooLarge: false);
            }
        }
    }
}

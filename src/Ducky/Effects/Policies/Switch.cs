namespace Ducky;

// The Switch policy (SPEC §6.6, D8, INV-11): each start installs a new slot with its own Cts, linked to the store lifetime,
// and supersedes the run in flight for the same key. Whoever takes a slot out of the dictionary owns its Cts: the run, if
// its own compare-and-remove succeeds; otherwise the drainer that replaced it, which cancels it off the drainer.
internal sealed partial class EffectRunner
{
    // Side-effect free: no user code runs and nothing is installed yet. Null for a policy without a slot (Merge); the
    // Exhaust marker and the Queue Done are described in ExhaustQueue.cs.
    internal Slot? NewSlot(CancellationToken lifetime) => Policy switch
    {
        Concurrency.Switch => new(CancellationTokenSource.CreateLinkedTokenSource(lifetime)),
        Concurrency.Exhaust => new(),
        Concurrency.Queue => new(done: new(TaskCreationOptions.RunContinuationsAsynchronously)),
        _ => null,
    };

    // On the drainer, inside the run's try/catch: the key function is user code, so its throw is EffectFailed for this
    // effect only. The factories only return the pre-built slot and record what they replaced; AddOrUpdate may call them
    // several times, and the last call before it succeeds is the one whose result was installed, so prev is exactly the
    // replaced slot (or null); a Switch prev is superseded here, a Queue prev is the predecessor the run waits for. Returns
    // the key object the dictionary stores (Slot.Key), the one the run removes with. Exhaust never replaces: it returns
    // null when the key's marker is held (the effect is dropped), else the key, which TryAdd stored.
    internal SlotKey? Install(object action, Slot slot, SafeLogger logger, out Slot? prev)
    {
        var key = new SlotKey(KeyOf(action) ?? _nullKey);
        Slot? replaced = null;
        if (Policy == Concurrency.Exhaust)
        {
            prev = null;
            return _slots.TryAdd(key, slot) ? key : null;
        }

        _slots.AddOrUpdate(
            key,
            _ =>
            {
                // Stryker disable once Statement : resets prev only when AddOrUpdate retries the add after an update
                // lost a race with a concurrent remove, an interleaving no deterministic test reaches
                replaced = null;
                slot.Key = key;
                return slot;
            },
            (_, old) =>
            {
                replaced = old;
                slot.Key = old.Key;
                return slot;
            });
        prev = replaced;
        if (prev?.Cts is { } cts)
        {
            Supersede(cts, logger);
        }

        return slot.Key;
    }

    // In the run's finally, in the normative order (§6.6): the compare-and-remove, the Queue Done, then the Switch Cts
    // disposal; the idle decrement follows in EndRun. The run owns its Cts if it never installed it (key is null: the key
    // function or its GetHashCode threw) or if its own compare-and-remove succeeds; otherwise the drainer that replaced it
    // owns and disposes it. The key is the stored key object, so the removal runs no user Equals on the run's own entry;
    // only a hash collision with a distinct key in the same bucket can call it, so Done completes even when it throws: a
    // queued successor never waits on a run that ended. Such a throw leaves the slot in place, its Cts undisposed (an
    // install replacing it takes it over), and reaches the run's finally, which logs it before the idle decrement.
    internal void Release(SlotKey? key, Slot slot)
    {
        bool owned;
        try
        {
            owned = key is null || _slots.TryRemove(KeyValuePair.Create(key, slot));
        }
        finally
        {
            slot.Done?.SetResult();
        }

        if (owned)
        {
            slot.Cts?.Dispose();
        }
    }

    // CancelAsync keeps user cancellation callbacks off the drainer, and the continuation disposes the Cts only after
    // every callback ran, so it is never cancelled after it was disposed; a callback fault is logged, never thrown.
    private static void Supersede(CancellationTokenSource cts, SafeLogger logger) =>
        _ = cts.CancelAsync().ContinueWith(
            cancel =>
            {
                cts.Dispose();
                if (cancel.Exception is { } exception)
                {
                    Log.CancelCallbackThrew(logger, exception);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}

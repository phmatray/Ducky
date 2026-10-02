namespace Ducky;

// The Switch policy (SPEC §6.6, D8, INV-11): each start installs a new slot with its own Cts, linked to the store lifetime,
// and supersedes the run in flight for the same key. Whoever takes a slot out of the dictionary owns its Cts: the run, if
// its own compare-and-remove succeeds; otherwise the drainer that replaced it, which cancels it off the drainer.
internal sealed partial class EffectRunner
{
    // Side-effect free: no user code runs and nothing is installed yet. Null for a policy without a slot (Merge).
    internal Slot? NewSlot(CancellationToken lifetime) =>
        Policy == Concurrency.Switch ? new(CancellationTokenSource.CreateLinkedTokenSource(lifetime)) : null;

    // On the drainer, inside the run's try/catch: the key function is user code, so its throw is EffectFailed for this
    // effect only. The factories only return the pre-built slot and record what they replaced; AddOrUpdate may call them
    // several times, and the last call before it succeeds is the one whose result was installed, so prev is exactly the
    // replaced slot (or null). Returns the key, never null once installed.
    internal SlotKey Install(object action, Slot slot, SafeLogger logger)
    {
        var key = new SlotKey(effect.KeyOf(action) ?? _nullKey);
        Slot? prev = null;
        _slots.AddOrUpdate(
            key,
            _ =>
            {
                // Stryker disable once Statement : resets prev only when AddOrUpdate retries the add after an update
                // lost a race with a concurrent remove, an interleaving no deterministic test reaches
                prev = null;
                return slot;
            },
            (_, old) =>
            {
                prev = old;
                return slot;
            });
        if (prev is not null)
        {
            Supersede(prev, logger);
        }

        return key;
    }

    // In the run's finally. The run owns its Cts if it never installed it (key is null: the key function or its
    // GetHashCode threw) or if its own compare-and-remove succeeds; otherwise the drainer that replaced it owns and
    // disposes it. Runs no user code (SlotKey), so it never throws and the idle decrement after it always runs.
    internal void Release(SlotKey? key, Slot slot)
    {
        if (key is null || _slots.TryRemove(KeyValuePair.Create(key, slot)))
        {
            slot.Cts.Dispose();
        }
    }

    // CancelAsync keeps user cancellation callbacks off the drainer, and the continuation disposes the Cts only after
    // every callback ran, so it is never cancelled after it was disposed; a callback fault is logged, never thrown.
    private static void Supersede(Slot prev, SafeLogger logger) =>
        _ = prev.Cts.CancelAsync().ContinueWith(
            cancel =>
            {
                prev.Cts.Dispose();
                if (cancel.Exception is { } exception)
                {
                    Log.CancelCallbackThrew(logger, exception);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
}

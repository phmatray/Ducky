using System.Collections.Concurrent;

namespace Ducky;

// One per effect registration per store (SPEC §6.6, INV-11): the effect, its policy (read once, at materialization) and
// its slots, keyed by a SlotKey over ConcurrencyKey(action) ?? NullKey (the private sentinel _nullKey). Every start
// installs a new immutable Slot, and a completing run removes only its own slot (compare-and-remove), so an old run never
// removes its successor's slot.
internal sealed partial class EffectRunner(Effect effect)
{
    private static readonly object _nullKey = new();

    private readonly ConcurrentDictionary<SlotKey, Slot> _slots =
        new(EqualityComparer<SlotKey>.Create(static (x, y) => SlotKey.Same(x!, y!), static k => k.Hash));

    internal Effect Effect => effect;

    internal Concurrency Policy { get; } = effect.RunPolicy;

    // The rule the effect tests assert: no non-LongRunning slot remains once WhenIdleAsync has completed.
    internal int SlotCount => _slots.Count;
}

// Immutable once published, one per start (§6.6). Switch: the run's Cts, linked to the store lifetime. Queue: Done,
// completed by the run's finally (RunContinuationsAsynchronously, never faulted). Exhaust: neither, the slot is only a
// running marker.
internal sealed class Slot(CancellationTokenSource? cts = null, TaskCompletionSource? done = null)
{
    internal CancellationTokenSource? Cts { get; } = cts;

    internal TaskCompletionSource? Done { get; } = done;

    // The key object the dictionary stores for this slot, set on the drainer by Install before the slot is published: the
    // run's own key on an add, the replaced slot's Key on a replace (AddOrUpdate keeps the stored key). The run removes
    // with it, so object.Equals short-circuits on the reference and no user Equals runs (§6.6).
    internal SlotKey? Key { get; set; }
}

// The installed key, its user GetHashCode taken once, at install, inside the run's try (§6.6), so the run's compare-and-remove
// in its finally never rehashes. The run removes with the stored key object (Slot.Key) and object.Equals short-circuits on
// the reference, so that removal runs no user code; only a hash collision with a distinct key can call the user's Equals,
// and EffectRunner.Release survives a throw there.
internal sealed class SlotKey(object key)
{
    internal int Hash { get; } = key.GetHashCode();

    private object Key => key;

    internal static bool Same(SlotKey x, SlotKey y) => Equals(x.Key, y.Key);
}

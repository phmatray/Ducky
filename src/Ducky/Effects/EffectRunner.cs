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

// Immutable, one per start (§6.6). Switch: the run's Cts, linked to the store lifetime.
internal sealed class Slot(CancellationTokenSource cts)
{
    internal CancellationTokenSource Cts { get; } = cts;
}

// The installed key, its user GetHashCode taken once, at install, inside the run's try (§6.6). The run's compare-and-remove
// in its finally then runs no user code: a cached hash, and object.Equals short-circuits on the reference of the key the run
// installed, so a key whose equality throws can never leave the finally before the idle decrement.
internal sealed class SlotKey(object key)
{
    internal int Hash { get; } = key.GetHashCode();

    private object Key => key;

    internal static bool Same(SlotKey x, SlotKey y) => Equals(x.Key, y.Key);
}

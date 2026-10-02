namespace Ducky;

/// <summary>Typed selection over one slice (SPEC §5.2).</summary>
public static class StoreExtensions
{
    /// <summary>
    /// Selects a value from the state of the slice whose state type is <typeparamref name="TState"/>, like
    /// <see cref="IStore.Select{T}"/>. The projector is skipped while that state's reference is unchanged since its last
    /// run; each call has a cache of its own.
    /// </summary>
    /// <typeparam name="TState">The slice's state type.</typeparam>
    /// <typeparam name="T">The selected value's type.</typeparam>
    /// <param name="store">The store to select from.</param>
    /// <param name="selector">Projects the slice state to the value; must be pure.</param>
    /// <param name="onChange">Called on the drainer with the new value, only when it changed; never under a lock.</param>
    /// <param name="comparer">Decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The selection; dispose it to unsubscribe <paramref name="onChange"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> or <paramref name="selector"/> is null.</exception>
    public static Selection<T> Select<TState, T>(
        this IStore store, Func<TState, T> selector, Action<T>? onChange = null, IEqualityComparer<T>? comparer = null)
        where TState : class
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(selector);

        // SPEC §5.2, §6.9: one immutable entry per call, swapped whole. Two threads may both compute; the last write wins,
        // and both results are valid because projectors are pure.
        Entry<TState, T>? entry = null;
        return store.Select(
            s =>
            {
                var slot = s.Get<TState>();
                var cached = Volatile.Read(ref entry);
                if (cached is not null && ReferenceEquals(cached.Slot, slot))
                {
                    return cached.Result;
                }

                var result = selector(slot);
                Volatile.Write(ref entry, new Entry<TState, T>(slot, result));
                return result;
            },
            onChange,
            comparer);
    }

    private sealed record Entry<TState, T>(TState Slot, T Result);
}

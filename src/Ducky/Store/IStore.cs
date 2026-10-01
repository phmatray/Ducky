namespace Ducky;

/// <summary>
/// The store. In the browser there is one per app; on the server there is one per DI scope (the request or the circuit),
/// so code resolving it from another scope gets a different, empty store.
/// </summary>
public interface IStore : IDispatcher
{
    /// <summary>
    /// Gets the current state: a lock-free read of the last committed snapshot. The first read starts init; when every
    /// init completes synchronously and no other drain is active, it already reflects <see cref="StoreInitialized"/>.
    /// Await <see cref="InitializeAsync"/> for the guarantee.
    /// </summary>
    StateSnapshot State { get; }

    /// <summary>Gets every slice's initial state, built once when the store was created (version 0).</summary>
    StateSnapshot InitialState { get; }

    /// <summary>Gets the store-owned slice instances in registration order. Reading them starts nothing.</summary>
    IReadOnlyList<Slice> Slices { get; }

    /// <summary>
    /// Replaces the state of the named slices in one action that bypasses the init buffer and is never vetoed. Each value is
    /// the slice's state instance; an unknown key or a value of another type restores nothing for that entry. Only
    /// <see cref="Origin.Hydration"/> marks a slice as restored (<see cref="StateSnapshot.WasRestored(string)"/>).
    /// Restoring does not start init.
    /// </summary>
    /// <param name="values">The new states by slice key, copied when this is called.</param>
    /// <param name="origin"><see cref="Origin.Hydration"/>, <see cref="Origin.CrossTab"/> or <see cref="Origin.DevTools"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="origin"/> is not a restore origin.</exception>
    void Restore(IReadOnlyDictionary<string, object> values, Origin origin);

    /// <summary>
    /// Starts init if it has not started, and waits until <see cref="StoreInitialized"/> has been processed. The first
    /// dispatch or <see cref="State"/> read starts init too, so calling this is optional.
    /// </summary>
    /// <param name="cancellationToken">Cancels this caller's wait only, never init.</param>
    /// <returns>A task that completes once <see cref="StoreInitialized"/> has been processed; it never faults.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

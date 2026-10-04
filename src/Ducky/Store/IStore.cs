namespace Ducky;

/// <summary>
/// The store. In the browser there is one per app; on the server there is one per DI scope (the request or the circuit),
/// so code resolving it from another scope gets a different, empty store.
/// </summary>
public interface IStore : IDispatcher, IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the current state: a lock-free read of the last committed snapshot. The first read starts init; when every
    /// init completes synchronously and no other drain is active, it already reflects <see cref="StoreInitialized"/>.
    /// Await <see cref="InitializeAsync"/> for the guarantee.
    /// </summary>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    StateSnapshot State { get; }

    /// <summary>Gets every slice's initial state, built once when the store was created (version 0).</summary>
    StateSnapshot InitialState { get; }

    /// <summary>Gets the store-owned slice instances in registration order. Reading them starts nothing.</summary>
    IReadOnlyList<Slice> Slices { get; }

    /// <summary>
    /// Selects a value from the state. The selection's <see cref="Selection{T}.Value"/> evaluates
    /// <paramref name="selector"/> against the current snapshot on each read. With <paramref name="onChange"/>, the
    /// drainer evaluates <paramref name="selector"/> after each commit that changed the snapshot and calls
    /// <paramref name="onChange"/> only when the selected value changed; a commit racing this call is never missed.
    /// Starts init. After disposal the selection is inert and reads the last snapshot.
    /// </summary>
    /// <typeparam name="T">The selected value's type.</typeparam>
    /// <param name="selector">Projects a snapshot to the value; must be pure.</param>
    /// <param name="onChange">Called on the drainer with the new value, only when it changed; never under a lock.</param>
    /// <param name="comparer">Decides whether the value changed; <see cref="EqualityComparer{T}.Default"/> when null.</param>
    /// <returns>The selection; dispose it to unsubscribe <paramref name="onChange"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
#pragma warning disable CA1716 // justification: Select is the one selection signature of the library (SPEC §5.2, §5.9)
    Selection<T> Select<T>(Func<StateSnapshot, T> selector, Action<T>? onChange = null, IEqualityComparer<T>? comparer = null);
#pragma warning restore CA1716

    /// <summary>Gets the store's JSON gateway, built from <see cref="DuckyBuilder.UseJson(System.Text.Json.JsonSerializerOptions)"/>.</summary>
    DuckyJson Json { get; }

    /// <summary>
    /// Replaces the state of the named slices in one action that bypasses the init buffer and is never vetoed. Each value is
    /// the slice's state instance or a <see cref="System.Text.Json.JsonElement"/>, which is cloned when this is called (so
    /// the caller may dispose its <see cref="System.Text.Json.JsonDocument"/>) and deserialized with the slice's declared
    /// state type through <see cref="Json"/>. An unknown key, a value of another type or a value that fails to deserialize
    /// restores nothing for that entry, and the other entries still restore. Only
    /// <see cref="Origin.Hydration"/> marks a slice as restored (<see cref="StateSnapshot.WasRestored(string)"/>).
    /// Restoring does not start init.
    /// </summary>
    /// <param name="values">The new states by slice key, copied when this is called.</param>
    /// <param name="origin"><see cref="Origin.Hydration"/>, <see cref="Origin.CrossTab"/> or <see cref="Origin.DevTools"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="origin"/> is not a restore origin.</exception>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    void Restore(IReadOnlyDictionary<string, object> values, Origin origin);

    /// <summary>
    /// Starts init if it has not started, and waits until <see cref="StoreInitialized"/> has been processed. The first
    /// dispatch or <see cref="State"/> read starts init too, so calling this is optional.
    /// </summary>
    /// <param name="cancellationToken">Cancels this caller's wait only, never init.</param>
    /// <returns>A task that completes once <see cref="StoreInitialized"/> has been processed; it never faults.</returns>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts init if it has not started, and waits until the store is idle: initialized, with nothing queued, no
    /// drain running and no effect run in progress (runs of a <see cref="Effect.LongRunning"/> effect are not waited
    /// for). Use it after <see cref="IDispatcher.DispatchAsync(object)"/> to also wait for what that action caused,
    /// effects included. After disposal it completes at once. A middleware that awaits it from its init waits until
    /// init times out, because idle requires initialization.
    /// </summary>
    /// <param name="cancellationToken">Cancels this caller's wait only, never init or any other wait.</param>
    /// <returns>A task that completes once the store is idle or disposed; it never faults.</returns>
    /// <exception cref="DuckyConfigurationException">
    /// DUCKY353: the store's effects or middleware could not be constructed. Every call rethrows the same cached
    /// instance, never once disposal has begun.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Called from a run of one of this store's effects that is not <see cref="Effect.LongRunning"/>: that run would
    /// wait for itself forever.
    /// </exception>
    Task WhenIdleAsync(CancellationToken cancellationToken = default);
}

namespace Ducky;

/// <summary>
/// The store. In the browser there is one per app; on the server there is one per DI scope (the request or the circuit),
/// so code resolving it from another scope gets a different, empty store.
/// </summary>
public interface IStore : IDispatcher
{
    /// <summary>Gets the current state: a lock-free read of the last committed snapshot.</summary>
    StateSnapshot State { get; }

    /// <summary>Gets every slice's initial state, built once when the store was created (version 0).</summary>
    StateSnapshot InitialState { get; }

    /// <summary>Gets the store-owned slice instances in registration order. Reading them starts nothing.</summary>
    IReadOnlyList<Slice> Slices { get; }
}

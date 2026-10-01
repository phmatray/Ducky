using System.Diagnostics.CodeAnalysis;

namespace Ducky;

/// <summary>
/// An immutable view of every slice's state, taken from one commit. Reads take no lock.
/// </summary>
public sealed class StateSnapshot
{
    private readonly Registry _registry;
    private readonly object[] _states;
    private readonly ulong[] _restored;

    // The initial snapshot (Version 0): every slice's Initial, nothing restored.
    internal StateSnapshot(Registry registry)
        : this(
            registry,
            Array.ConvertAll(registry.Slices, slice => slice.InitialState),
            // Stryker disable once Arithmetic : the / to * mutant only over-allocates (equivalent); Slice64UsesSecondWord kills the + to - one
            new ulong[(registry.Slices.Length + 63) / 64],
            0)
    {
    }

    private StateSnapshot(Registry registry, object[] states, ulong[] restored, long version)
    {
        _registry = registry;
        _states = states;
        _restored = restored;
        Version = version;
    }

    /// <summary>Gets the commit version: 0 for the initial state, then one more for each commit that changed something.</summary>
    public long Version { get; }

    /// <summary>Gets the slice keys, in registration order.</summary>
    public IReadOnlyList<string> Keys => _registry.Keys;

    /// <summary>Gets the state of the slice whose declared state type is exactly <typeparamref name="TState"/>.</summary>
    /// <typeparam name="TState">The declared state type of a registered slice.</typeparam>
    /// <returns>The slice's state in this snapshot.</returns>
    /// <exception cref="DuckyConfigurationException">No slice holds <typeparamref name="TState"/> (DUCKY350).</exception>
    public TState Get<TState>()
        where TState : class => (TState)_states[Ordinal<TState>()];

    /// <summary>Gets the state of the slice whose declared state type is exactly <typeparamref name="TState"/>, if there is one.</summary>
    /// <typeparam name="TState">The state type to look up.</typeparam>
    /// <param name="state">The slice's state, or <see langword="null"/> when no slice holds <typeparamref name="TState"/>.</param>
    /// <returns><see langword="true"/> when a slice holds <typeparamref name="TState"/>.</returns>
    public bool TryGet<TState>([NotNullWhen(true)] out TState? state)
        where TState : class
    {
        var found = _registry.ByStateType.TryGetValue(typeof(TState), out var ordinal);
        state = found ? (TState)_states[ordinal] : null;
        return found;
    }

    /// <summary>Gets the state of the slice with the given key.</summary>
    /// <param name="key">A registered slice key.</param>
    /// <returns>The slice's state in this snapshot.</returns>
    /// <exception cref="KeyNotFoundException">No slice has <paramref name="key"/>.</exception>
    public object Get(string key) => _states[_registry.ByKey[key]];

    /// <summary>Gets whether the slice holding <typeparamref name="TState"/> was restored with <see cref="Origin.Hydration"/> in this store.</summary>
    /// <typeparam name="TState">The declared state type of a registered slice.</typeparam>
    /// <returns><see langword="true"/> when the slice was restored from storage, a prerender seed or a server cache.</returns>
    /// <exception cref="DuckyConfigurationException">No slice holds <typeparamref name="TState"/> (DUCKY350).</exception>
    public bool WasRestored<TState>()
        where TState : class => IsRestored(Ordinal<TState>());

    /// <summary>Gets whether the slice with the given key was restored with <see cref="Origin.Hydration"/> in this store.</summary>
    /// <param name="key">A registered slice key.</param>
    /// <returns><see langword="true"/> when the slice was restored from storage, a prerender seed or a server cache.</returns>
    /// <exception cref="KeyNotFoundException">No slice has <paramref name="key"/>.</exception>
    public bool WasRestored(string key) => IsRestored(_registry.ByKey[key]);

    // SPEC §6.2, §6.4 step 7: clones the slot array only when a slot changes reference, and the restored bitset only when a
    // Hydration-origin commit sets a new bit; when neither happens it returns this snapshot, so the caller publishes nothing.
    // Each change is compared with the slot as written so far in this commit, so for a repeated ordinal the last write wins.
    internal StateSnapshot Commit(ReadOnlySpan<(int Ordinal, object State)> changes, Origin origin)
    {
        object[]? states = null;
        ulong[]? restored = null;
        var hydration = origin == Origin.Hydration;
        foreach (var (ordinal, state) in changes)
        {
            if (!ReferenceEquals((states ?? _states)[ordinal], state))
            {
                (states ??= (object[])_states.Clone())[ordinal] = state;
            }

            if (hydration && !IsRestored(ordinal))
            {
                (restored ??= (ulong[])_restored.Clone())[ordinal >> 6] |= 1UL << ordinal;
            }
        }

        return states is null && restored is null ? this : new(_registry, states ?? _states, restored ?? _restored, Version + 1);
    }

    private int Ordinal<TState>()
        where TState : class =>
        _registry.ByStateType.TryGetValue(typeof(TState), out var ordinal)
            ? ordinal
            : throw new DuckyConfigurationException([DuckyErrors.UnregisteredState(typeof(TState))]);

    // A ulong shift count is taken modulo 64, so 1UL << ordinal is the ordinal's bit within its word.
    private bool IsRestored(int ordinal) => (_restored[ordinal >> 6] & (1UL << ordinal)) != 0;

    // The state at a registry ordinal: the dispatcher's lookup-free read (SPEC §6.4 step 6).
    internal object this[int ordinal] => _states[ordinal];
}

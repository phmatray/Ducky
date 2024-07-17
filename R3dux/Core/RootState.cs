using System.Collections.Immutable;
using R3dux.Exceptions;

namespace R3dux;

/// <summary>
/// Represents the root state of the application, managing slice states.
/// </summary>
public sealed record RootState
{
    private readonly ImmutableSortedDictionary<string, object> _state;

    /// <summary>
    /// Initializes a new instance of the <see cref="RootState"/> class.
    /// </summary>
    /// <param name="state">The state dictionary.</param>
    public RootState(ImmutableSortedDictionary<string, object> state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    /// <summary>
    /// Gets the underlying state dictionary for serialization purposes.
    /// </summary>
    public ImmutableSortedDictionary<string, object> GetStateDictionary()
        => _state;

    /// <summary>
    /// Gets the slice state associated with the specified key.
    /// </summary>
    /// <typeparam name="TState">The type of the state to select.</typeparam>
    /// <param name="key">The key of the state to select.</param>
    /// <returns>The state associated with the specified key.</returns>
    /// <exception cref="R3duxException">Thrown when the state is not of the expected type.</exception>
    public TState GetSliceState<TState>(string key)
        where TState : notnull
    { 
        ArgumentNullException.ThrowIfNull(key);
        
        if (_state.TryGetValue(key, out var value) && value is TState state)
        {
            return state;
        }

        throw new R3duxException($"State with key '{key}' is not of type '{typeof(TState).Name}'.");
    }
    
    /// <summary>
    /// Gets the slice state of the specified type.
    /// </summary>
    /// <typeparam name="TState">The type of the state to select.</typeparam>
    /// <returns>The state of the specified type.</returns>
    /// <exception cref="R3duxException">Thrown when the state is not found.</exception>
    public TState GetSliceState<TState>()
        where TState : notnull
    { 
        // take the first state of the specified type
        foreach (var value in _state.Values)
        {
            if (value is TState state)
            {
                return state;
            }
        }
        
        throw new R3duxException($"State of type '{typeof(TState).Name}' not found.");
    }

    /// <summary>
    /// Determines whether the state contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the state.</param>
    /// <returns><c>true</c> if the state contains an element with the key; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key)
        => _state.ContainsKey(key);
}

using System.Collections.Concurrent;
using R3dux.Exceptions;

namespace R3dux;

/// <summary>
/// Represents the root state of the application, managing slice states.
/// </summary>
public record RootState
{
    private readonly ConcurrentDictionary<string, object> _state = [];
    
    /// <summary>
    /// Gets the underlying state dictionary for serialization purposes.
    /// </summary>
    internal IDictionary<string, object> GetStateDictionary() => _state;

    /// <summary>
    /// Gets or sets the state associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the state to get or set.</param>
    /// <returns>The state associated with the specified key.</returns>
    /// <exception cref="R3duxException">Thrown when the state is not of the expected type.</exception>
    public object this[string key]
    {
        get => Select<object>(key);
        set => AddOrUpdateSliceState(key, value);
    }
    
    /// <summary>
    /// Adds or updates the state associated with the specified key.
    /// </summary>
    /// <param name="key">The key of the state to add or update.</param>
    /// <param name="initialState">The initial state to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when the key or initial state is null.</exception>
    public void AddOrUpdateSliceState(string key, object initialState)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(initialState);
        
        _state[key] = initialState;
    }

    /// <summary>
    /// Selects the state associated with the specified key.
    /// </summary>
    /// <typeparam name="TState">The type of the state to select.</typeparam>
    /// <param name="key">The key of the state to select.</param>
    /// <returns>The state associated with the specified key.</returns>
    /// <exception cref="R3duxException">Thrown when the state is not of the expected type.</exception>
    public TState Select<TState>(string key)
        where TState : notnull
    { 
        if (_state.TryGetValue(key, out var value) && value is TState state)
        {
            return state;
        }

        throw new R3duxException($"State with key '{key}' is not of type '{typeof(TState).Name}'.");
    }

    /// <summary>
    /// Determines whether the state contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the state.</param>
    /// <returns><c>true</c> if the state contains an element with the key; otherwise, <c>false</c>.</returns>
    public bool ContainsKey(string key)
        => _state.ContainsKey(key);
}
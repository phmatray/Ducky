// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Represents the root state of the application, managing slice states.
/// </summary>
public sealed record RootState : IStateProvider
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

    /// <inheritdoc/>
    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        return _state;
    }

    /// <inheritdoc/>
    public ImmutableSortedSet<string> GetKeys()
    {
        return _state.Keys.ToImmutableSortedSet();
    }

    /// <inheritdoc/>
    /// <exception cref="DuckyException">Thrown when the state is not of the expected type.</exception>
    public TState GetSlice<TState>(string key)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        return _state.TryGetValue(key, out object? value) && value is TState state
            ? state
            : throw new DuckyException($"State with key '{key}' is not of type '{typeof(TState).Name}'.");
    }

    /// <summary>
    /// Gets the state of a specific slice by its type (from IRootState).
    /// </summary>
    /// <exception cref="DuckyException">Thrown when the state is not found.</exception>
    public TState GetSlice<TState>()
        where TState : notnull
    {
        // take the first state of the specified type
        foreach (object value in _state.Values)
        {
            if (value is TState state)
            {
                return state;
            }
        }

        throw new DuckyException($"Slice of type {typeof(TState).Name} not found.");
    }

    /// <summary>
    /// Gets the state of a specific slice by its type (from IStateProvider).
    /// </summary>
    TState IStateProvider.GetSlice<TState>()
    {
        // take the first state of the specified type
        foreach (object value in _state.Values)
        {
            if (value is TState state)
            {
                return state;
            }
        }

        throw new DuckyException($"Slice of type {typeof(TState).Name} not found.");
    }

    /// <summary>
    /// Gets the state of a specific slice by its key (from IStateProvider).
    /// </summary>
    TState IStateProvider.GetSliceByKey<TState>(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return _state.TryGetValue(key, out object? value) && value is TState state
            ? state
            : throw new DuckyException($"State with key '{key}' is not of type '{typeof(TState).Name}'.");
    }

    /// <summary>
    /// Attempts to get the state of a specific slice by its type (from IStateProvider).
    /// </summary>
    public bool TryGetSlice<TState>(out TState? state)
    {
        foreach (object value in _state.Values)
        {
            if (value is TState foundState)
            {
                state = foundState;
                return true;
            }
        }

        state = default;
        return false;
    }

    /// <summary>
    /// Checks whether a slice of the specified type exists (from IStateProvider).
    /// </summary>
    public bool HasSlice<TState>()
    {
        return _state.Values.OfType<TState>().Any();
    }

    /// <summary>
    /// Checks whether a slice with the specified key exists (from IStateProvider).
    /// </summary>
    public bool HasSliceByKey(string key)
    {
        return _state.ContainsKey(key);
    }

    /// <summary>
    /// Gets all slice keys (from IStateProvider).
    /// </summary>
    public IReadOnlyCollection<string> GetSliceKeys()
    {
        return _state.Keys.ToList();
    }

    /// <summary>
    /// Gets all slices as key-value pairs (from IStateProvider).
    /// </summary>
    public IReadOnlyDictionary<string, object> GetAllSlices()
    {
        return _state;
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key)
    {
        return _state.ContainsKey(key);
    }
}

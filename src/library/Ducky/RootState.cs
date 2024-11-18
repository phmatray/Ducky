// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Represents the root state of the application, managing slice states.
/// </summary>
public sealed record RootState : IRootState
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
    public TState GetSliceState<TState>(string key)
        where TState : notnull
    {
        ArgumentNullException.ThrowIfNull(key);

        return _state.TryGetValue(key, out var value) && value is TState state
            ? state
            : throw new DuckyException($"State with key '{key}' is not of type '{typeof(TState).Name}'.");
    }

    /// <inheritdoc/>
    /// <exception cref="DuckyException">Thrown when the state is not found.</exception>
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

        throw new DuckyException($"State of type '{typeof(TState).Name}' not found.");
    }

    /// <inheritdoc/>
    public bool ContainsKey(string key)
    {
        return _state.ContainsKey(key);
    }
}

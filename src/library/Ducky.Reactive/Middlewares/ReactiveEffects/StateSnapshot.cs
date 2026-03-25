// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky.Reactive.Middlewares.ReactiveEffects;

/// <summary>
/// An immutable point-in-time snapshot of application state.
/// Used to ensure reactive effects see consistent state corresponding to a specific action.
/// </summary>
internal sealed class StateSnapshot : IStateProvider
{
    private readonly ImmutableSortedDictionary<string, object> _state;

    public StateSnapshot(ImmutableSortedDictionary<string, object> state)
    {
        _state = state;
    }

    public TState GetSlice<TState>()
    {
        string? key = _state.Keys.FirstOrDefault(k => _state[k] is TState);
        if (key is not null)
        {
            return (TState)_state[key];
        }

        throw new InvalidOperationException($"Slice of type {typeof(TState).Name} not found in state snapshot.");
    }

    public TState GetSliceByKey<TState>(string key)
    {
        if (_state.TryGetValue(key, out object? value) && value is TState state)
        {
            return state;
        }

        throw new KeyNotFoundException($"Slice with key '{key}' not found in state snapshot.");
    }

    public bool TryGetSlice<TState>(out TState? state)
    {
        string? key = _state.Keys.FirstOrDefault(k => _state[k] is TState);
        if (key is not null)
        {
            state = (TState)_state[key];
            return true;
        }

        state = default;
        return false;
    }

    public bool HasSlice<TState>()
    {
        return _state.Values.Any(v => v is TState);
    }

    public bool HasSliceByKey(string key)
    {
        return _state.ContainsKey(key);
    }

    public IReadOnlyCollection<string> GetSliceKeys()
    {
        return _state.Keys.ToList();
    }

    public IReadOnlyDictionary<string, object> GetAllSlices()
    {
        return _state;
    }

    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        return _state;
    }

    public ImmutableSortedSet<string> GetKeys()
    {
        return _state.Keys.ToImmutableSortedSet();
    }
}

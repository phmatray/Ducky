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
    private readonly Dictionary<Type, string> _typeIndex;

    public StateSnapshot(
        ImmutableSortedDictionary<string, object> state,
        Dictionary<Type, string> typeIndex)
    {
        _state = state;
        _typeIndex = typeIndex;
    }

    public TState GetSlice<TState>()
    {
        if (_typeIndex.TryGetValue(typeof(TState), out string? key)
            && _state.TryGetValue(key, out object? value)
            && value is TState state)
        {
            return state;
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
        if (_typeIndex.TryGetValue(typeof(TState), out string? key)
            && _state.TryGetValue(key, out object? value)
            && value is TState typedState)
        {
            state = typedState;
            return true;
        }

        state = default;
        return false;
    }

    public bool HasSlice<TState>()
    {
        return _typeIndex.ContainsKey(typeof(TState));
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

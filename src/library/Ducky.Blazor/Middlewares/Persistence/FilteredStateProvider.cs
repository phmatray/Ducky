using System.Collections.Immutable;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// A wrapper that provides a filtered view of the state.
/// </summary>
internal sealed class FilteredStateProvider : IStateProvider
{
    private readonly IStateProvider _originalProvider;
    private readonly ImmutableSortedDictionary<string, object> _filteredState;

    public FilteredStateProvider(IStateProvider originalProvider, ImmutableSortedDictionary<string, object> filteredState)
    {
        _originalProvider = originalProvider;
        _filteredState = filteredState;
    }

    public TState GetSlice<TState>()
    {
        // Try to get from filtered state first
        string? key = _filteredState.Keys.FirstOrDefault(k => _filteredState[k] is TState);
        if (key is not null)
        {
            return (TState)_filteredState[key];
        }

        throw new InvalidOperationException($"Slice of type {typeof(TState).Name} not found in filtered state.");
    }

    public TState GetSliceByKey<TState>(string key)
    {
        if (_filteredState.TryGetValue(key, out object? value) && value is TState state)
        {
            return state;
        }

        throw new KeyNotFoundException($"Slice with key '{key}' not found in filtered state.");
    }

    public bool TryGetSlice<TState>(out TState? state)
    {
        string? key = _filteredState.Keys.FirstOrDefault(k => _filteredState[k] is TState);
        if (key is not null)
        {
            state = (TState)_filteredState[key];
            return true;
        }

        state = default;
        return false;
    }

    public bool HasSlice<TState>()
    {
        return _filteredState.Values.Any(v => v is TState);
    }

    public bool HasSliceByKey(string key)
    {
        return _filteredState.ContainsKey(key);
    }

    public IReadOnlyCollection<string> GetSliceKeys()
    {
        return _filteredState.Keys.ToList();
    }

    public IReadOnlyDictionary<string, object> GetAllSlices()
    {
        return _filteredState;
    }

    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        return _filteredState;
    }

    public ImmutableSortedSet<string> GetKeys()
    {
        return _filteredState.Keys.ToImmutableSortedSet();
    }
}

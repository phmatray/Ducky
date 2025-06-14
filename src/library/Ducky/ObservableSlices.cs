// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Manages a collection of slices and provides state management.
/// </summary>
public sealed class ObservableSlices : IStateProvider, IDisposable
{
    private readonly Dictionary<string, ISlice> _slices = [];
    private readonly List<EventHandler> _sliceUpdateHandlers = [];

    /// <summary>
    /// Occurs when any slice state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? SliceStateChanged;

    /// <summary>
    /// Gets an enumerable collection of all registered slices.
    /// </summary>
    public IEnumerable<ISlice> AllSlices
        => _slices.Values;

    /// <summary>
    /// Adds a new slice with the specified key and data.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        _slices[slice.GetKey()] = slice;

        // Create handler for slice updates
        EventHandler handler = (sender, _) =>
        {
            if (sender is not ISlice updatedSlice)
            {
                return;
            }

            object newState = updatedSlice.GetState();
            SliceStateChanged?.Invoke(
                this,
                new StateChangedEventArgs(
                    updatedSlice.GetKey(),
                    newState.GetType(),
                    newState));
        };
        _sliceUpdateHandlers.Add(handler);
        slice.StateUpdated += handler;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Unsubscribe all handlers
        int index = 0;
        foreach (ISlice slice in _slices.Values)
        {
            if (index < _sliceUpdateHandlers.Count)
            {
                slice.StateUpdated -= _sliceUpdateHandlers[index];
            }

            index++;
        }

        _sliceUpdateHandlers.Clear();
        _slices.Clear();
        SliceStateChanged = null;
    }

    #region IStateProvider Implementation

    /// <inheritdoc />
    public TState GetSlice<TState>()
    {
        ISlice? slice = _slices.Values.FirstOrDefault(s => s.GetState() is TState);
        if (slice is null)
        {
            throw new InvalidOperationException($"Slice of type {typeof(TState).Name} not found.");
        }

        return (TState)slice.GetState();
    }

    /// <inheritdoc />
    public TState GetSliceByKey<TState>(string key)
    {
        if (!_slices.TryGetValue(key, out ISlice? slice))
        {
            throw new KeyNotFoundException($"Slice with key '{key}' not found.");
        }

        return (TState)slice.GetState();
    }

    /// <inheritdoc />
    public bool TryGetSlice<TState>(out TState? state)
    {
        ISlice? slice = _slices.Values.FirstOrDefault(s => s.GetState() is TState);
        if (slice is not null)
        {
            state = (TState)slice.GetState();
            return true;
        }

        state = default;
        return false;
    }

    /// <inheritdoc />
    public bool HasSlice<TState>()
    {
        return _slices.Values.Any(s => s.GetState() is TState);
    }

    /// <inheritdoc />
    public bool HasSliceByKey(string key)
    {
        return _slices.ContainsKey(key);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSliceKeys()
    {
        return _slices.Keys.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetAllSlices()
    {
        return _slices.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetState());
    }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        return _slices.ToImmutableSortedDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetState());
    }

    /// <inheritdoc />
    public ImmutableSortedSet<string> GetKeys()
    {
        return _slices.Keys.ToImmutableSortedSet();
    }

    #endregion
}

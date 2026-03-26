// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Manages a collection of slices and provides state management.
/// </summary>
public sealed class ObservableSlices : IStateProvider, IDisposable
{
    private readonly Dictionary<string, ISlice> _slices = [];
    private readonly Dictionary<Type, ISlice> _slicesByStateType = [];
    private readonly Dictionary<string, EventHandler> _sliceUpdateHandlers = [];

    /// <summary>
    /// Occurs when any slice state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? SliceStateChanged;

    /// <summary>
    /// Gets the number of registered slices.
    /// </summary>
    public int Count => _slices.Count;

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

        string key = slice.GetKey();
        Type stateType = slice.GetStateType();

        // If replacing an existing slice with the same key,
        // update the type index to point to the new instance.
        if (_slices.TryGetValue(key, out ISlice? existingSlice))
        {
            Type existingType = existingSlice.GetStateType();
            if (_slicesByStateType.TryGetValue(existingType, out ISlice? indexedSlice)
                && ReferenceEquals(indexedSlice, existingSlice))
            {
                _slicesByStateType[existingType] = slice;
            }
        }

        _slices[key] = slice;
        _slicesByStateType.TryAdd(stateType, slice);

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
        _sliceUpdateHandlers[slice.GetKey()] = handler;
        slice.StateUpdated += handler;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Unsubscribe all handlers by explicit key pairing
        foreach ((string key, EventHandler handler) in _sliceUpdateHandlers)
        {
            if (_slices.TryGetValue(key, out ISlice? slice))
            {
                slice.StateUpdated -= handler;
            }
        }

        _sliceUpdateHandlers.Clear();
        _slices.Clear();
        _slicesByStateType.Clear();
        SliceStateChanged = null;
    }

    #region IStateProvider Implementation

    /// <inheritdoc />
    public TState GetSlice<TState>()
    {
        if (!_slicesByStateType.TryGetValue(
            typeof(TState), out ISlice? slice))
        {
            throw new InvalidOperationException(
                $"Slice of type {typeof(TState).Name} not found.");
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
        if (_slicesByStateType.TryGetValue(
            typeof(TState), out ISlice? slice))
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
        return _slicesByStateType.ContainsKey(typeof(TState));
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

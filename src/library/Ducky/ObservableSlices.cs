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
    private readonly ReaderWriterLockSlim _rwLock = new();
    private ImmutableSortedDictionary<string, object>? _cachedStateDictionary;
    private Dictionary<Type, string>? _cachedTypeIndex;
    private volatile bool _stateDirty = true;

    /// <summary>
    /// Occurs when any slice state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? SliceStateChanged;

    /// <summary>
    /// Gets the number of registered slices.
    /// </summary>
    public int Count
    {
        get
        {
            _rwLock.EnterReadLock();
            try
            {
                return _slices.Count;
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Gets an enumerable collection of all registered slices.
    /// </summary>
    public IEnumerable<ISlice> AllSlices
    {
        get
        {
            _rwLock.EnterReadLock();
            try
            {
                return _slices.Values.ToList();
            }
            finally
            {
                _rwLock.ExitReadLock();
            }
        }
    }

    /// <summary>
    /// Adds a new slice with the specified key and data.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        string key = slice.GetKey();
        Type stateType = slice.GetStateType();

        _rwLock.EnterWriteLock();
        try
        {
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

            // Invalidate cache when slices change
            _stateDirty = true;

            // Create handler for slice updates
            EventHandler handler = (sender, _) =>
            {
                if (sender is not ISlice updatedSlice)
                {
                    return;
                }

                _stateDirty = true;

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
        finally
        {
            _rwLock.ExitWriteLock();
        }
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
        _rwLock.Dispose();
    }

    #region IStateProvider Implementation

    /// <inheritdoc />
    public TState GetSlice<TState>()
    {
        _rwLock.EnterReadLock();
        try
        {
            if (!_slicesByStateType.TryGetValue(
                typeof(TState), out ISlice? slice))
            {
                throw ExceptionFactory.SliceNotFound(typeof(TState));
            }

            return (TState)slice.GetState();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public TState GetSliceByKey<TState>(string key)
    {
        _rwLock.EnterReadLock();
        try
        {
            if (!_slices.TryGetValue(key, out ISlice? slice))
            {
                throw ExceptionFactory.SliceKeyNotFound(key);
            }

            return (TState)slice.GetState();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public bool TryGetSlice<TState>(out TState? state)
    {
        _rwLock.EnterReadLock();
        try
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
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public bool HasSlice<TState>()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _slicesByStateType.ContainsKey(typeof(TState));
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public bool HasSliceByKey(string key)
    {
        _rwLock.EnterReadLock();
        try
        {
            return _slices.ContainsKey(key);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetSliceKeys()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _slices.Keys.ToList().AsReadOnly();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object> GetAllSlices()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _slices.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetState());
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <inheritdoc />
    public ImmutableSortedDictionary<string, object> GetStateDictionary()
    {
        return GetSnapshotData().State;
    }

    /// <summary>
    /// Returns a cached snapshot of the current state dictionary and type index.
    /// Rebuilds only when the state is marked dirty.
    /// </summary>
    public (ImmutableSortedDictionary<string, object> State, Dictionary<Type, string> TypeIndex) GetSnapshotData()
    {
        _rwLock.EnterReadLock();
        try
        {
            if (!_stateDirty && _cachedStateDictionary is not null && _cachedTypeIndex is not null)
            {
                return (_cachedStateDictionary, _cachedTypeIndex);
            }
        }
        finally
        {
            _rwLock.ExitReadLock();
        }

        _rwLock.EnterWriteLock();
        try
        {
            // Double-check after acquiring write lock
            if (!_stateDirty && _cachedStateDictionary is not null && _cachedTypeIndex is not null)
            {
                return (_cachedStateDictionary, _cachedTypeIndex);
            }

            ImmutableSortedDictionary<string, object>.Builder builder = ImmutableSortedDictionary.CreateBuilder<string, object>();
            Dictionary<Type, string> typeIndex = new(_slices.Count);

            foreach ((string key, ISlice slice) in _slices)
            {
                object state = slice.GetState();
                builder.Add(key, state);
                typeIndex.TryAdd(state.GetType(), key);
            }

            _cachedStateDictionary = builder.ToImmutable();
            _cachedTypeIndex = typeIndex;
            _stateDirty = false;

            return (_cachedStateDictionary, _cachedTypeIndex);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public ImmutableSortedSet<string> GetKeys()
    {
        _rwLock.EnterReadLock();
        try
        {
            return _slices.Keys.ToImmutableSortedSet();
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    #endregion
}

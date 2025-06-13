// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;

namespace Ducky;

/// <summary>
/// Manages a collection of slices and provides root state management.
/// </summary>
public sealed class ObservableSlices : IDisposable
{
    private readonly Dictionary<string, ISlice> _slices = [];
    private readonly List<EventHandler> _sliceUpdateHandlers = [];
    private IRootState _currentState;

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableSlices"/> class.
    /// </summary>
    public ObservableSlices()
    {
        _currentState = CreateRootState();
    }

    /// <summary>
    /// Occurs when the root state changes.
    /// </summary>
    public event EventHandler<StateChangedEventArgs>? RootStateChanged;

    /// <summary>
    /// Gets an enumerable collection of all registered slices.
    /// </summary>
    public IEnumerable<ISlice> AllSlices
        => _slices.Values;

    /// <summary>
    /// Gets the current root state.
    /// </summary>
    public IRootState CurrentState => _currentState;

    /// <summary>
    /// Adds a new slice with the specified key and data.
    /// </summary>
    /// <param name="slice">The slice to add.</param>
    public void AddSlice(ISlice slice)
    {
        ArgumentNullException.ThrowIfNull(slice);

        _slices[slice.GetKey()] = slice;

        // Create handler for slice updates
        EventHandler handler = (_, _) => UpdateRootState();
        _sliceUpdateHandlers.Add(handler);
        slice.StateUpdated += handler;
        
        UpdateRootState();
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
        RootStateChanged = null;
    }

    /// <summary>
    /// Creates a new root state based on the current slices.
    /// </summary>
    /// <returns>A new <see cref="RootState"/> object.</returns>
    private RootState CreateRootState()
    {
        ImmutableSortedDictionary<string, object> state = _slices.ToImmutableSortedDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.GetState());

        return new RootState(state);
    }

    /// <summary>
    /// Updates the root state.
    /// </summary>
    private void UpdateRootState()
    {
        IRootState previousState = _currentState;
        _currentState = CreateRootState();
        RootStateChanged?.Invoke(this, new StateChangedEventArgs(_currentState, previousState));
    }
}

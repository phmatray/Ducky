// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public interface IStore
{
    /// <summary>
    /// Occurs when the root state changes.
    /// </summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the current root state of the application.
    /// </summary>
    IRootState CurrentState { get; }

    /// <summary>
    /// Gets a value indicating whether the store has been initialized.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the UTC time when the store was created.
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the number of registered slices.
    /// </summary>
    int SliceCount { get; }

    /// <summary>
    /// Gets the state of a specific slice by its type.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <returns>The current state of the slice.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the slice is not found.</exception>
    TState GetSlice<TState>();

    /// <summary>
    /// Gets the state of a specific slice by its key.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <param name="key">The slice key.</param>
    /// <returns>The current state of the slice.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the slice key is not found.</exception>
    TState GetSliceByKey<TState>(string key);

    /// <summary>
    /// Attempts to get the state of a specific slice by its type.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <param name="state">When this method returns, contains the state of the slice if found; otherwise, the default value.</param>
    /// <returns><c>true</c> if the slice was found; otherwise, <c>false</c>.</returns>
    bool TryGetSlice<TState>(out TState? state);

    /// <summary>
    /// Checks whether a slice of the specified type exists in the store.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <returns><c>true</c> if the slice exists; otherwise, <c>false</c>.</returns>
    bool HasSlice<TState>();

    /// <summary>
    /// Gets the names of all registered slices.
    /// </summary>
    /// <returns>A read-only list of slice names.</returns>
    IReadOnlyList<string> GetSliceNames();

    /// <summary>
    /// Subscribes to changes of a specific slice.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <param name="callback">The callback to invoke when the slice changes.</param>
    /// <returns>A disposable that unsubscribes the callback when disposed.</returns>
    IDisposable WhenSliceChanges<TState>(Action<TState> callback);

    /// <summary>
    /// Subscribes to changes of a specific slice with a transformation function.
    /// </summary>
    /// <typeparam name="TState">The type of the slice state.</typeparam>
    /// <typeparam name="TResult">The type of the transformed result.</typeparam>
    /// <param name="selector">A function to transform the slice state.</param>
    /// <param name="callback">The callback to invoke with the transformed value when the slice changes.</param>
    /// <returns>A disposable that unsubscribes the callback when disposed.</returns>
    IDisposable WhenSliceChanges<TState, TResult>(Func<TState, TResult> selector, Action<TResult> callback);
}

/// <summary>
/// Provides data for the <see cref="IStore.StateChanged"/> event.
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new state.
    /// </summary>
    public IRootState NewState { get; }

    /// <summary>
    /// Gets the previous state.
    /// </summary>
    public IRootState? PreviousState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="newState">The new state.</param>
    /// <param name="previousState">The previous state.</param>
    public StateChangedEventArgs(IRootState newState, IRootState? previousState = null)
    {
        NewState = newState;
        PreviousState = previousState;
    }
}

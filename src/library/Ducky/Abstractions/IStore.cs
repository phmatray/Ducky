// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents a store that manages application state and handles actions.
/// </summary>
public interface IStore : IStateProvider
{
    /// <summary>
    /// Occurs when the state changes.
    /// </summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;

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
    /// Gets the key of the slice that changed.
    /// </summary>
    public string SliceKey { get; }

    /// <summary>
    /// Gets the type of the slice that changed.
    /// </summary>
    public Type SliceType { get; }

    /// <summary>
    /// Gets the new state of the slice.
    /// </summary>
    public object NewState { get; }

    /// <summary>
    /// Gets the previous state of the slice.
    /// </summary>
    public object? PreviousState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="sliceKey">The key of the slice that changed.</param>
    /// <param name="sliceType">The type of the slice that changed.</param>
    /// <param name="newState">The new state of the slice.</param>
    /// <param name="previousState">The previous state of the slice.</param>
    public StateChangedEventArgs(string sliceKey, Type sliceType, object newState, object? previousState = null)
    {
        SliceKey = sliceKey;
        SliceType = sliceType;
        NewState = newState;
        PreviousState = previousState;
    }
}

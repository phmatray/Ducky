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

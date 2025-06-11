// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents a state slice with basic state management capabilities.
/// </summary>
public interface ISlice
{
    /// <summary>
    /// Occurs when the state is updated.
    /// </summary>
    event EventHandler? StateUpdated;

    /// <summary>
    /// Gets the unique key for this state slice.
    /// </summary>
    /// <returns>The unique key as a string.</returns>
    string GetKey();

    /// <summary>
    /// Gets the type of the state managed by this slice.
    /// </summary>
    /// <returns>The type of the state.</returns>
    Type GetStateType();

    /// <summary>
    /// Gets the current state of this slice.
    /// </summary>
    /// <returns>The current state as an object.</returns>
    object GetState();

    /// <summary>
    /// Handles the dispatch of an action.
    /// </summary>
    /// <param name="action">The action to be dispatched.</param>
    void OnDispatch(object action);
}

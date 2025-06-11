// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Defines the contract for a dispatcher that can dispatch actions and provide events for dispatched actions.
/// </summary>
public interface IDispatcher
{
    /// <summary>
    /// Occurs when an action is dispatched.
    /// </summary>
    event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

    /// <summary>
    /// Gets the last action that was dispatched.
    /// </summary>
    object? LastAction { get; }

    /// <summary>
    /// Dispatches the specified action.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher has been disposed.</exception>
    void Dispatch(object action);
}

/// <summary>
/// Provides data for the <see cref="IDispatcher.ActionDispatched"/> event.
/// </summary>
public class ActionDispatchedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the action that was dispatched.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDispatchedEventArgs"/> class.
    /// </summary>
    /// <param name="action">The action that was dispatched.</param>
    public ActionDispatchedEventArgs(object action)
    {
        Action = action;
    }
}

// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents an effect that react to an action and dispatch new actions.
/// </summary>
public interface IEffect
{
    /// <summary>
    /// Gets the last action that was dispatched.
    /// </summary>
    IAction? LastAction { get; }

    /// <summary>
    /// Handles the specified action and dispatches new actions.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    /// <param name="rootState"></param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task HandleAsync(object action, IRootState rootState);

    /// <summary>
    /// Determines whether the effect can handle the specified action.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns><c>true</c> if the effect can handle the action; otherwise, <c>false</c>.</returns>
    bool CanHandle(object action);

    /// <summary>
    /// Sets the dispatcher used to dispatch new actions.
    /// </summary>
    /// <param name="dispatcher">The dispatcher to use.</param>
    public void SetDispatcher(IDispatcher dispatcher);

    /// <summary>
    /// Dispatches the specified action.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher has been disposed.</exception>
    void Dispatch(IAction action);
}

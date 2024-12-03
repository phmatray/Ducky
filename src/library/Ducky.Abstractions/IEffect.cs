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
    /// Handles the specified action and dispatches new actions.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    /// <param name="dispatcher">The dispatcher to use to dispatch new actions.</param>
    /// <param name="rootState"></param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task HandleAsync(object action, IDispatcher dispatcher, IRootState rootState);

    /// <summary>
    /// Determines whether the effect can handle the specified action.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns><c>true</c> if the effect can handle the action; otherwise, <c>false</c>.</returns>
    bool CanHandle(object action);
}

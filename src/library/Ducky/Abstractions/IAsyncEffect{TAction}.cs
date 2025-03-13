// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <summary>
/// Represents an effect that react to an action and dispatch new actions.
/// </summary>
/// <typeparam name="TAction"></typeparam>
public interface IAsyncEffect<TAction>
{
    /// <summary>
    /// Handles the specified action and dispatches new actions.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    /// <param name="rootState">The current root state of the application.</param>
    /// <param name="dispatcher">The dispatcher used to dispatch new actions.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task HandleAsync(TAction action, IRootState rootState, IDispatcher dispatcher);
}

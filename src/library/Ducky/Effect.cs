// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky;

/// <inheritdoc />
public abstract class Effect<TAction> : IEffect
{
    /// <summary>
    /// Handles the specified action and dispatches new actions.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    /// <param name="dispatcher">The dispatcher to use to dispatch new actions.</param>
    /// <param name="rootState">The current root state of the application.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task HandleAsync(TAction action, IDispatcher dispatcher, IRootState rootState);

    /// <inheritdoc />
    public Task HandleAsync(object action, IDispatcher dispatcher, IRootState rootState)
    {
        return HandleAsync((TAction)action, dispatcher, rootState);
    }

    /// <inheritdoc />
    public bool CanHandle(object action)
    {
        return action is TAction;
    }
}

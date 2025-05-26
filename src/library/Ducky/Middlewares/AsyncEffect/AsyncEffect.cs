// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Middlewares.AsyncEffect;

/// <inheritdoc />
public abstract class AsyncEffect<TAction> : IAsyncEffect
{
    /// <inheritdoc />
    public object? LastAction
        => Dispatcher?.LastAction;

    /// <summary>
    /// Gets the dispatcher.
    /// </summary>
    public IDispatcher? Dispatcher { get; private set; }

    /// <inheritdoc />
    public void SetDispatcher(IDispatcher dispatcher)
    {
        Dispatcher = dispatcher;
    }

    /// <inheritdoc />
    public bool CanHandle(object action)
    {
        return action is TAction;
    }

    /// <inheritdoc />
    public Task HandleAsync(object action, IRootState rootState)
    {
        return HandleAsync((TAction)action, rootState);
    }

    /// <summary>
    /// Handles the specified action and dispatches new actions.
    /// </summary>
    /// <param name="action">The action to handle.</param>
    /// <param name="rootState">The current root state of the application.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public abstract Task HandleAsync(TAction action, IRootState rootState);
}

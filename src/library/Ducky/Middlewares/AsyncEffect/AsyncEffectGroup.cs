// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Base class for grouping related async effects with shared dependencies and helper methods.
/// This allows injecting dependencies once and sharing logic across multiple effects.
/// </summary>
public abstract class AsyncEffectGroup : IAsyncEffect
{
    private readonly Dictionary<Type, IAsyncEffect> _effects = [];

    /// <inheritdoc />
    public object? LastAction
        => Dispatcher.LastAction;

    /// <summary>
    /// Gets the dispatcher.
    /// </summary>
    public IDispatcher Dispatcher { get; private set; } = null!;

    /// <inheritdoc />
    public void SetDispatcher(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        Dispatcher = dispatcher;

        // Set dispatcher on all registered effects
        foreach (IAsyncEffect effect in _effects.Values)
        {
            effect.SetDispatcher(dispatcher);
        }
    }

    /// <inheritdoc />
    public bool CanHandle(object action)
    {
        return _effects.ContainsKey(action.GetType());
    }

    /// <inheritdoc />
    public Task HandleAsync(object action, IStateProvider stateProvider)
    {
        if (_effects.TryGetValue(action.GetType(), out IAsyncEffect? effect))
        {
            return effect.HandleAsync(action, stateProvider);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registers an effect handler for a specific action type.
    /// </summary>
    /// <typeparam name="TAction">The type of action to handle.</typeparam>
    /// <param name="handler">The async handler function.</param>
    protected void On<TAction>(Func<TAction, IStateProvider, Task> handler)
    {
        DelegateAsyncEffect<TAction> effect = new(handler, this);
        _effects[typeof(TAction)] = effect;
    }

    /// <summary>
    /// Internal effect implementation that delegates to a handler function.
    /// </summary>
    private class DelegateAsyncEffect<TAction>(
        Func<TAction, IStateProvider, Task> handler,
        AsyncEffectGroup parent)
        : AsyncEffect<TAction>
    {
        public override Task HandleAsync(TAction action, IStateProvider stateProvider)
        {
            // Use parent's dispatcher so all effects in the group share the same dispatcher
            Dispatcher = parent.Dispatcher;
            return handler(action, stateProvider);
        }
    }
}

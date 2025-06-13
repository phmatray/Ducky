// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive;

/// <summary>
/// Base class for reactive effects that work with a specific state type.
/// </summary>
/// <typeparam name="TState">The type of state this effect works with.</typeparam>
public abstract class ReactiveEffect<TState> : ReactiveEffectBase
    where TState : class
{
    /// <summary>
    /// Handles the effect with typed state access.
    /// </summary>
    protected sealed override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        IObservable<TState> typedState = rootState
            .Select(state => state.GetSliceState<TState>())
            .DistinctUntilChanged();

        return HandleTyped(actions, typedState);
    }

    /// <summary>
    /// Handles the effect with typed state. Override this method to implement your effect logic.
    /// </summary>
    /// <param name="actions">The stream of actions.</param>
    /// <param name="state">The stream of typed state.</param>
    /// <returns>An observable of new actions to dispatch.</returns>
    protected abstract IObservable<object> HandleTyped(
        IObservable<object> actions,
        IObservable<TState> state);

    /// <summary>
    /// Selects state properties with automatic distinct until changed.
    /// </summary>
    /// <typeparam name="TProperty">The type of property to select.</typeparam>
    /// <param name="state">The state observable.</param>
    /// <param name="selector">The property selector.</param>
    /// <returns>An observable of the selected property.</returns>
    protected IObservable<TProperty> SelectProperty<TProperty>(
        IObservable<TState> state,
        Func<TState, TProperty> selector)
    {
        return state
            .Select(selector)
            .DistinctUntilChanged();
    }

    /// <summary>
    /// Creates an effect that only runs when a specific condition is met.
    /// </summary>
    /// <param name="state">The state observable.</param>
    /// <param name="condition">The condition to check.</param>
    /// <param name="effect">The effect to run when condition is true.</param>
    /// <returns>An observable of actions.</returns>
    protected IObservable<object> WhenCondition(
        IObservable<TState> state,
        Func<TState, bool> condition,
        Func<TState, IObservable<object>> effect)
    {
        return state
            .Where(condition)
            .SwitchSelect(effect);
    }

    /// <summary>
    /// Creates an effect that runs when a property changes to a specific value.
    /// </summary>
    /// <typeparam name="TProperty">The type of property.</typeparam>
    /// <param name="state">The state observable.</param>
    /// <param name="selector">The property selector.</param>
    /// <param name="value">The value to watch for.</param>
    /// <param name="effect">The effect to run.</param>
    /// <returns>An observable of actions.</returns>
    protected IObservable<object> WhenPropertyEquals<TProperty>(
        IObservable<TState> state,
        Func<TState, TProperty> selector,
        TProperty value,
        Func<IObservable<object>> effect)
    {
        return SelectProperty(state, selector)
            .Where(prop => EqualityComparer<TProperty>.Default.Equals(prop, value))
            .SwitchSelect(_ => effect());
    }
}

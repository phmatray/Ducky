// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Patterns;

/// <summary>
/// A reactive effect that debounces actions before processing them.
/// </summary>
/// <typeparam name="TAction">The type of action to debounce.</typeparam>
public abstract class DebouncedEffect<TAction> : ReactiveEffectBase
    where TAction : class
{
    private readonly TimeSpan _debounceTime;
    private readonly IScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedEffect{TAction}"/> class.
    /// </summary>
    /// <param name="debounceTime">The time to wait before processing an action.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    protected DebouncedEffect(TimeSpan debounceTime, IScheduler? scheduler = null)
    {
        _debounceTime = debounceTime;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    /// <summary>
    /// Handles the effect with debouncing logic.
    /// </summary>
    protected sealed override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IRootState> rootState)
    {
        return actions
            .OfActionType<TAction>()
            .Throttle(_debounceTime, _scheduler)
            .SwitchSelect(action => ProcessDebouncedAction(action, rootState));
    }

    /// <summary>
    /// Processes a debounced action.
    /// </summary>
    /// <param name="action">The debounced action to process.</param>
    /// <param name="rootState">The current root state observable.</param>
    /// <returns>An observable of new actions to dispatch.</returns>
    protected abstract IObservable<object> ProcessDebouncedAction(
        TAction action,
        IObservable<IRootState> rootState);
}

/// <summary>
/// A reactive effect that debounces actions and works with typed state.
/// </summary>
/// <typeparam name="TAction">The type of action to debounce.</typeparam>
/// <typeparam name="TState">The type of state to work with.</typeparam>
public abstract class DebouncedEffect<TAction, TState> : ReactiveEffect<TState>
    where TAction : class
    where TState : class
{
    private readonly TimeSpan _debounceTime;
    private readonly IScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DebouncedEffect{TAction,TState}"/> class.
    /// </summary>
    /// <param name="debounceTime">The time to wait before processing an action.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    protected DebouncedEffect(TimeSpan debounceTime, IScheduler? scheduler = null)
    {
        _debounceTime = debounceTime;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    /// <summary>
    /// Handles the typed effect with debouncing logic.
    /// </summary>
    protected sealed override IObservable<object> HandleTyped(
        IObservable<object> actions,
        IObservable<TState> state)
    {
        return actions
            .OfActionType<TAction>()
            .Throttle(_debounceTime, _scheduler)
            .WithLatestFrom(state, (action, currentState) => (action, currentState))
            .SwitchSelect(tuple => ProcessDebouncedAction(tuple.action, tuple.currentState));
    }

    /// <summary>
    /// Processes a debounced action with the current state.
    /// </summary>
    /// <param name="action">The debounced action to process.</param>
    /// <param name="state">The current state.</param>
    /// <returns>An observable of new actions to dispatch.</returns>
    protected abstract IObservable<object> ProcessDebouncedAction(TAction action, TState state);
}

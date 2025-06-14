// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Ducky.Reactive.Patterns;

/// <summary>
/// A reactive effect that polls at regular intervals.
/// </summary>
public abstract class PollingEffect : ReactiveEffectBase
{
    private readonly TimeSpan _pollingInterval;
    private readonly IScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingEffect"/> class.
    /// </summary>
    /// <param name="pollingInterval">The interval between polls.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    protected PollingEffect(TimeSpan pollingInterval, IScheduler? scheduler = null)
    {
        _pollingInterval = pollingInterval;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    /// <summary>
    /// Gets or sets a value indicating whether polling should start immediately on initialization.
    /// </summary>
    public bool StartImmediately { get; set; } = true;

    /// <summary>
    /// Handles the effect with polling logic.
    /// </summary>
    protected sealed override IObservable<object> HandleCore(
        IObservable<object> actions,
        IObservable<IStateProvider> stateProvider)
    {
        IObservable<object> startSignal = GetStartSignal(actions);
        IObservable<object> stopSignal = GetStopSignal(actions);

        return startSignal
            .SwitchSelect(_ =>
            {
                IObservable<object> polling = Observable
                    .Interval(_pollingInterval, _scheduler)
                    .StartWith(StartImmediately ? 0 : -1)
                    .Where(i => i >= 0)
                    .WithLatestFrom(stateProvider, (_, state) => state)
                    .SwitchSelect(Poll)
                    .TakeUntil(stopSignal);

                return polling;
            });
    }

    /// <summary>
    /// Gets the observable that signals when to start polling.
    /// Override to customize start behavior.
    /// </summary>
    /// <param name="actions">The action stream.</param>
    /// <returns>An observable that emits when polling should start.</returns>
    protected virtual IObservable<object> GetStartSignal(IObservable<object> actions)
    {
        // By default, start immediately
        return Observable.Return(new object());
    }

    /// <summary>
    /// Gets the observable that signals when to stop polling.
    /// Override to customize stop behavior.
    /// </summary>
    /// <param name="actions">The action stream.</param>
    /// <returns>An observable that emits when polling should stop.</returns>
    protected abstract IObservable<object> GetStopSignal(IObservable<object> actions);

    /// <summary>
    /// Performs the polling operation.
    /// </summary>
    /// <param name="stateProvider">The current state provider.</param>
    /// <returns>An observable of actions to dispatch.</returns>
    protected abstract IObservable<object> Poll(IStateProvider stateProvider);
}

/// <summary>
/// A reactive effect that polls at regular intervals with typed state.
/// </summary>
/// <typeparam name="TState">The type of state to work with.</typeparam>
public abstract class PollingEffect<TState> : ReactiveEffect<TState>
    where TState : class
{
    private readonly TimeSpan _pollingInterval;
    private readonly IScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PollingEffect{TState}"/> class.
    /// </summary>
    /// <param name="pollingInterval">The interval between polls.</param>
    /// <param name="scheduler">The scheduler to use for timing operations.</param>
    protected PollingEffect(TimeSpan pollingInterval, IScheduler? scheduler = null)
    {
        _pollingInterval = pollingInterval;
        _scheduler = scheduler ?? Scheduler.Default;
    }

    /// <summary>
    /// Gets or sets a value indicating whether polling should start immediately on initialization.
    /// </summary>
    public bool StartImmediately { get; set; } = true;

    /// <summary>
    /// Handles the typed effect with polling logic.
    /// </summary>
    protected sealed override IObservable<object> HandleTyped(
        IObservable<object> actions,
        IObservable<TState> state)
    {
        IObservable<object> startSignal = GetStartSignal(actions);
        IObservable<object> stopSignal = GetStopSignal(actions);

        return startSignal
            .SwitchSelect(_ =>
            {
                IObservable<object> polling = Observable
                    .Interval(_pollingInterval, _scheduler)
                    .StartWith(StartImmediately ? 0 : -1)
                    .Where(i => i >= 0)
                    .WithLatestFrom(state, (_, currentState) => currentState)
                    .SwitchSelect(Poll)
                    .TakeUntil(stopSignal);

                return polling;
            });
    }

    /// <summary>
    /// Gets the observable that signals when to start polling.
    /// Override to customize start behavior.
    /// </summary>
    /// <param name="actions">The action stream.</param>
    /// <returns>An observable that emits when polling should start.</returns>
    protected virtual IObservable<object> GetStartSignal(IObservable<object> actions)
    {
        // By default, start immediately
        return Observable.Return(new object());
    }

    /// <summary>
    /// Gets the observable that signals when to stop polling.
    /// Override to customize stop behavior.
    /// </summary>
    /// <param name="actions">The action stream.</param>
    /// <returns>An observable that emits when polling should stop.</returns>
    protected abstract IObservable<object> GetStopSignal(IObservable<object> actions);

    /// <summary>
    /// Performs the polling operation with typed state.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <returns>An observable of actions to dispatch.</returns>
    protected abstract IObservable<object> Poll(TState state);
}

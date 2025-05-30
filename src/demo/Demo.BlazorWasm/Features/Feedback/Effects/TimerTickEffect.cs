// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.ReactiveEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that manages timer ticks based on timer state.
/// </summary>
public class TimerTickEffect : ReactiveEffect
{
    /// <inheritdoc />
    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        // Watch for timer start/stop actions and manage timer accordingly
        Observable<object> startTimer = actions
            .OfActionType<StartTimer>()
            .Select(_ => rootState
                .Select(state => state.GetSliceState<TimerState>())
                .Where(timer => timer is { IsRunning: true })
                .SelectMany(_ => Observable.Interval(TimeSpan.FromSeconds(1), TimeProvider))
                .TakeUntil(actions.OfActionType<StopTimer>())
                .Select(_ => (object)new Tick()))
            .Switch();

        // When timer reaches zero, automatically stop it
        Observable<object> autoStop = rootState
            .Select(state => state.GetSliceState<TimerState>())
            .Where(timer => timer is { IsRunning: true, Time: <= 0 })
            .Select(_ => (object)new StopTimer());

        return Observable.Merge(startTimer, autoStop);
    }
}

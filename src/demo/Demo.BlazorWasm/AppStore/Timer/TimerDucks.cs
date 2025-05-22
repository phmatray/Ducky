// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Middlewares.ReactiveEffect;

namespace Demo.BlazorWasm.AppStore;

#region State

public record TimerState
{
    public int Time { get; init; }

    public bool IsRunning { get; init; }

    // Selectors
    public string SelectAngle()
        => $"{Time % 60 * 6}deg";
}

#endregion

#region Actions

[DuckyAction]
public record StartTimer;

[DuckyAction]
public record StopTimer;

[DuckyAction]
public record ResetTimer;

[DuckyAction]
public record Tick;

#endregion

#region Reducers

public record TimerReducers : SliceReducers<TimerState>
{
    public TimerReducers()
    {
        On<StartTimer>(Reduce);
        On<StopTimer>(Reduce);
        On<ResetTimer>(Reduce);
        On<Tick>(Reduce);
    }

    public override TimerState GetInitialState()
        => new()
        {
            Time = 0,
            IsRunning = false
        };

    private static TimerState Reduce(TimerState state, StartTimer _)
        => state with { IsRunning = true };

    private static TimerState Reduce(TimerState state, StopTimer _)
        => state with { IsRunning = false };

    private static TimerState Reduce(TimerState timerState, ResetTimer resetTimer)
        => new();

    private static TimerState Reduce(TimerState state, Tick _)
        => state with { Time = state.Time + 1 };
}

#endregion

#region Effects

// ReSharper disable once UnusedType.Global
public class StartTimerEffect : ReactiveEffect
{
    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<StartTimer>()
            .SwitchSelect(_ => Observable
                .Interval(TimeSpan.FromSeconds(1), TimeProvider)
                .Select(_ => new Tick())
                .TakeUntil(actions.OfActionType<StopTimer>())
                .Cast<Tick, object>());
    }
}

#endregion

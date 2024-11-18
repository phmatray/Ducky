// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Timer;

#region State

public record TimerState
{
    public int Time { get; init; }

    public bool IsRunning { get; init; }

    // Selectors
    public string SelectAngle()
    {
        var time = Time % 60;
        return $"{time * 6}deg";
    }
}

#endregion

#region Actions

public record StartTimer : IAction;

public record StopTimer : IAction;

public record ResetTimer : IAction;

public record Tick : IAction;

#endregion

#region Reducers

public record TimerReducers : SliceReducers<TimerState>
{
    public TimerReducers()
    {
        On<StartTimer>((state, _)
            => state with { IsRunning = true });

        On<StopTimer>((state, _)
            => state with { IsRunning = false });

        On<ResetTimer>((_, _)
            => new TimerState());

        On<Tick>((state, _)
            => state with { Time = state.Time + 1 });
    }

    public override TimerState GetInitialState()
    {
        return new TimerState
        {
            Time = 0,
            IsRunning = false
        };
    }
}

#endregion

#region Effects

// ReSharper disable once UnusedType.Global
public class StartTimerEffect : Effect
{
    public override Observable<IAction> Handle(
        Observable<IAction> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<StartTimer>()
            .SwitchSelect(_ => Observable
                .Interval(TimeSpan.FromSeconds(1), TimeProvider)
                .Select(_ => new Tick())
                .TakeUntil(actions.OfActionType<StopTimer>())
                .Cast<Tick, IAction>());
    }
}

#endregion

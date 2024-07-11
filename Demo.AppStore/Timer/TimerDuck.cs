namespace Demo.AppStore;

#region State

public record TimerState
{
    public int Time { get; init; }
    public bool IsRunning { get; init; }
}

#endregion

#region Actions

public record StartTimer : IAction;
public record StopTimer : IAction;
public record ResetTimer : IAction;
public record Tick : IAction;

#endregion

#region Reducers

public class TimerReducers : ReducerCollection<TimerState>
{
    public TimerReducers()
    {
        Map<StartTimer>((state, _)
            => state with { IsRunning = true });
        
        Map<StopTimer>((state, _)
            => state with { IsRunning = false });
        
        Map<ResetTimer>((_, _)
            => new TimerState());
        
        Map<Tick>((state, _)
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
        Observable<RootState> rootState)
    {
        return actions
            .FilterActions<StartTimer>()
            .SelectMany(_ => Observable.Interval(TimeSpan.FromSeconds(1), TimeProvider))
            .Select(_ => new Tick())
            .TakeUntil(actions.FilterActions<StopTimer>())
            .Cast<Tick, IAction>();
    }
}

#endregion

#region Slice

// ReSharper disable once UnusedType.Global
public record TimerSlice : Slice<TimerState>
{
    public override ReducerCollection<TimerState> Reducers { get; } = new TimerReducers();
}

#endregion
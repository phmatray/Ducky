namespace Demo.AppStore;

// State
public record TimerState
{
    public int Time { get; init; }
    public bool IsRunning { get; init; }
}

// Actions
public record StartTimer : IAction;
public record StopTimer : IAction;
public record ResetTimer : IAction;
public record Tick : IAction;

// Reducers
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
}

// Effects
// ReSharper disable once UnusedType.Global
public class StartTimerEffect : Effect
{
    public override Observable<object> Handle(
        Observable<object> actions, Store store)
    {
        return actions
            .FilterActions<StartTimer>()
            .SelectMany(_ => Observable.Interval(TimeSpan.FromSeconds(1)))
            .Select(_ => new Tick())
            .TakeUntil(actions.FilterActions<StopTimer>())
            .Cast<Tick, object>();
    }
}

// Slice
// ReSharper disable once UnusedType.Global
public record TimerSlice : Slice<TimerState>
{
    public override string Key => "timer";
    public override TimerState InitialState { get; } = new();
    public override IReducer<TimerState> Reducers { get; } = new TimerReducers();
}
namespace Demo.AppStore;

// State
public record TimerState
{
    public int Time { get; init; }
    public bool IsRunning { get; init; }
}

// Actions
public record StartTimer;
public record StopTimer;
public record ResetTimer;
public record Tick;

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

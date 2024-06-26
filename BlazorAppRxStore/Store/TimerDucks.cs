namespace BlazorAppRxStore.Store;

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

// Reducer
public class TimerReducer : ActionReducer<TimerState>
{
    public TimerReducer()
    {
        Register<StartTimer>(state => state with { IsRunning = true });
        Register<StopTimer>(state => state with { IsRunning = false });
        Register<ResetTimer>(_ => new TimerState());
        Register<Tick>(state => state with { Time = state.Time + 1 });
    }
}

public class TimerReducerFactory : IActionReducerFactory<TimerState>
{
    public IActionReducer<TimerState> CreateReducer(
        IDictionary<string, IActionReducer<TimerState>> reducers,
        TimerState initialState)
    {
        return new TimerReducer();
    }
}
namespace BlazorAppRxStore.Store;

// State
public record CounterState
{
    public int Count { get; init; } = 10;
}

// Actions
public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;

// Reducer
public class CounterReducer : ReducerBase<CounterState>
{
    public CounterReducer()
    {
        Register<Reset>(() => new CounterState());
        Register<Increment>(state => new CounterState { Count = state.Count + 1 });
        Register<Decrement>(state => new CounterState { Count = state.Count - 1 });
    }
}


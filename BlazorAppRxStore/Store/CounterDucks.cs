using BlazorAppRxStore.SeedWork;

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
        Register<Reset>((state, action)
            => new CounterState { Count = 0 });
        
        Register<Increment>((state, action)
            => new CounterState { Count = state.Count + 1 });
        
        Register<Decrement>((state, action)
            => new CounterState { Count = state.Count - 1 });
    }
}


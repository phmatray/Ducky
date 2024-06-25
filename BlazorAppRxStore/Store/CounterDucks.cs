using BlazorAppRxStore.SeedWork;

namespace BlazorAppRxStore.Store;

// State
public record CounterState
{
    public int Count { get; init; }
}

// Actions
public record Increment : IAction;
public record Decrement : IAction;
public record Reset : IAction;

// Reducer


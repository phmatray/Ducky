# Ducky: A Predictable State Management Library for Blazor

Ducky is a state management library designed for Blazor applications, inspired by the Redux pattern commonly used in JavaScript applications. It provides a predictable state container for .NET, ensuring that the application state is managed in a clear, consistent, and centralized manner.

### Key Features of Ducky:

1. **Predictable State Management**: By following the principles of Redux, Ducky ensures that the application state is predictable. Every state change is described by an action and handled by a reducer, which returns a new state.
2. **Single Source of Truth**: The entire state of the application is stored in a single state tree, which makes debugging and state inspection easier.
3. **Immutability**: Ducky enforces immutability in state changes. Instead of mutating the existing state, reducers return a new state object, ensuring the integrity of the state over time.
4. **Actions and Reducers**: Actions describe the changes in the application, and reducers specify how the application's state changes in response to actions.
5. **Middleware and Effects**: Middleware allows for intercepting actions before they reach the reducer, enabling tasks such as logging, analytics, and asynchronous operations. Effects handle side effects like data fetching and other asynchronous tasks.
6. **Selectors**: Selectors are used to query the state in a performant manner. Memoized selectors help in reducing unnecessary recomputations, thus optimizing performance.
7. **Integration with Blazor**: Ducky is tailored for Blazor applications, integrating seamlessly with Blazor's component-based architecture.

### How Ducky Works:

1. **State**: The application's state is represented by a single immutable object.
2. **Actions**: Actions are plain objects that describe what happened in the application.
3. **Reducers**: Reducers are pure functions that take the current state and an action, and return a new state.
4. **Dispatch**: The `dispatch` function sends an action to the store, which then forwards it to the reducer to compute the new state.
5. **Selectors**: Selectors are functions that select a piece of the state. Memoized selectors cache the results of state queries to improve performance.

### Example:

Here is a simple example of how Ducky might be used in a Blazor application to manage a counter's state:

```csharp
namespace Demo.AppStore
{
    // State
    public record CounterState
    {
        public int Count { get; init; }
    }

    // Actions
    public record Increment(int Amount = 1) : IAction;
    public record Decrement(int Amount = 1) : IAction;
    public record Reset : IAction;

    // Reducers
    public record CounterReducers : SliceReducers<CounterState>
    {
        public CounterReducers()
        {
            Map<Increment>((state, action) => state with { Count = state.Count + action.Amount });
            Map<Decrement>((state, action) => state with { Count = state.Count - action.Amount });
            Map<Reset>(_ => GetInitialState());
        }

        public override CounterState GetInitialState()
        {
            return new CounterState { Count = 0 };
        }
    }

    // Component
    @page "/counter"
    @inherits DuckyComponent<CounterState>

    <div>
        <p>Current count: @State.Count</p>
        <button @onclick="IncrementCount">Increment</button>
        <button @onclick="DecrementCount">Decrement</button>
        <button @onclick="ResetCount">Reset</button>
    </div>

    @code {
        private void IncrementCount() => Dispatch(new Increment());
        private void DecrementCount() => Dispatch(new Decrement());
        private void ResetCount() => Dispatch(new Reset());
    }
}
```

### Summary

Ducky simplifies state management in Blazor applications by providing a structured and predictable way to handle state changes, inspired by the Redux pattern. It promotes best practices such as immutability, single source of truth, and clear separation of concerns, making it easier to manage complex application states.

# State Management

Global state in Ducky provides a powerful mechanism for managing application-wide data in a predictable and maintainable way. By centralizing your state and handling all changes through actions and reducers, you can build applications that are easier to understand, debug, and scale.

State management is a crucial aspect of modern application development, especially as applications grow in complexity. Ducky makes it simple to implement global state management in your .NET applications, ensuring that your application state is consistent, predictable, and easily accessible from any part of your application.

## What is Global State?

Global state refers to a single, centralized state that holds the entire application's data. Unlike local state, which is confined to individual components or modules, global state is accessible throughout the application. This allows different parts of your application to share data and stay synchronized.

### Key Characteristics of Global State

1. **Single Source of Truth**: The global state serves as the single source of truth for your application. All components and services that need to read or modify state must interact with this centralized store.
2. **Accessibility**: Global state is accessible from any part of your application. This means that data can be easily shared between different components, pages, or services, ensuring consistency across your application.
3. **Predictability**: By managing all state changes through a centralized mechanism (such as actions and reducers), global state makes it easier to track, debug, and predict how state evolves over time.

## How Global State Works in Ducky

In Ducky, global state is managed using a centralized store. This store holds the entire application state and is responsible for updating the state in response to actions.

### Components of State Management in Ducky

1. **Store**: The store is the core of the Ducky state management system. It holds the global state and provides methods to access the state, dispatch actions, and subscribe to state changes.
2. **Actions**: Actions are simple objects that describe the type of change that should occur in the state. They are dispatched to the store to trigger state updates.
3. **Reducers**: Reducers are pure functions that take the current state and an action, and return a new state. They define how the state should change in response to each action.
4. **Selectors**: Selectors are functions that allow you to retrieve specific pieces of the global state. They are used to encapsulate and optimize access to the state.

### Example of Global State in Action

Let's consider a simple example of global state management using Ducky in a Blazor application.

#### Defining the Global State

Suppose we have a counter application. The global state could be as simple as an integer representing the current count:

```C#
public class CounterReducers : SliceReducers<int>
{
    public CounterReducers()
    {
        On<Increment>((state, _) => state + 1);
        On<Decrement>((state, _) => state - 1);
    }

    public override int GetInitialState()
    {
        return 0;
    }
}
```

In this example:
- The global state is represented by an integer.
- The `Increment` and `Decrement` actions are used to modify the state.

#### Accessing and Modifying Global State

In a Blazor component, you can access and modify the global state through Ducky as follows:

```html
@page "/counter"
@inherits DuckyComponent<int>

<h3>Counter</h3>

<p>Current count: @State</p>

<button class="btn btn-primary" @onclick="Increment">Increment</button>
<button class="btn btn-secondary" @onclick="Decrement">Decrement</button>

@code {
    private void Increment()
    {
        Dispatch(new Increment());
    }

    private void Decrement()
    {
        Dispatch(new Decrement());
    }
}
```

In this Blazor component:
- The global state is automatically injected into the component via `DuckyComponent<int>`.
- The `State` property reflects the current global state (i.e., the counter value).
- The `Dispatch` method is used to modify the state by dispatching actions.

### Benefits of Using Global State

- **Consistency**: Since all components read from and write to the same state, it's easy to maintain consistency across your application.
- **Debugging**: Centralized state management simplifies the process of tracking down bugs and understanding how state changes over time.
- **Scalability**: As your application grows, managing state through a global store can help you avoid the pitfalls of prop drilling and other common state management issues.

### When to Use Global State

Global state is particularly useful in applications where multiple components need to share and synchronize data. However, it's essential to balance the use of global state with local state, which is more appropriate for managing component-specific data.

# Reducers

Reducers are a core concept in R3dux's state management system. They are pure functions that take the current state and an action, and return a new state. Reducers are responsible for determining how the global state should change in response to a given action.

## What are Reducers?

Reducers in R3dux are pure functions that dictate how the state of your application should change in response to actions. A reducer takes the current state and an action as inputs and returns a new state as output. This approach ensures that state transitions are predictable and traceable.

### Key Characteristics of Reducers

1. **Pure Functions**: Reducers are pure functions, meaning they always return the same output for the same input and have no side effects. They do not modify the existing state but instead return a new state object.
2. **Immutability**: Reducers enforce immutability by returning a new state object rather than mutating the existing state. This immutability is crucial for ensuring predictable state changes and making state management easier to debug.
3. **Centralized State Management**: By centralizing state changes in reducers, you can ensure that all state transformations are handled in a controlled and predictable manner.

## Defining Reducers

In R3dux, reducers are defined by extending the `SliceReducers<TState>` class, where `TState` represents the type of the state slice that the reducer manages. Within a reducer, you map actions to the state changes they should produce.

### Example of a Simple Reducer

```csharp
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
- The `CounterReducers` class manages a piece of state representing a simple integer counter.
- The `On<Increment>` method maps the `Increment` action to a state change that increases the counter by one.
- The `On<Decrement>` method decreases the counter by one when the `Decrement` action is dispatched.
- The `GetInitialState` method defines the initial state of the counter as `0`.

## Handling Actions with Reducers

Reducers use the `On<TAction>` method to specify how the state should change in response to a specific action. The `On` method takes two parameters:
1. The type of action that the reducer handles.
2. A function that defines how the state should change in response to the action.

### Example of Handling Actions

```csharp
public class UserReducers : SliceReducers<UserState>
{
    public UserReducers()
    {
        On<SetUserName>((state, action) => state with { UserName = action.UserName });
        On<SetUserAge>((state, action) => state with { Age = action.Age });
    }

    public override UserState GetInitialState()
    {
        return new UserState { UserName = "Anonymous", Age = 0 };
    }
}
```

In this example:
- The `SetUserName` action updates the `UserName` property of the state.
- The `SetUserAge` action updates the `Age` property of the state.
- The `state with { ... }` syntax ensures that the state is updated immutably.

## State Composition in R3dux

In R3dux, the global state is automatically composed from the individual slice states managed by each reducer. This means that you don't need to manually combine reducers; instead, R3dux handles this composition behind the scenes.

### Example: Root State Composition

Each slice reducer manages its own piece of the global state. For example, a `UserReducers` might manage the `UserState`, while a `CounterReducers` manages the `CounterState`. R3dux automatically combines these slices into the root state.

```csharp
public class RootState : IRootState
{
    // RootState is automatically composed by R3dux from the individual slice states.
}
```

When you register reducers in R3dux, they automatically contribute to the overall application state, which R3dux manages as a `RootState` object.

## Refactoring Reducers with Extracted Methods

As your application grows, your reducers may become more complex. To improve maintainability, you can refactor reducers by extracting logic into separate methods.

### Refactored Reducer Example

```csharp
public record TodoReducers : SliceReducers<TodoState>
{
    public TodoReducers()
    {
        On<CreateTodo>(ReduceCreateTodo);
        On<ToggleTodo>(ReduceToggleTodo);
        On<DeleteTodo>(ReduceDeleteTodo);
    }

    public override TodoState GetInitialState()
    {
        return TodoState.Create(new[]
        {
            new TodoItem(SampleIds.Id1, "Learn Blazor", true),
            new TodoItem(SampleIds.Id2, "Learn Redux"),
            new TodoItem(SampleIds.Id3, "Learn Reactive Programming"),
            new TodoItem(SampleIds.Id4, "Create a Todo App", true),
            new TodoItem(SampleIds.Id5, "Publish a NuGet package")
        });
    }

    private static TodoState ReduceCreateTodo(TodoState state, CreateTodo action)
    {
        return state.SetOne(new TodoItem(action.Payload.Title));
    }

    private static TodoState ReduceToggleTodo(TodoState state, ToggleTodo action)
    {
        return state.UpdateOne(action.Payload.Id, todo => todo.IsCompleted = !todo.IsCompleted);
    }

    private static TodoState ReduceDeleteTodo(TodoState state, DeleteTodo action)
    {
        return state.RemoveOne(action.Payload.Id);
    }
}
```

In this example:
- The reducer methods (`ReduceCreateTodo`, `ReduceToggleTodo`, `ReduceDeleteTodo`) are extracted from the main reducer class, making the class more readable and easier to maintain.
- Each method is focused on a specific action, further enhancing the clarity and testability of the code.

## Initializing State

Each reducer defines an initial state using the `GetInitialState` method. This method is called when the store is first created and sets up the default state for the application or the slice managed by the reducer.

### Example of Initializing State

```csharp
public override int GetInitialState()
{
    return 10; // Sets the initial counter value to 10
}
```

The `GetInitialState` method provides a default value for the state, ensuring that the application has a well-defined starting point.

## Best Practices for Writing Reducers

- **Keep Reducers Pure**: Reducers should not have side effects. Avoid making API calls or dispatching other actions from within a reducer.
- **Use Immutability**: Always return a new state object rather than mutating the existing state.
- **Decompose State**: Break down the state into smaller, manageable slices, each handled by its own reducer.
- **Handle Actions Explicitly**: Each reducer should explicitly handle specific actions. This makes the state transitions clear and predictable.
- **Use Default States**: Ensure that `GetInitialState` provides a meaningful default state for each slice of your application.

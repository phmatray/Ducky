# Actions

Actions in Ducky are a fundamental part of the state management process. They serve as the only means by which the global state can be modified, ensuring that all state changes are predictable, traceable, and consistent across the application.

## What are Actions?

Actions are simple objects that describe a change that should occur in the state. An action typically contains a `type` that indicates the nature of the action, and optionally, some data (payload) that provides additional information needed to perform the state update.

### Key Characteristics of Actions

1. **Intent Declaration**: Actions declare the intent to change the state. They do not execute the change themselves but serve as a signal to the store that a change should be made.
2. **Plain Objects**: Actions in Ducky are typically simple objects or records that implement the `IAction` interface. This simplicity ensures that actions are easy to create, dispatch, and test.
3. **Immutable**: Actions should be immutable, meaning once they are created, their properties should not be modified. This immutability aligns with the overall philosophy of predictable and traceable state management.

## Defining Actions

Defining actions in Ducky is straightforward. You simply create a class or record that implements the `IAction` interface. Here’s an example:

```csharp
public record Increment(int Amount = 1);
public record Decrement(int Amount = 1);
```

### Action Types

Each action should have a distinct type that identifies what kind of state change it represents. In C#, when using records or classes for actions, the class or record name typically serves as the action type implicitly.

#### Example:

```csharp
public record SetUserName(string UserName);
```

In this example:
- The `SetUserName` action contains a single property, `UserName`, which is the payload needed to update the state.
- The action implicitly has a type, which is the name of the record (`SetUserName`).

## Dispatching Actions

Once actions are defined, they are dispatched to the store to initiate a state change. Dispatching an action involves passing it to the store, which then uses reducers to update the state accordingly.

### Example of Dispatching an Action

Consider a Blazor component that needs to update the user's name in the global state:

```razor
@code {
    private void UpdateUserName()
    {
        Dispatch(new SetUserName("John Doe"));
    }
}
```

In this example:
- The `SetUserName` action is dispatched with the payload `"John Doe"`.
- The store processes this action, and the state is updated with the new user name.

## Handling Actions in Reducers

Actions by themselves do not modify the state. Instead, they are handled by reducers, which are responsible for interpreting the action and determining how the state should change in response.

### Example of Handling Actions in Reducers

```csharp
public class UserReducers : SliceReducers<UserState>
{
    public UserReducers()
    {
        On<SetUserName>((state, action) => state with { UserName = action.UserName });
    }

    public override UserState GetInitialState()
    {
        return new UserState { UserName = "Anonymous" };
    }
}
```

In this reducer:
- The `On<SetUserName>` method maps the `SetUserName` action to a state change that updates the `UserName` property.
- The new state is created immutably using the `with` keyword.

## Action Payloads

Actions often carry additional data necessary to perform a state update. This data is known as the action's payload. The payload is passed to reducers along with the current state, allowing the reducer to create a new state based on the information provided.

### Example of an Action with Payload

```csharp
public record AddProduct(string ProductName, decimal Price);
```

In this example:
- The `AddProduct` action contains two pieces of information: `ProductName` and `Price`.
- When this action is dispatched, the reducer will use this data to update the state, typically by adding a new product to a list in the state.

## Actions and Asynchronous Operations

While actions themselves are typically synchronous, they can be dispatched from asynchronous operations. This is often handled through **Effects** in Ducky, where actions are dispatched based on the result of an asynchronous operation such as an API call.

### Example:

```csharp
public class LoadProductsEffect : ReactiveEffect
{
    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfType<LoadProducts>()
            .InvokeService(
                async () => await productService.GetProductsAsync(),
                products => new LoadProductsSuccess(products),
                ex => new LoadProductsFailure(ex));
    }
}
```

In this example:
- The `LoadProducts` action is dispatched to trigger the loading of products.
- Depending on the outcome of the service call, either `LoadProductsSuccess` or `LoadProductsFailure` is dispatched.

## Best Practices for Actions

- **Keep Actions Simple**: Actions should be simple and focused. They should contain only the information necessary to describe the state change.
- **Use Meaningful Names**: Name your actions clearly to reflect their purpose. This makes your code more readable and easier to maintain.
- **Leverage Immutability**: Ensure that actions are immutable to maintain consistency and predictability in state management.
- **Document Your Actions**: Clearly document what each action does, especially in large applications where many actions may exist.

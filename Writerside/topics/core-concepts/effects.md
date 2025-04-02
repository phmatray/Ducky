# Effects

In Ducky, effects are a powerful tool for handling side effects—operations that interact with the outside world or involve asynchronous logic. Effects listen to the stream of actions dispatched to the store and can dispatch new actions based on the outcome of those operations, such as API calls, logging, or timers.

## What are Effects?

Effects are responsible for managing side effects in your application. Unlike reducers, which handle pure state transformations, effects are used to perform operations that have side effects, such as:
- Making asynchronous API calls.
- Dispatching notifications.
- Handling time-based events (like timers).
- Interacting with services outside of your state management logic.

### Key Characteristics of Effects

1. **Asynchronous Operations**: Effects can handle asynchronous operations such as API calls, allowing your application to interact with external services or data sources.
2. **Dispatching New Actions**: Based on the outcome of the side effect, effects can dispatch new actions to update the state.
3. **Separation of Concerns**: Effects keep side effects separate from reducers, ensuring that state transformations remain pure and predictable.

## Defining Effects

In Ducky, you define an effect by extending the `ReactiveEffect` class and overriding the `Handle` method. The `Handle` method takes two parameters:
- `Observable<object> actions`: The stream of actions dispatched to the store.
- `Observable<IRootState> rootState`: The observable stream of the current state.

### Example: Loading Movies with an API Call

Here’s an example of an effect that handles loading movies from an external API:

```C#
public class LoadMoviesEffect : Effect
{
    private readonly IMoviesService _moviesService;

    public LoadMoviesEffect(IMoviesService moviesService)
    {
        _moviesService = moviesService;
    }

    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfType<LoadMovies>()
            .LogMessage("Loading movies...")
            .WithSliceState<MoviesState, LoadMovies>(rootState)
            .InvokeService(
                pair => _moviesService.GetMoviesAsync(pair.State.Pagination.CurrentPage, 5),
                response => new LoadMoviesSuccess(response.Movies, response.TotalItems),
                ex => new LoadMoviesFailure(ex))
            .LogMessage("Movies loaded.");
    }
}
```

In this example:
- The `LoadMoviesEffect` listens for the `LoadMovies` action.
- It logs the action, retrieves the current state slice, and invokes the `GetMoviesAsync` method from `IMoviesService`.
- Based on the API response, it dispatches either `LoadMoviesSuccess` or `LoadMoviesFailure` actions.

### Example: Displaying Notifications After a Successful Load

This example demonstrates how to use an effect to display a success notification after movies are successfully loaded:

```C#
public class LoadMoviesSuccessEffect : ReactiveEffect
{
    private readonly ISnackbar _snackbar;

    public LoadMoviesSuccessEffect(ISnackbar snackbar)
    {
        _snackbar = snackbar;
    }

    public override Observable<object> Handle(
        Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfType<object, LoadMoviesSuccess>()
            .Select(GetSnackBarMessage)
            .Do(message => _snackbar.Add(message, Severity.Success))
            .Select(message =>
            {
                var notification = new SuccessNotification(message);
                return new AddNotification(notification);
            });
    }

    private static string GetSnackBarMessage(LoadMoviesSuccess action)
    {
        return $"Loaded {action.Movies.Length} movies from the server.";
    }
}
```

In this example:
- The `LoadMoviesSuccessEffect` listens for the `LoadMoviesSuccess` action.
- It generates a success message and displays it using the `ISnackbar` service.
- After displaying the notification, it dispatches a new `AddNotification` action.

### Example: Handling Timers

Effects are also useful for handling time-based operations, such as starting and stopping timers. Here's an example of an effect that handles a timer:

```C#
public class StartTimerEffect : ReactiveEffect
{
    public override Observable<(IAction)> Handle(
        Observable<(IAction)> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfType<StartTimer>()
            .SwitchSelect(_ => Observable
                .Interval(TimeSpan.FromSeconds(1), TimeProvider)
                .Select(_ => new Tick())
                .TakeUntil(actions.OfType<StopTimer>())
                .Cast<Tick, object>());
    }
}
```

In this example:
- The `StartTimerEffect` listens for the `StartTimer` action.
- It starts a timer that dispatches a `Tick` action every second.
- The timer stops when a `StopTimer` action is dispatched.

## Custom Operators in Effects

Ducky provides several custom operators to simplify common patterns in effects, such as filtering actions, invoking services, and handling errors.

### Key Operators

- **OfType**: Filters actions of a specific type.
- **WithSliceState**: Combines actions with the corresponding slice of the state.
- **InvokeService**: Handles calling external services and dispatching success or error actions based on the outcome.

### Example: Using Custom Operators

```C#
public override Observable<object> Handle(
    Observable<object> actions,
    Observable<IRootState> rootState)
{
    return actions
        .OfType<LoadMovies>()
        .WithSliceState<MoviesState, LoadMovies>(rootState)
        .InvokeService(
            pair => _moviesService.GetMoviesAsync(pair.State.Pagination.CurrentPage, 5),
            response => new LoadMoviesSuccess(response.Movies, response.TotalItems),
            ex => new LoadMoviesFailure(ex));
}
```

This code demonstrates how custom operators streamline the process of handling actions, accessing state, and performing service calls.

## Best Practices for Writing Effects

- **Keep Effects Focused**: Each effect should handle a specific side effect, such as an API call or a timer. This makes effects easier to manage and test.
- **Use Custom Operators**: Leverage Ducky’s custom operators to simplify your effect logic and make it more readable.
- **Handle Errors Gracefully**: Always handle potential errors in your effects by dispatching appropriate failure actions.
- **Avoid Side Effects in Reducers**: Use effects, not reducers, for handling side effects. Reducers should remain pure and only manage state transformations.

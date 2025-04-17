# Effects

In Ducky, **effects** manage side effects—operations interacting with external systems or involving asynchronous logic. They listen to actions dispatched to the store and dispatch new actions based on their outcomes, including API calls, notifications, logging, or timers.

## What are Effects?

Effects handle asynchronous tasks and external interactions. Unlike reducers, which manage pure state transformations, effects:
- Execute asynchronous API calls
- Dispatch notifications and logs
- Handle timers and delayed operations
- Interact with external services or APIs

## Two Types of Effects

Ducky provides two main approaches for defining effects:

### 1. Reactive Effects (`ReactiveEffect`)

Reactive Effects are declarative and leverage observables to manage streams of actions:

```C#
public class IncrementEffect : ReactiveEffect
{
    public override Observable<object> Handle(
        Observable<object> actions,
        Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<Increment>()
            .WithSliceState<CounterState, Increment>(rootState)
            .Where(pair => pair.State.Value > 15)
            .Delay(TimeSpan.FromSeconds(3), TimeProvider)
            .SelectAction(_ => new Reset());
    }
}
```

This example:
- Listens for `Increment` actions.
- Checks the current state; if the counter exceeds 15, waits 3 seconds.
- Dispatches a `Reset` action afterward.

### 2. Async Effects (`AsyncEffect<T>`)

Async Effects allow asynchronous handling with `async/await`:

```C#
public class ResetCounterAfter3Sec : AsyncEffect<Increment>
{
    public override async Task HandleAsync(Increment action, IRootState rootState)
    {
        var counterState = rootState.GetSliceState<CounterState>();

        if (counterState.Value > 15)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), ObservableSystem.DefaultTimeProvider);
            Dispatcher.Reset();
        }
    }
}
```

This example:
- Handles an `Increment` action asynchronously.
- Checks state condition and delays execution accordingly.
- Dispatches actions directly using the dispatcher.

### Examples for Async Effects

**Example: Asynchronous API Call**

```C#
public class LoadMoviesAsyncEffect : AsyncEffect<LoadMovies>
{
    private readonly IMoviesService _moviesService;

    public LoadMoviesAsyncEffect(IMoviesService moviesService)
    {
        _moviesService = moviesService;
    }

    public override async Task HandleAsync(LoadMovies action, IRootState rootState)
    {
        try
        {
            var state = rootState.GetSliceState<MoviesState>();
            var response = await _moviesService.GetMoviesAsync(state.Pagination.CurrentPage, 5);
            Dispatcher.Dispatch(new LoadMoviesSuccess(response.Movies, response.TotalItems));
        }
        catch (Exception ex)
        {
            Dispatcher.Dispatch(new LoadMoviesFailure(ex));
        }
    }
}
```

This example:
- Performs an asynchronous API call.
- Dispatches success or failure actions based on the outcome.

**Example: Notifications after successful async operation**

```C#
public class NotifyLoadMoviesSuccessEffect : AsyncEffect<LoadMoviesSuccess>
{
    private readonly ISnackbar _snackbar;

    public NotifyLoadMoviesSuccessEffect(ISnackbar snackbar)
    {
        _snackbar = snackbar;
    }

    public override Task HandleAsync(LoadMoviesSuccess action, IRootState rootState)
    {
        var message = $"Successfully loaded {action.Movies.Length} movies.";
        _snackbar.Add(message, Severity.Success);
        Dispatcher.Dispatch(new AddNotification(new SuccessNotification(message)));
        return Task.CompletedTask;
    }
}
```

## Key Characteristics of Effects

- **Asynchronous**: Perform async tasks easily.
- **Dispatch Actions**: Dispatch new actions based on task outcomes.
- **Separation of Concerns**: Clearly separate state logic (reducers) from external interactions (effects).

## Custom Operators (Reactive Effects Only)

Ducky simplifies Reactive Effect logic with custom operators:
- **`OfType`**: Filters actions by type.
- **`WithSliceState`**: Combines actions with a specific state slice.
- **`InvokeService`**: Handles API calls, dispatching success/failure actions automatically.

## Best Practices

- **Single Responsibility**: Keep each effect focused.
- **Leverage Custom Operators**: Simplify Reactive Effect logic and enhance readability.
- **Handle Errors Explicitly**: Dispatch meaningful actions upon failure.
- **Maintain Reducer Purity**: Avoid side effects in reducers, using effects instead.

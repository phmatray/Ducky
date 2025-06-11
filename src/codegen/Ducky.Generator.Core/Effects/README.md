# Effects Generator

The Effects Generator creates reactive effect classes for handling side effects like API calls, timers, and other asynchronous operations in response to actions.

## Usage

```csharp
using Ducky.CodeGen.Core;

// Configure the generator
var options = new EffectsGeneratorOptions
{
    Namespace = "MyApp.Effects",
    EffectName = "TodoApiEffect",
    EffectType = EffectType.AsyncEffect,
    TriggerActions = new List<string>
    {
        "FetchTodosAction",
        "SaveTodoAction",
        "DeleteTodoAction"
    },
    TimeoutMs = 30000,
    Description = "Handles API operations for todo management"
};

// Generate the effect code
var generator = new EffectsGenerator();
var generatedCode = generator.GenerateCode(options);
```

## Generated Output

The generator produces reactive effect classes like this:

```csharp
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using Ducky;
using MyApp.Actions;
using MyApp.State;

namespace MyApp.Effects;

/// <summary>
/// Handles API operations for todo management
/// </summary>
public partial class TodoApiEffect : AsyncEffect
{
    private readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(30000);

    public override Observable<object> Handle(Observable<object> actions, Observable<IRootState> rootState)
    {
        return Observable.Merge(
            HandleFetchTodos(actions, rootState),
            HandleSaveTodo(actions, rootState),
            HandleDeleteTodo(actions, rootState)
        );
    }

    /// <summary>
    /// Handles FetchTodosAction effects.
    /// </summary>
    private partial Observable<object> HandleFetchTodos(Observable<object> actions, Observable<IRootState> rootState);

    /// <summary>
    /// Handles SaveTodoAction effects.
    /// </summary>
    private partial Observable<object> HandleSaveTodo(Observable<object> actions, Observable<IRootState> rootState);

    /// <summary>
    /// Handles DeleteTodoAction effects.
    /// </summary>
    private partial Observable<object> HandleDeleteTodo(Observable<object> actions, Observable<IRootState> rootState);
}
```

## Implementation

Developers implement the partial methods to provide custom effect logic:

```csharp
// TodoApiEffect.Implementation.cs
using System;
using System.Reactive.Linq;
using MyApp.Services;

namespace MyApp.Effects;

public partial class TodoApiEffect
{
    private readonly ITodoApiService _apiService;

    public TodoApiEffect(ITodoApiService apiService)
    {
        _apiService = apiService;
    }

    private partial Observable<object> HandleFetchTodos(Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<FetchTodosAction>()
            .SwitchSelect(action => 
                _apiService.GetTodosAsync()
                    .ToObservable()
                    .Select(todos => new FetchTodosSuccessAction(todos))
                    .Catch<object, Exception>(ex => 
                        Observable.Return(new FetchTodosErrorAction(ex.Message)))
                    .Timeout(_timeout)
            );
    }

    private partial Observable<object> HandleSaveTodo(Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<SaveTodoAction>()
            .SwitchSelect(action =>
                _apiService.SaveTodoAsync(action.Todo)
                    .ToObservable()
                    .Select(savedTodo => new SaveTodoSuccessAction(savedTodo))
                    .Catch<object, Exception>(ex =>
                        Observable.Return(new SaveTodoErrorAction(action.Todo.Id, ex.Message)))
                    .Timeout(_timeout)
            );
    }

    private partial Observable<object> HandleDeleteTodo(Observable<object> actions, Observable<IRootState> rootState)
    {
        return actions
            .OfActionType<DeleteTodoAction>()
            .SwitchSelect(action =>
                _apiService.DeleteTodoAsync(action.Id)
                    .ToObservable()
                    .Select(_ => new DeleteTodoSuccessAction(action.Id))
                    .Catch<object, Exception>(ex =>
                        Observable.Return(new DeleteTodoErrorAction(action.Id, ex.Message)))
                    .Timeout(_timeout)
            );
    }
}
```

## Effect Types

### AsyncEffect
For asynchronous operations like API calls:
```csharp
public class ApiEffect : AsyncEffect
{
    // Handles async operations with proper error handling and timeouts
}
```

### ReactiveEffect  
For reactive streams and real-time updates:
```csharp
public class WebSocketEffect : ReactiveEffect
{
    // Handles real-time streams, WebSocket connections, etc.
}
```

## Features

### 1. Reactive Patterns
- Uses Observable streams for composable async operations
- Built-in support for R3 reactive extensions
- Automatic subscription management

### 2. Error Handling
- Structured error handling with catch operators
- Timeout support for preventing hanging operations
- Graceful degradation patterns

### 3. Action Filtering
- Automatic filtering by action type using `OfActionType<T>()`
- Type-safe action handling
- Selective effect triggering

### 4. State Access
- Access to current root state via observable stream
- Reactive state updates for dependent operations
- State-dependent effect logic

## Benefits

1. **Separation of Concerns**: Side effects separated from reducers
2. **Testability**: Observable streams are easily testable
3. **Composability**: Effects can be combined and composed
4. **Error Safety**: Built-in error handling and timeout support
5. **Type Safety**: Strongly typed action and state handling
6. **Performance**: Efficient reactive stream processing

## Common Patterns

### API Call with Loading States
```csharp
private partial Observable<object> HandleLoadData(Observable<object> actions, Observable<IRootState> rootState)
{
    return actions
        .OfActionType<LoadDataAction>()
        .SwitchSelect(action => Observable.Merge(
            Observable.Return(new SetLoadingAction(true)),
            _apiService.LoadDataAsync()
                .ToObservable()
                .Select(data => new LoadDataSuccessAction(data))
                .Catch<object, Exception>(ex => Observable.Return(new LoadDataErrorAction(ex.Message)))
                .Finally(() => Observable.Return(new SetLoadingAction(false)))
        ));
}
```

### Debounced Search
```csharp
private partial Observable<object> HandleSearch(Observable<object> actions, Observable<IRootState> rootState)
{
    return actions
        .OfActionType<SearchAction>()
        .Debounce(TimeSpan.FromMilliseconds(300))
        .DistinctUntilChanged(action => action.Query)
        .SwitchSelect(action =>
            _searchService.SearchAsync(action.Query)
                .ToObservable()
                .Select(results => new SearchResultsAction(results))
                .Catch<object, Exception>(ex => Observable.Return(new SearchErrorAction(ex.Message)))
        );
}
```

### State-Dependent Effects
```csharp
private partial Observable<object> HandleConditionalAction(Observable<object> actions, Observable<IRootState> rootState)
{
    return actions
        .OfActionType<ConditionalAction>()
        .WithLatestFrom(rootState, (action, state) => new { action, state })
        .Where(x => x.state.User.IsAuthenticated) // Only process if authenticated
        .Select(x => x.action)
        .SwitchSelect(action => ProcessAuthenticatedAction(action));
}
```

## Integration with Store

```csharp
// Register effects with the store
services.AddDuckyStore(builder =>
{
    builder
        .AddDefaultMiddlewares()
        .AddEffect<TodoApiEffect>()
        .AddEffect<NotificationEffect>()
        .ConfigureStore(options => options.AssemblyNames = ["MyApp"]);
});
```
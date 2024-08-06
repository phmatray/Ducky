#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## Dispatcher Class

A dispatcher that queues and dispatches actions, providing an observable stream of dispatched actions.

```csharp
public sealed class Dispatcher :
R3dux.IDispatcher,
System.IDisposable
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; Dispatcher

Implements [R3dux.IDispatcher](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IDispatcher 'R3dux.IDispatcher'), [System.IDisposable](https://docs.microsoft.com/en-us/dotnet/api/System.IDisposable 'System.IDisposable')

| Properties | |
| :--- | :--- |
| [ActionStream](Dispatcher.ActionStream.md 'R3dux.Dispatcher.ActionStream') | Gets an observable stream of dispatched actions. |

| Methods | |
| :--- | :--- |
| [DequeueActions()](Dispatcher.DequeueActions().md 'R3dux.Dispatcher.DequeueActions()') | Dequeues and dispatches actions to the observable stream. |
| [Dispatch(IAction)](Dispatcher.Dispatch(IAction).md 'R3dux.Dispatcher.Dispatch(R3dux.IAction)') | Dispatches the specified action. |
| [Dispose()](Dispatcher.Dispose().md 'R3dux.Dispatcher.Dispose()') | Releases all resources used by the [Dispatcher](Dispatcher.md 'R3dux.Dispatcher') class. |

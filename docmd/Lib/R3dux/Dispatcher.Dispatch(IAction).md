#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[Dispatcher](Dispatcher.md 'R3dux.Dispatcher')

## Dispatcher.Dispatch(IAction) Method

Dispatches the specified action.

```csharp
public void Dispatch(R3dux.IAction action);
```
#### Parameters

<a name='R3dux.Dispatcher.Dispatch(R3dux.IAction).action'></a>

`action` [R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')

The action to dispatch.

Implements [Dispatch(IAction)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IDispatcher.Dispatch#R3dux_IDispatcher_Dispatch_R3dux_IAction_ 'R3dux.IDispatcher.Dispatch(R3dux.IAction)')

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
Thrown when the [action](Dispatcher.Dispatch(IAction).md#R3dux.Dispatcher.Dispatch(R3dux.IAction).action 'R3dux.Dispatcher.Dispatch(R3dux.IAction).action') is null.

[System.ObjectDisposedException](https://docs.microsoft.com/en-us/dotnet/api/System.ObjectDisposedException 'System.ObjectDisposedException')  
Thrown when the dispatcher has been disposed.
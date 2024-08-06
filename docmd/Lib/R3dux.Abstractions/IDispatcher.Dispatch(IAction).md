#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IDispatcher](IDispatcher.md 'R3dux.IDispatcher')

## IDispatcher.Dispatch(IAction) Method

Dispatches the specified action.

```csharp
void Dispatch(R3dux.IAction action);
```
#### Parameters

<a name='R3dux.IDispatcher.Dispatch(R3dux.IAction).action'></a>

`action` [IAction](IAction.md 'R3dux.IAction')

The action to dispatch.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
Thrown when the [action](IDispatcher.Dispatch(IAction).md#R3dux.IDispatcher.Dispatch(R3dux.IAction).action 'R3dux.IDispatcher.Dispatch(R3dux.IAction).action') is null.

[System.ObjectDisposedException](https://docs.microsoft.com/en-us/dotnet/api/System.ObjectDisposedException 'System.ObjectDisposedException')  
Thrown when the dispatcher has been disposed.
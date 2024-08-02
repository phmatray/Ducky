#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IDispatcher Interface

Defines the contract for a dispatcher that can dispatch actions and provide an observable stream of dispatched actions.

```csharp
public interface IDispatcher
```

| Properties | |
| :--- | :--- |
| [ActionStream](IDispatcher.ActionStream.md 'R3dux.IDispatcher.ActionStream') | Gets an observable stream of dispatched actions. |

| Methods | |
| :--- | :--- |
| [Dispatch(IAction)](IDispatcher.Dispatch(IAction).md 'R3dux.IDispatcher.Dispatch(R3dux.IAction)') | Dispatches the specified action. |

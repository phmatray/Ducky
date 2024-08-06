#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[StateChange&lt;TState&gt;](StateChange_TState_.md 'R3dux.StateChange<TState>')

## StateChange(IAction, TState, TState, double) Constructor

Represents a state change notification.

```csharp
public StateChange(R3dux.IAction Action, TState PreviousState, TState NewState, double ElapsedMilliseconds);
```
#### Parameters

<a name='R3dux.StateChange_TState_.StateChange(R3dux.IAction,TState,TState,double).Action'></a>

`Action` [R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')

The action causing the state change.

<a name='R3dux.StateChange_TState_.StateChange(R3dux.IAction,TState,TState,double).PreviousState'></a>

`PreviousState` [TState](StateChange_TState_.md#R3dux.StateChange_TState_.TState 'R3dux.StateChange<TState>.TState')

The previous state before the change.

<a name='R3dux.StateChange_TState_.StateChange(R3dux.IAction,TState,TState,double).NewState'></a>

`NewState` [TState](StateChange_TState_.md#R3dux.StateChange_TState_.TState 'R3dux.StateChange<TState>.TState')

The new state after the change.

<a name='R3dux.StateChange_TState_.StateChange(R3dux.IAction,TState,TState,double).ElapsedMilliseconds'></a>

`ElapsedMilliseconds` [System.Double](https://docs.microsoft.com/en-us/dotnet/api/System.Double 'System.Double')

The time taken for the state change in milliseconds.
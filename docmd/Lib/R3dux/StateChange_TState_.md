#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## StateChange<TState> Class

Represents a state change notification.

```csharp
public sealed class StateChange<TState> :
System.IEquatable<R3dux.StateChange<TState>>
```
#### Type parameters

<a name='R3dux.StateChange_TState_.TState'></a>

`TState`

The type of the state.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; StateChange<TState>

Implements [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.StateChange&lt;](StateChange_TState_.md 'R3dux.StateChange<TState>')[TState](StateChange_TState_.md#R3dux.StateChange_TState_.TState 'R3dux.StateChange<TState>.TState')[&gt;](StateChange_TState_.md 'R3dux.StateChange<TState>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [StateChange(IAction, TState, TState, double)](StateChange_TState_.StateChange(IAction,TState,TState,double).md 'R3dux.StateChange<TState>.StateChange(R3dux.IAction, TState, TState, double)') | Represents a state change notification. |

| Properties | |
| :--- | :--- |
| [Action](StateChange_TState_.Action.md 'R3dux.StateChange<TState>.Action') | The action causing the state change. |
| [ElapsedMilliseconds](StateChange_TState_.ElapsedMilliseconds.md 'R3dux.StateChange<TState>.ElapsedMilliseconds') | The time taken for the state change in milliseconds. |
| [NewState](StateChange_TState_.NewState.md 'R3dux.StateChange<TState>.NewState') | The new state after the change. |
| [PreviousState](StateChange_TState_.PreviousState.md 'R3dux.StateChange<TState>.PreviousState') | The previous state before the change. |

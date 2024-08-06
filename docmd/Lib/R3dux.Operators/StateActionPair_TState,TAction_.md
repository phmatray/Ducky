#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux')

## StateActionPair<TState,TAction> Class

Represents a pair of state and action.

```csharp
public sealed class StateActionPair<TState,TAction> :
System.IEquatable<R3dux.StateActionPair<TState, TAction>>
    where TAction : R3dux.IAction
```
#### Type parameters

<a name='R3dux.StateActionPair_TState,TAction_.TState'></a>

`TState`

The type of the state.

<a name='R3dux.StateActionPair_TState,TAction_.TAction'></a>

`TAction`

The type of the action, which must implement [R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction').

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; StateActionPair<TState,TAction>

Implements [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.StateActionPair&lt;](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[TState](StateActionPair_TState,TAction_.md#R3dux.StateActionPair_TState,TAction_.TState 'R3dux.StateActionPair<TState,TAction>.TState')[,](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[TAction](StateActionPair_TState,TAction_.md#R3dux.StateActionPair_TState,TAction_.TAction 'R3dux.StateActionPair<TState,TAction>.TAction')[&gt;](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [StateActionPair(TState, TAction)](StateActionPair_TState,TAction_.StateActionPair(TState,TAction).md 'R3dux.StateActionPair<TState,TAction>.StateActionPair(TState, TAction)') | Represents a pair of state and action. |

| Properties | |
| :--- | :--- |
| [Action](StateActionPair_TState,TAction_.Action.md 'R3dux.StateActionPair<TState,TAction>.Action') | The action to be performed. |
| [State](StateActionPair_TState,TAction_.State.md 'R3dux.StateActionPair<TState,TAction>.State') | The current state. |

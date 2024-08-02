#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## StateLoggerObserver<TState> Class

Provides logging functionalities for state changes.

```csharp
public sealed class StateLoggerObserver<TState> : R3.Observer<R3dux.StateChange<TState>>
```
#### Type parameters

<a name='R3dux.StateLoggerObserver_TState_.TState'></a>

`TState`

The type of the state.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [R3.Observer&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observer-1 'R3.Observer`1')[R3dux.StateChange&lt;](StateChange_TState_.md 'R3dux.StateChange<TState>')[TState](StateLoggerObserver_TState_.md#R3dux.StateLoggerObserver_TState_.TState 'R3dux.StateLoggerObserver<TState>.TState')[&gt;](StateChange_TState_.md 'R3dux.StateChange<TState>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observer-1 'R3.Observer`1') &#129106; StateLoggerObserver<TState>

| Methods | |
| :--- | :--- |
| [GetActionType(IAction)](StateLoggerObserver_TState_.GetActionType(IAction).md 'R3dux.StateLoggerObserver<TState>.GetActionType(R3dux.IAction)') | Gets the name of the action type. |
| [GetObjectDetails(object)](StateLoggerObserver_TState_.GetObjectDetails(object).md 'R3dux.StateLoggerObserver<TState>.GetObjectDetails(object)') | Gets the details of an object. |
| [LogStateChange(StateChange&lt;TState&gt;)](StateLoggerObserver_TState_.LogStateChange(StateChange_TState_).md 'R3dux.StateLoggerObserver<TState>.LogStateChange(R3dux.StateChange<TState>)') | Logs the state change. |
| [OnCompletedCore(Result)](StateLoggerObserver_TState_.OnCompletedCore(Result).md 'R3dux.StateLoggerObserver<TState>.OnCompletedCore(R3.Result)') | Handles the completion of the state change stream. |
| [OnErrorResumeCore(Exception)](StateLoggerObserver_TState_.OnErrorResumeCore(Exception).md 'R3dux.StateLoggerObserver<TState>.OnErrorResumeCore(System.Exception)') | Handles an error in the state change stream. |
| [OnNextCore(StateChange&lt;TState&gt;)](StateLoggerObserver_TState_.OnNextCore(StateChange_TState_).md 'R3dux.StateLoggerObserver<TState>.OnNextCore(R3dux.StateChange<TState>)') | Handles the reception of a state change notification. |

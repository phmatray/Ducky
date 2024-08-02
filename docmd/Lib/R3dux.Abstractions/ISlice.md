#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## ISlice Interface

Represents a state slice with basic state management capabilities.

```csharp
public interface ISlice
```

Derived  
&#8627; [ISlice&lt;TState&gt;](ISlice_TState_.md 'R3dux.ISlice<TState>')

| Properties | |
| :--- | :--- |
| [StateUpdated](ISlice.StateUpdated.md 'R3dux.ISlice.StateUpdated') | Gets an observable sequence that produces a notification when the state is updated. |

| Methods | |
| :--- | :--- |
| [GetKey()](ISlice.GetKey().md 'R3dux.ISlice.GetKey()') | Gets the unique key for this state slice. |
| [GetState()](ISlice.GetState().md 'R3dux.ISlice.GetState()') | Gets the current state of this slice. |
| [GetStateType()](ISlice.GetStateType().md 'R3dux.ISlice.GetStateType()') | Gets the type of the state managed by this slice. |
| [OnDispatch(IAction)](ISlice.OnDispatch(IAction).md 'R3dux.ISlice.OnDispatch(R3dux.IAction)') | Handles the dispatch of an action. |

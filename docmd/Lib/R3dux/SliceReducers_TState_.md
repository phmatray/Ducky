#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## SliceReducers<TState> Class

Represents a strongly-typed state slice with state management and reducers.

```csharp
public abstract class SliceReducers<TState> :
R3dux.ISlice<TState>,
R3dux.ISlice,
System.IDisposable,
System.IEquatable<R3dux.SliceReducers<TState>>
```
#### Type parameters

<a name='R3dux.SliceReducers_TState_.TState'></a>

`TState`

The type of the state managed by this slice.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; SliceReducers<TState>

Implements [R3dux.ISlice&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice-1 'R3dux.ISlice`1')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice-1 'R3dux.ISlice`1'), [R3dux.ISlice](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice 'R3dux.ISlice'), [System.IDisposable](https://docs.microsoft.com/en-us/dotnet/api/System.IDisposable 'System.IDisposable'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.SliceReducers&lt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

### Remarks
Each "slice reducer" is responsible for providing an initial value  
and calculating the updates to that slice of the state.

| Properties | |
| :--- | :--- |
| [Reducers](SliceReducers_TState_.Reducers.md 'R3dux.SliceReducers<TState>.Reducers') | Gets a dictionary that holds the reducers mapped by the type of action. |
| [State](SliceReducers_TState_.State.md 'R3dux.SliceReducers<TState>.State') | Gets an observable sequence that produces the state of this slice. |
| [StateUpdated](SliceReducers_TState_.StateUpdated.md 'R3dux.SliceReducers<TState>.StateUpdated') | Gets an observable sequence that produces a notification when the state is updated. |

| Methods | |
| :--- | :--- |
| [Dispose()](SliceReducers_TState_.Dispose().md 'R3dux.SliceReducers<TState>.Dispose()') | Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources. |
| [Dispose(bool)](SliceReducers_TState_.Dispose(bool).md 'R3dux.SliceReducers<TState>.Dispose(bool)') | Releases the unmanaged resources used by the [SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>') and optionally releases the managed resources. |
| [GetInitialState()](SliceReducers_TState_.GetInitialState().md 'R3dux.SliceReducers<TState>.GetInitialState()') | Gets the initial state of the reducer. |
| [GetKey()](SliceReducers_TState_.GetKey().md 'R3dux.SliceReducers<TState>.GetKey()') | Gets the unique key for this state slice. |
| [GetState()](SliceReducers_TState_.GetState().md 'R3dux.SliceReducers<TState>.GetState()') | Gets the current state of this slice. |
| [GetStateType()](SliceReducers_TState_.GetStateType().md 'R3dux.SliceReducers<TState>.GetStateType()') | Gets the type of the state managed by this slice. |
| [LowerCharUpperCharRegex()](SliceReducers_TState_.LowerCharUpperCharRegex().md 'R3dux.SliceReducers<TState>.LowerCharUpperCharRegex()') | |
| [On&lt;TAction&gt;(Func&lt;TState,TAction,TState&gt;)](SliceReducers_TState_.On_TAction_(Func_TState,TAction,TState_).md 'R3dux.SliceReducers<TState>.On<TAction>(System.Func<TState,TAction,TState>)') | Maps a reducer function to a specific action type. |
| [On&lt;TAction&gt;(Func&lt;TState,TState&gt;)](SliceReducers_TState_.On_TAction_(Func_TState,TState_).md 'R3dux.SliceReducers<TState>.On<TAction>(System.Func<TState,TState>)') | Maps a reducer function to a specific action type. |
| [On&lt;TAction&gt;(Func&lt;TState&gt;)](SliceReducers_TState_.On_TAction_(Func_TState_).md 'R3dux.SliceReducers<TState>.On<TAction>(System.Func<TState>)') | Maps a reducer function to a specific action type. |
| [OnDispatch(IAction)](SliceReducers_TState_.OnDispatch(IAction).md 'R3dux.SliceReducers<TState>.OnDispatch(R3dux.IAction)') | Handles the dispatch of an action. |
| [Reduce(TState, IAction)](SliceReducers_TState_.Reduce(TState,IAction).md 'R3dux.SliceReducers<TState>.Reduce(TState, R3dux.IAction)') | Reduces the state using the appropriate reducer for the given action. |

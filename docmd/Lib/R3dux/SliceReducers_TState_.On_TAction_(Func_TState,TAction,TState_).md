#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.On<TAction>(Func<TState,TAction,TState>) Method

Maps a reducer function to a specific action type.

```csharp
public void On<TAction>(System.Func<TState,TAction,TState> reducer)
    where TAction : R3dux.IAction;
```
#### Type parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState,TAction,TState_).TAction'></a>

`TAction`

The type of the action.
#### Parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState,TAction,TState_).reducer'></a>

`reducer` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[TAction](SliceReducers_TState_.On_TAction_(Func_TState,TAction,TState_).md#R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState,TAction,TState_).TAction 'R3dux.SliceReducers<TState>.On<TAction>(System.Func<TState,TAction,TState>).TAction')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-3 'System.Func`3')

The reducer function that takes the state and action and returns a new state.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
Thrown when the reducer is null.
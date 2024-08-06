#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.On<TAction>(Func<TState,TState>) Method

Maps a reducer function to a specific action type.

```csharp
public void On<TAction>(System.Func<TState,TState> reducer);
```
#### Type parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState,TState_).TAction'></a>

`TAction`

The type of the action.
#### Parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState,TState_).reducer'></a>

`reducer` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The reducer function that takes only the state and returns a new state.
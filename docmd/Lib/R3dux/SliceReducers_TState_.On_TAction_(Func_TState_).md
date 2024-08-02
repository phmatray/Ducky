#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.On<TAction>(Func<TState>) Method

Maps a reducer function to a specific action type.

```csharp
public void On<TAction>(System.Func<TState> reducer);
```
#### Type parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState_).TAction'></a>

`TAction`

The type of the action.
#### Parameters

<a name='R3dux.SliceReducers_TState_.On_TAction_(System.Func_TState_).reducer'></a>

`reducer` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-1 'System.Func`1')[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-1 'System.Func`1')

The reducer function that takes no arguments and returns a new state.
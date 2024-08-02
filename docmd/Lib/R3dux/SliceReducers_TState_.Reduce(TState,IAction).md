#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[SliceReducers&lt;TState&gt;](SliceReducers_TState_.md 'R3dux.SliceReducers<TState>')

## SliceReducers<TState>.Reduce(TState, IAction) Method

Reduces the state using the appropriate reducer for the given action.

```csharp
public TState Reduce(TState state, R3dux.IAction action);
```
#### Parameters

<a name='R3dux.SliceReducers_TState_.Reduce(TState,R3dux.IAction).state'></a>

`state` [TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')

The current state.

<a name='R3dux.SliceReducers_TState_.Reduce(TState,R3dux.IAction).action'></a>

`action` [R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')

The action to apply to the state.

#### Returns
[TState](SliceReducers_TState_.md#R3dux.SliceReducers_TState_.TState 'R3dux.SliceReducers<TState>.TState')  
The new state after applying the reducer, or the original state if no reducer is found.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
Thrown when the action is null.
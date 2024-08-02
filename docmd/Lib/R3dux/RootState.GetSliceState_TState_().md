#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[RootState](RootState.md 'R3dux.RootState')

## RootState.GetSliceState<TState>() Method

Gets the slice state of the specified type.

```csharp
public TState GetSliceState<TState>()
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.RootState.GetSliceState_TState_().TState'></a>

`TState`

The type of the state to select.

Implements [GetSliceState&lt;TState&gt;()](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState.GetSliceState--1 'R3dux.IRootState.GetSliceState``1')

#### Returns
[TState](RootState.GetSliceState_TState_().md#R3dux.RootState.GetSliceState_TState_().TState 'R3dux.RootState.GetSliceState<TState>().TState')  
The state of the specified type.
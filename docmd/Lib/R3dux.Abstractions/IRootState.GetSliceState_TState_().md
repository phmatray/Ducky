#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootState](IRootState.md 'R3dux.IRootState')

## IRootState.GetSliceState<TState>() Method

Gets the slice state of the specified type.

```csharp
TState GetSliceState<TState>()
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.IRootState.GetSliceState_TState_().TState'></a>

`TState`

The type of the state to select.

#### Returns
[TState](IRootState.GetSliceState_TState_().md#R3dux.IRootState.GetSliceState_TState_().TState 'R3dux.IRootState.GetSliceState<TState>().TState')  
The state of the specified type.
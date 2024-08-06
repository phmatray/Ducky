#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootState](IRootState.md 'R3dux.IRootState')

## IRootState.GetSliceState<TState>(string) Method

Gets the slice state associated with the specified key.

```csharp
TState GetSliceState<TState>(string key)
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.IRootState.GetSliceState_TState_(string).TState'></a>

`TState`

The type of the state to select.
#### Parameters

<a name='R3dux.IRootState.GetSliceState_TState_(string).key'></a>

`key` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The key of the state to select.

#### Returns
[TState](IRootState.GetSliceState_TState_(string).md#R3dux.IRootState.GetSliceState_TState_(string).TState 'R3dux.IRootState.GetSliceState<TState>(string).TState')  
The state associated with the specified key.
#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[RootState](RootState.md 'R3dux.RootState')

## RootState.GetSliceState<TState>(string) Method

Gets the slice state associated with the specified key.

```csharp
public TState GetSliceState<TState>(string key)
    where TState : notnull;
```
#### Type parameters

<a name='R3dux.RootState.GetSliceState_TState_(string).TState'></a>

`TState`

The type of the state to select.
#### Parameters

<a name='R3dux.RootState.GetSliceState_TState_(string).key'></a>

`key` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The key of the state to select.

Implements [GetSliceState&lt;TState&gt;(string)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState.GetSliceState--1#R3dux_IRootState_GetSliceState__1_System_String_ 'R3dux.IRootState.GetSliceState``1(System.String)')

#### Returns
[TState](RootState.GetSliceState_TState_(string).md#R3dux.RootState.GetSliceState_TState_(string).TState 'R3dux.RootState.GetSliceState<TState>(string).TState')  
The state associated with the specified key.
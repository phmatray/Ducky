#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[RootStateSerializer](RootStateSerializer.md 'R3dux.RootStateSerializer')

## RootStateSerializer.Serialize(IRootState, string) Method

Serializes the slice state associated with the specified key to a JSON string.

```csharp
public string Serialize(R3dux.IRootState rootState, string key);
```
#### Parameters

<a name='R3dux.RootStateSerializer.Serialize(R3dux.IRootState,string).rootState'></a>

`rootState` [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState')

The [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') to serialize.

<a name='R3dux.RootStateSerializer.Serialize(R3dux.IRootState,string).key'></a>

`key` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The key of the slice state to serialize.

Implements [Serialize(IRootState, string)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootStateSerializer.Serialize#R3dux_IRootStateSerializer_Serialize_R3dux_IRootState,System_String_ 'R3dux.IRootStateSerializer.Serialize(R3dux.IRootState,System.String)')

#### Returns
[System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')  
The JSON string representation of the slice state.
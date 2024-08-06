#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[RootStateSerializer](RootStateSerializer.md 'R3dux.RootStateSerializer')

## RootStateSerializer.Serialize(IRootState) Method

Serializes the specified [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') to a JSON string.

```csharp
public string Serialize(R3dux.IRootState rootState);
```
#### Parameters

<a name='R3dux.RootStateSerializer.Serialize(R3dux.IRootState).rootState'></a>

`rootState` [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState')

The [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') to serialize.

Implements [Serialize(IRootState)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootStateSerializer.Serialize#R3dux_IRootStateSerializer_Serialize_R3dux_IRootState_ 'R3dux.IRootStateSerializer.Serialize(R3dux.IRootState)')

#### Returns
[System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')  
The JSON string representation of the state.
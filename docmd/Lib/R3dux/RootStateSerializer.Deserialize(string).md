#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[RootStateSerializer](RootStateSerializer.md 'R3dux.RootStateSerializer')

## RootStateSerializer.Deserialize(string) Method

Deserializes a [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') from a JSON string.

```csharp
public R3dux.IRootState Deserialize(string json);
```
#### Parameters

<a name='R3dux.RootStateSerializer.Deserialize(string).json'></a>

`json` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The JSON string representation of the state.

Implements [Deserialize(string)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootStateSerializer.Deserialize#R3dux_IRootStateSerializer_Deserialize_System_String_ 'R3dux.IRootStateSerializer.Deserialize(System.String)')

#### Returns
[R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState')  
A new instance of [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') with the deserialized state.
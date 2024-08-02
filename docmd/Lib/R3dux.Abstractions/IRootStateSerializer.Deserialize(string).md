#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer')

## IRootStateSerializer.Deserialize(string) Method

Deserializes a [IRootState](IRootState.md 'R3dux.IRootState') from a JSON string.

```csharp
R3dux.IRootState Deserialize(string json);
```
#### Parameters

<a name='R3dux.IRootStateSerializer.Deserialize(string).json'></a>

`json` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The JSON string representation of the state.

#### Returns
[IRootState](IRootState.md 'R3dux.IRootState')  
A new instance of [IRootState](IRootState.md 'R3dux.IRootState') with the deserialized state.
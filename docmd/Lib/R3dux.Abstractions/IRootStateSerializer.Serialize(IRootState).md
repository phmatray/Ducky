#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer')

## IRootStateSerializer.Serialize(IRootState) Method

Serializes the specified [IRootState](IRootState.md 'R3dux.IRootState') to a JSON string.

```csharp
string Serialize(R3dux.IRootState rootState);
```
#### Parameters

<a name='R3dux.IRootStateSerializer.Serialize(R3dux.IRootState).rootState'></a>

`rootState` [IRootState](IRootState.md 'R3dux.IRootState')

The [IRootState](IRootState.md 'R3dux.IRootState') to serialize.

#### Returns
[System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')  
The JSON string representation of the state.
#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer')

## IRootStateSerializer.Serialize(IRootState, string) Method

Serializes the slice state associated with the specified key to a JSON string.

```csharp
string Serialize(R3dux.IRootState rootState, string key);
```
#### Parameters

<a name='R3dux.IRootStateSerializer.Serialize(R3dux.IRootState,string).rootState'></a>

`rootState` [IRootState](IRootState.md 'R3dux.IRootState')

The [IRootState](IRootState.md 'R3dux.IRootState') to serialize.

<a name='R3dux.IRootStateSerializer.Serialize(R3dux.IRootState,string).key'></a>

`key` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The key of the slice state to serialize.

#### Returns
[System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')  
The JSON string representation of the slice state.
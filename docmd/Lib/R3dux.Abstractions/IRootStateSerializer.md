#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IRootStateSerializer Interface

Provides methods for serializing and deserializing [IRootState](IRootState.md 'R3dux.IRootState') instances.

```csharp
public interface IRootStateSerializer
```

| Methods | |
| :--- | :--- |
| [Deserialize(string)](IRootStateSerializer.Deserialize(string).md 'R3dux.IRootStateSerializer.Deserialize(string)') | Deserializes a [IRootState](IRootState.md 'R3dux.IRootState') from a JSON string. |
| [LoadFromFile(string)](IRootStateSerializer.LoadFromFile(string).md 'R3dux.IRootStateSerializer.LoadFromFile(string)') | Loads a [IRootState](IRootState.md 'R3dux.IRootState') from a file. |
| [SaveToFile(IRootState, string)](IRootStateSerializer.SaveToFile(IRootState,string).md 'R3dux.IRootStateSerializer.SaveToFile(R3dux.IRootState, string)') | Saves the specified [IRootState](IRootState.md 'R3dux.IRootState') to a file. |
| [Serialize(IRootState, string)](IRootStateSerializer.Serialize(IRootState,string).md 'R3dux.IRootStateSerializer.Serialize(R3dux.IRootState, string)') | Serializes the slice state associated with the specified key to a JSON string. |
| [Serialize(IRootState)](IRootStateSerializer.Serialize(IRootState).md 'R3dux.IRootStateSerializer.Serialize(R3dux.IRootState)') | Serializes the specified [IRootState](IRootState.md 'R3dux.IRootState') to a JSON string. |

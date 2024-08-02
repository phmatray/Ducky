#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## RootStateSerializer Class

Provides methods for serializing and deserializing [RootState](RootState.md 'R3dux.RootState') instances.

```csharp
public sealed class RootStateSerializer :
R3dux.IRootStateSerializer
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; RootStateSerializer

Implements [R3dux.IRootStateSerializer](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootStateSerializer 'R3dux.IRootStateSerializer')

| Methods | |
| :--- | :--- |
| [Deserialize(string)](RootStateSerializer.Deserialize(string).md 'R3dux.RootStateSerializer.Deserialize(string)') | Deserializes a [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') from a JSON string. |
| [LoadFromFile(string)](RootStateSerializer.LoadFromFile(string).md 'R3dux.RootStateSerializer.LoadFromFile(string)') | Loads a [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') from a file. |
| [SaveToFile(IRootState, string)](RootStateSerializer.SaveToFile(IRootState,string).md 'R3dux.RootStateSerializer.SaveToFile(R3dux.IRootState, string)') | Saves the specified [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') to a file. |
| [Serialize(IRootState, string)](RootStateSerializer.Serialize(IRootState,string).md 'R3dux.RootStateSerializer.Serialize(R3dux.IRootState, string)') | Serializes the slice state associated with the specified key to a JSON string. |
| [Serialize(IRootState)](RootStateSerializer.Serialize(IRootState).md 'R3dux.RootStateSerializer.Serialize(R3dux.IRootState)') | Serializes the specified [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState') to a JSON string. |

#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer')

## IRootStateSerializer.SaveToFile(IRootState, string) Method

Saves the specified [IRootState](IRootState.md 'R3dux.IRootState') to a file.

```csharp
void SaveToFile(R3dux.IRootState rootState, string filePath);
```
#### Parameters

<a name='R3dux.IRootStateSerializer.SaveToFile(R3dux.IRootState,string).rootState'></a>

`rootState` [IRootState](IRootState.md 'R3dux.IRootState')

The [IRootState](IRootState.md 'R3dux.IRootState') to save.

<a name='R3dux.IRootStateSerializer.SaveToFile(R3dux.IRootState,string).filePath'></a>

`filePath` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The path of the file to save the state to.
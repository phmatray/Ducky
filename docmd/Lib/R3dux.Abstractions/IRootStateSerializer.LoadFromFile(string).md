#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootStateSerializer](IRootStateSerializer.md 'R3dux.IRootStateSerializer')

## IRootStateSerializer.LoadFromFile(string) Method

Loads a [IRootState](IRootState.md 'R3dux.IRootState') from a file.

```csharp
R3dux.IRootState LoadFromFile(string filePath);
```
#### Parameters

<a name='R3dux.IRootStateSerializer.LoadFromFile(string).filePath'></a>

`filePath` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The path of the file to load the state from.

#### Returns
[IRootState](IRootState.md 'R3dux.IRootState')  
A new instance of [IRootState](IRootState.md 'R3dux.IRootState') with the loaded state.
#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IRootState](IRootState.md 'R3dux.IRootState')

## IRootState.GetStateDictionary() Method

Gets the underlying state dictionary for serialization purposes.

```csharp
System.Collections.Immutable.ImmutableSortedDictionary<string,object> GetStateDictionary();
```

#### Returns
[System.Collections.Immutable.ImmutableSortedDictionary&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableSortedDictionary-2 'System.Collections.Immutable.ImmutableSortedDictionary`2')[System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableSortedDictionary-2 'System.Collections.Immutable.ImmutableSortedDictionary`2')[System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableSortedDictionary-2 'System.Collections.Immutable.ImmutableSortedDictionary`2')  
The state dictionary.
#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## RootState Class

Represents the root state of the application, managing slice states.

```csharp
public sealed class RootState :
R3dux.IRootState,
System.IEquatable<R3dux.RootState>
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; RootState

Implements [R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[RootState](RootState.md 'R3dux.RootState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [RootState(ImmutableSortedDictionary&lt;string,object&gt;)](RootState.RootState(ImmutableSortedDictionary_string,object_).md 'R3dux.RootState.RootState(System.Collections.Immutable.ImmutableSortedDictionary<string,object>)') | Initializes a new instance of the [RootState](RootState.md 'R3dux.RootState') class. |

| Methods | |
| :--- | :--- |
| [ContainsKey(string)](RootState.ContainsKey(string).md 'R3dux.RootState.ContainsKey(string)') | Determines whether the state contains an element with the specified key. |
| [GetKeys()](RootState.GetKeys().md 'R3dux.RootState.GetKeys()') | Gets the keys of the state. |
| [GetSliceState&lt;TState&gt;()](RootState.GetSliceState_TState_().md 'R3dux.RootState.GetSliceState<TState>()') | Gets the slice state of the specified type. |
| [GetSliceState&lt;TState&gt;(string)](RootState.GetSliceState_TState_(string).md 'R3dux.RootState.GetSliceState<TState>(string)') | Gets the slice state associated with the specified key. |
| [GetStateDictionary()](RootState.GetStateDictionary().md 'R3dux.RootState.GetStateDictionary()') | Gets the underlying state dictionary for serialization purposes. |

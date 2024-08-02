#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IRootState Interface

Represents the root state of the application, managing slice states.

```csharp
public interface IRootState
```

| Methods | |
| :--- | :--- |
| [ContainsKey(string)](IRootState.ContainsKey(string).md 'R3dux.IRootState.ContainsKey(string)') | Determines whether the state contains an element with the specified key. |
| [GetKeys()](IRootState.GetKeys().md 'R3dux.IRootState.GetKeys()') | Gets the keys of the state. |
| [GetSliceState&lt;TState&gt;()](IRootState.GetSliceState_TState_().md 'R3dux.IRootState.GetSliceState<TState>()') | Gets the slice state of the specified type. |
| [GetSliceState&lt;TState&gt;(string)](IRootState.GetSliceState_TState_(string).md 'R3dux.IRootState.GetSliceState<TState>(string)') | Gets the slice state associated with the specified key. |
| [GetStateDictionary()](IRootState.GetStateDictionary().md 'R3dux.IRootState.GetStateDictionary()') | Gets the underlying state dictionary for serialization purposes. |

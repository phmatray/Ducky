#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.ContainsKey(TKey) Method

Checks if an entity with the specified key exists in the state.

```csharp
public bool ContainsKey(TKey key);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.ContainsKey(TKey).key'></a>

`key` [TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')

The key of the entity.

#### Returns
[System.Boolean](https://docs.microsoft.com/en-us/dotnet/api/System.Boolean 'System.Boolean')  
`true` if the entity exists; otherwise, `false`.
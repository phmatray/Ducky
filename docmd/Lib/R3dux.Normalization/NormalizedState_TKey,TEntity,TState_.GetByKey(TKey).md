#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.GetByKey(TKey) Method

Gets an entity by its key.

```csharp
public TEntity GetByKey(TKey key);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.GetByKey(TKey).key'></a>

`key` [TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')

The key of the entity.

#### Returns
[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')  
The entity if found; otherwise, `null`.

#### Exceptions

[R3dux.R3duxException](https://docs.microsoft.com/en-us/dotnet/api/R3dux.R3duxException 'R3dux.R3duxException')  
The entity does not exist.
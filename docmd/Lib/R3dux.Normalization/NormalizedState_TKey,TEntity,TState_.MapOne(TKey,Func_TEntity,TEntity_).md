#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.MapOne(TKey, Func<TEntity,TEntity>) Method

Updates one entity in the collection by defining a map function.

```csharp
public TState MapOne(TKey key, System.Func<TEntity,TEntity> map);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.MapOne(TKey,System.Func_TEntity,TEntity_).key'></a>

`key` [TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')

The key of the entity to map.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.MapOne(TKey,System.Func_TEntity,TEntity_).map'></a>

`map` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The map function to apply to the entity.

Implements [MapOne(TKey, Func&lt;TEntity,TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.MapOne(TKey,Func_TEntity,TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.MapOne(TKey, System.Func<TEntity,TEntity>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entity mapped.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The key and map function must not be null.
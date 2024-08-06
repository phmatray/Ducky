#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.UpdateOne(TKey, Action<TEntity>) Method

Updates one entity in the collection. Supports partial updates.

```csharp
public TState UpdateOne(TKey key, System.Action<TEntity> update);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.UpdateOne(TKey,System.Action_TEntity_).key'></a>

`key` [TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')

The key of the entity to update.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.UpdateOne(TKey,System.Action_TEntity_).update'></a>

`update` [System.Action&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')

The update action to apply to the entity.

Implements [UpdateOne(TKey, Action&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateOne(TKey,Action_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateOne(TKey, System.Action<TEntity>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entity updated.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The key and update action must not be null.
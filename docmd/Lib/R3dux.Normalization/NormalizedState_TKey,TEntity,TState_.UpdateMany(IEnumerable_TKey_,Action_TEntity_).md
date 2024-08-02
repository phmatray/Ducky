#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.UpdateMany(IEnumerable<TKey>, Action<TEntity>) Method

Updates multiple entities in the collection. Supports partial updates.

```csharp
public TState UpdateMany(System.Collections.Generic.IEnumerable<TKey> keys, System.Action<TEntity> update);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.UpdateMany(System.Collections.Generic.IEnumerable_TKey_,System.Action_TEntity_).keys'></a>

`keys` [System.Collections.Generic.IEnumerable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')[TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')

The keys of the entities to update.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.UpdateMany(System.Collections.Generic.IEnumerable_TKey_,System.Action_TEntity_).update'></a>

`update` [System.Action&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')

The update action to apply to the entities.

Implements [UpdateMany(IEnumerable&lt;TKey&gt;, Action&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateMany(IEnumerable_TKey_,Action_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateMany(System.Collections.Generic.IEnumerable<TKey>, System.Action<TEntity>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entities updated.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The keys collection and update action must not be null.
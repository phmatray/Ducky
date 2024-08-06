#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.SetAll(IEnumerable<TEntity>) Method

Replaces the current collection with the provided collection.

```csharp
public TState SetAll(System.Collections.Generic.IEnumerable<TEntity> entities);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.SetAll(System.Collections.Generic.IEnumerable_TEntity_).entities'></a>

`entities` [System.Collections.Generic.IEnumerable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')

The entities to set.

Implements [SetAll(IEnumerable&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.SetAll(IEnumerable_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.SetAll(System.Collections.Generic.IEnumerable<TEntity>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entities set.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The entities collection must not be null.
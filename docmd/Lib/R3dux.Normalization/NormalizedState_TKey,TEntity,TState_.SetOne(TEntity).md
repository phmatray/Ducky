#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.SetOne(TEntity) Method

Adds or replaces one entity in the collection.

```csharp
public TState SetOne(TEntity entity);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.SetOne(TEntity).entity'></a>

`entity` [TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')

The entity to set.

Implements [SetOne(TEntity)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.SetOne(TEntity).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.SetOne(TEntity)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entity set.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The entity must not be null.
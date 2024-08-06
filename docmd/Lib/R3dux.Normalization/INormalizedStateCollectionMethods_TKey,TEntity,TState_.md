#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux')

## INormalizedStateCollectionMethods<TKey,TEntity,TState> Interface

Defines methods for managing a collection of entities within a normalized state.

```csharp
public interface INormalizedStateCollectionMethods<in TKey,TEntity,out TState>
    where TKey : System.IEquatable<TKey>
    where TEntity : R3dux.IEntity<TKey>
    where TState : R3dux.NormalizedState<TKey, TEntity, TState>, new()
```
#### Type parameters

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TKey'></a>

`TKey`

The type of the entity key.

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TEntity'></a>

`TEntity`

The type of the entity.

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TState'></a>

`TState`

The type of the state.

Derived  
&#8627; [NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

| Methods | |
| :--- | :--- |
| [AddMany(IEnumerable&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.AddMany(IEnumerable_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.AddMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds multiple entities to the collection. |
| [AddOne(TEntity)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.AddOne(TEntity).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.AddOne(TEntity)') | Adds one entity to the collection. |
| [Map(Func&lt;TEntity,TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.Map(Func_TEntity,TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.Map(System.Func<TEntity,TEntity>)') | Updates multiple entities in the collection by defining a map function. |
| [MapOne(TKey, Func&lt;TEntity,TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.MapOne(TKey,Func_TEntity,TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.MapOne(TKey, System.Func<TEntity,TEntity>)') | Updates one entity in the collection by defining a map function. |
| [RemoveAll()](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveAll().md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveAll()') | Clears the entity collection. |
| [RemoveMany(IEnumerable&lt;TKey&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveMany(IEnumerable_TKey_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveMany(System.Collections.Generic.IEnumerable<TKey>)') | Removes multiple entities from the collection by id. |
| [RemoveMany(Func&lt;TEntity,bool&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveMany(Func_TEntity,bool_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveMany(System.Func<TEntity,bool>)') | Removes multiple entities from the collection by a predicate. |
| [RemoveOne(TKey)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveOne(TKey).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveOne(TKey)') | Removes one entity from the collection. |
| [SetAll(IEnumerable&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.SetAll(IEnumerable_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.SetAll(System.Collections.Generic.IEnumerable<TEntity>)') | Replaces the current collection with the provided collection. |
| [SetMany(IEnumerable&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.SetMany(IEnumerable_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.SetMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds or replaces multiple entities in the collection. |
| [SetOne(TEntity)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.SetOne(TEntity).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.SetOne(TEntity)') | Adds or replaces one entity in the collection. |
| [UpdateMany(IEnumerable&lt;TKey&gt;, Action&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateMany(IEnumerable_TKey_,Action_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateMany(System.Collections.Generic.IEnumerable<TKey>, System.Action<TEntity>)') | Updates multiple entities in the collection. Supports partial updates. |
| [UpdateMany(IEnumerable&lt;TKey&gt;, Func&lt;TEntity,TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateMany(IEnumerable_TKey_,Func_TEntity,TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateMany(System.Collections.Generic.IEnumerable<TKey>, System.Func<TEntity,TEntity>)') | Updates multiple entities in the collection. Supports partial updates. |
| [UpdateOne(TKey, Action&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateOne(TKey,Action_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateOne(TKey, System.Action<TEntity>)') | Updates one entity in the collection. Supports partial updates. |
| [UpdateOne(TKey, Func&lt;TEntity,TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpdateOne(TKey,Func_TEntity,TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpdateOne(TKey, System.Func<TEntity,TEntity>)') | Updates one entity in the collection. Supports partial updates. |
| [UpsertMany(IEnumerable&lt;TEntity&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpsertMany(IEnumerable_TEntity_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpsertMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds or updates multiple entities in the collection. |
| [UpsertOne(TEntity)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpsertOne(TEntity).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpsertOne(TEntity)') | Adds or updates one entity in the collection. |

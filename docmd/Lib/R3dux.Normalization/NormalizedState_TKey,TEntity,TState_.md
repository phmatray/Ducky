#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux')

## NormalizedState<TKey,TEntity,TState> Class

Represents a normalized state for collections.

```csharp
public abstract class NormalizedState<TKey,TEntity,TState> :
R3dux.INormalizedStateCollectionMethods<TKey, TEntity, TState>,
System.IEquatable<R3dux.NormalizedState<TKey, TEntity, TState>>
    where TKey : System.IEquatable<TKey>
    where TEntity : R3dux.IEntity<TKey>
    where TState : R3dux.NormalizedState<TKey, TEntity, TState>, new()
```
#### Type parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.TKey'></a>

`TKey`

The type of the entity key.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.TEntity'></a>

`TEntity`

The type of the entity value.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.TState'></a>

`TState`

The type of the state.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; NormalizedState<TKey,TEntity,TState>

Implements [R3dux.INormalizedStateCollectionMethods&lt;](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')[TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')[,](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[,](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')[&gt;](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.NormalizedState&lt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')[TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')[,](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[,](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')[&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Properties | |
| :--- | :--- |
| [AllIds](NormalizedState_TKey,TEntity,TState_.AllIds.md 'R3dux.NormalizedState<TKey,TEntity,TState>.AllIds') | Gets the list of entity IDs. |
| [ById](NormalizedState_TKey,TEntity,TState_.ById.md 'R3dux.NormalizedState<TKey,TEntity,TState>.ById') | Gets or init the dictionary of entities. |
| [this[TKey]](NormalizedState_TKey,TEntity,TState_.this[TKey].md 'R3dux.NormalizedState<TKey,TEntity,TState>.this[TKey]') | Indexer to get an entity by its key. |

| Methods | |
| :--- | :--- |
| [AddMany(IEnumerable&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.AddMany(IEnumerable_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.AddMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds multiple entities to the collection. |
| [AddOne(TEntity)](NormalizedState_TKey,TEntity,TState_.AddOne(TEntity).md 'R3dux.NormalizedState<TKey,TEntity,TState>.AddOne(TEntity)') | Adds one entity to the collection. |
| [ContainsKey(TKey)](NormalizedState_TKey,TEntity,TState_.ContainsKey(TKey).md 'R3dux.NormalizedState<TKey,TEntity,TState>.ContainsKey(TKey)') | Checks if an entity with the specified key exists in the state. |
| [Create(ImmutableList&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.Create(ImmutableList_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.Create(System.Collections.Immutable.ImmutableList<TEntity>)') | Creates a new state with the specified entities. |
| [GetByKey(TKey)](NormalizedState_TKey,TEntity,TState_.GetByKey(TKey).md 'R3dux.NormalizedState<TKey,TEntity,TState>.GetByKey(TKey)') | Gets an entity by its key. |
| [Map(Func&lt;TEntity,TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.Map(Func_TEntity,TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.Map(System.Func<TEntity,TEntity>)') | Updates multiple entities in the collection by defining a map function. |
| [MapOne(TKey, Func&lt;TEntity,TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.MapOne(TKey,Func_TEntity,TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.MapOne(TKey, System.Func<TEntity,TEntity>)') | Updates one entity in the collection by defining a map function. |
| [Merge(ImmutableDictionary&lt;TKey,TEntity&gt;, MergeStrategy)](NormalizedState_TKey,TEntity,TState_.Merge(ImmutableDictionary_TKey,TEntity_,MergeStrategy).md 'R3dux.NormalizedState<TKey,TEntity,TState>.Merge(System.Collections.Immutable.ImmutableDictionary<TKey,TEntity>, R3dux.MergeStrategy)') | Merges the specified entities into the state using the provided merge strategy. |
| [RemoveAll()](NormalizedState_TKey,TEntity,TState_.RemoveAll().md 'R3dux.NormalizedState<TKey,TEntity,TState>.RemoveAll()') | Clears the entity collection. |
| [RemoveMany(IEnumerable&lt;TKey&gt;)](NormalizedState_TKey,TEntity,TState_.RemoveMany(IEnumerable_TKey_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.RemoveMany(System.Collections.Generic.IEnumerable<TKey>)') | Removes multiple entities from the collection by id. |
| [RemoveMany(Func&lt;TEntity,bool&gt;)](NormalizedState_TKey,TEntity,TState_.RemoveMany(Func_TEntity,bool_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.RemoveMany(System.Func<TEntity,bool>)') | Removes multiple entities from the collection by a predicate. |
| [RemoveOne(TKey)](NormalizedState_TKey,TEntity,TState_.RemoveOne(TKey).md 'R3dux.NormalizedState<TKey,TEntity,TState>.RemoveOne(TKey)') | Removes one entity from the collection. |
| [SelectImmutableList()](NormalizedState_TKey,TEntity,TState_.SelectImmutableList().md 'R3dux.NormalizedState<TKey,TEntity,TState>.SelectImmutableList()') | Selects entities. |
| [SelectImmutableList(Func&lt;TEntity,bool&gt;)](NormalizedState_TKey,TEntity,TState_.SelectImmutableList(Func_TEntity,bool_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.SelectImmutableList(System.Func<TEntity,bool>)') | Selects entities based on a predicate. |
| [SetAll(IEnumerable&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.SetAll(IEnumerable_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.SetAll(System.Collections.Generic.IEnumerable<TEntity>)') | Replaces the current collection with the provided collection. |
| [SetMany(IEnumerable&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.SetMany(IEnumerable_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.SetMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds or replaces multiple entities in the collection. |
| [SetOne(TEntity)](NormalizedState_TKey,TEntity,TState_.SetOne(TEntity).md 'R3dux.NormalizedState<TKey,TEntity,TState>.SetOne(TEntity)') | Adds or replaces one entity in the collection. |
| [UpdateMany(IEnumerable&lt;TKey&gt;, Action&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.UpdateMany(IEnumerable_TKey_,Action_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpdateMany(System.Collections.Generic.IEnumerable<TKey>, System.Action<TEntity>)') | Updates multiple entities in the collection. Supports partial updates. |
| [UpdateMany(IEnumerable&lt;TKey&gt;, Func&lt;TEntity,TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.UpdateMany(IEnumerable_TKey_,Func_TEntity,TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpdateMany(System.Collections.Generic.IEnumerable<TKey>, System.Func<TEntity,TEntity>)') | Updates multiple entities in the collection. Supports partial updates. |
| [UpdateOne(TKey, Action&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.UpdateOne(TKey,Action_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpdateOne(TKey, System.Action<TEntity>)') | Updates one entity in the collection. Supports partial updates. |
| [UpdateOne(TKey, Func&lt;TEntity,TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.UpdateOne(TKey,Func_TEntity,TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpdateOne(TKey, System.Func<TEntity,TEntity>)') | Updates one entity in the collection. Supports partial updates. |
| [UpsertMany(IEnumerable&lt;TEntity&gt;)](NormalizedState_TKey,TEntity,TState_.UpsertMany(IEnumerable_TEntity_).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpsertMany(System.Collections.Generic.IEnumerable<TEntity>)') | Adds or updates multiple entities in the collection. |
| [UpsertOne(TEntity)](NormalizedState_TKey,TEntity,TState_.UpsertOne(TEntity).md 'R3dux.NormalizedState<TKey,TEntity,TState>.UpsertOne(TEntity)') | Adds or updates one entity in the collection. |

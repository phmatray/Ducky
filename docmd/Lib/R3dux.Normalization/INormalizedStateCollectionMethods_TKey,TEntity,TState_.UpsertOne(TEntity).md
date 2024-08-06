#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[INormalizedStateCollectionMethods&lt;TKey,TEntity,TState&gt;](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')

## INormalizedStateCollectionMethods<TKey,TEntity,TState>.UpsertOne(TEntity) Method

Adds or updates one entity in the collection.

```csharp
TState UpsertOne(TEntity entity);
```
#### Parameters

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.UpsertOne(TEntity).entity'></a>

`entity` [TEntity](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TEntity 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TEntity')

The entity to upsert.

#### Returns
[TState](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TState 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TState')  
The new state with the entity upserted.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The entity must not be null.
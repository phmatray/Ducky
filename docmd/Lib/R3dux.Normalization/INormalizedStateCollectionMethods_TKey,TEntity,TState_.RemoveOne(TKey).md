#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[INormalizedStateCollectionMethods&lt;TKey,TEntity,TState&gt;](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')

## INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveOne(TKey) Method

Removes one entity from the collection.

```csharp
TState RemoveOne(TKey key);
```
#### Parameters

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveOne(TKey).key'></a>

`key` [TKey](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TKey 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TKey')

The key of the entity to remove.

#### Returns
[TState](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TState 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TState')  
The new state with the entity removed.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The key must not be null.
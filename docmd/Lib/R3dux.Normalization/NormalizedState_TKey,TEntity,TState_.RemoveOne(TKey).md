#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.RemoveOne(TKey) Method

Removes one entity from the collection.

```csharp
public TState RemoveOne(TKey key);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.RemoveOne(TKey).key'></a>

`key` [TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')

The key of the entity to remove.

Implements [RemoveOne(TKey)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveOne(TKey).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveOne(TKey)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entity removed.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The key must not be null.
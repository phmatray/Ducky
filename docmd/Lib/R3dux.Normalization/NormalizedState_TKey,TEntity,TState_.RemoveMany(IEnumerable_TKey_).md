#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.RemoveMany(IEnumerable<TKey>) Method

Removes multiple entities from the collection by id.

```csharp
public TState RemoveMany(System.Collections.Generic.IEnumerable<TKey> keys);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.RemoveMany(System.Collections.Generic.IEnumerable_TKey_).keys'></a>

`keys` [System.Collections.Generic.IEnumerable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')[TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Generic.IEnumerable-1 'System.Collections.Generic.IEnumerable`1')

The keys of the entities to remove.

Implements [RemoveMany(IEnumerable&lt;TKey&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveMany(IEnumerable_TKey_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveMany(System.Collections.Generic.IEnumerable<TKey>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entities removed.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The keys collection must not be null.
#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.RemoveMany(Func<TEntity,bool>) Method

Removes multiple entities from the collection by a predicate.

```csharp
public TState RemoveMany(System.Func<TEntity,bool> predicate);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.RemoveMany(System.Func_TEntity,bool_).predicate'></a>

`predicate` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Boolean](https://docs.microsoft.com/en-us/dotnet/api/System.Boolean 'System.Boolean')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The predicate to filter entities to remove.

Implements [RemoveMany(Func&lt;TEntity,bool&gt;)](INormalizedStateCollectionMethods_TKey,TEntity,TState_.RemoveMany(Func_TEntity,bool_).md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.RemoveMany(System.Func<TEntity,bool>)')

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
The new state with the entities removed.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The predicate must not be null.
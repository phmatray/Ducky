#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.SelectImmutableList(Func<TEntity,bool>) Method

Selects entities based on a predicate.

```csharp
public System.Collections.Immutable.ImmutableList<TEntity> SelectImmutableList(System.Func<TEntity,bool> predicate);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.SelectImmutableList(System.Func_TEntity,bool_).predicate'></a>

`predicate` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Boolean](https://docs.microsoft.com/en-us/dotnet/api/System.Boolean 'System.Boolean')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The predicate to filter entities.

#### Returns
[System.Collections.Immutable.ImmutableList&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableList-1 'System.Collections.Immutable.ImmutableList`1')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableList-1 'System.Collections.Immutable.ImmutableList`1')  
An immutable list of entities that match the predicate.
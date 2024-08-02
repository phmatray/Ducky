#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.Create(ImmutableList<TEntity>) Method

Creates a new state with the specified entities.

```csharp
public static TState Create(System.Collections.Immutable.ImmutableList<TEntity> entities);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.Create(System.Collections.Immutable.ImmutableList_TEntity_).entities'></a>

`entities` [System.Collections.Immutable.ImmutableList&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableList-1 'System.Collections.Immutable.ImmutableList`1')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableList-1 'System.Collections.Immutable.ImmutableList`1')

The entities to create the state with.

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
A new state with the entities.
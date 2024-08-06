#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[NormalizedState&lt;TKey,TEntity,TState&gt;](NormalizedState_TKey,TEntity,TState_.md 'R3dux.NormalizedState<TKey,TEntity,TState>')

## NormalizedState<TKey,TEntity,TState>.Merge(ImmutableDictionary<TKey,TEntity>, MergeStrategy) Method

Merges the specified entities into the state using the provided merge strategy.

```csharp
public TState Merge(System.Collections.Immutable.ImmutableDictionary<TKey,TEntity> entities, R3dux.MergeStrategy strategy=R3dux.MergeStrategy.FailIfDuplicate);
```
#### Parameters

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.Merge(System.Collections.Immutable.ImmutableDictionary_TKey,TEntity_,R3dux.MergeStrategy).entities'></a>

`entities` [System.Collections.Immutable.ImmutableDictionary&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableDictionary-2 'System.Collections.Immutable.ImmutableDictionary`2')[TKey](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TKey 'R3dux.NormalizedState<TKey,TEntity,TState>.TKey')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableDictionary-2 'System.Collections.Immutable.ImmutableDictionary`2')[TEntity](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TEntity 'R3dux.NormalizedState<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Collections.Immutable.ImmutableDictionary-2 'System.Collections.Immutable.ImmutableDictionary`2')

The entities to merge into the state.

<a name='R3dux.NormalizedState_TKey,TEntity,TState_.Merge(System.Collections.Immutable.ImmutableDictionary_TKey,TEntity_,R3dux.MergeStrategy).strategy'></a>

`strategy` [MergeStrategy](MergeStrategy.md 'R3dux.MergeStrategy')

The strategy to use when merging entities.

#### Returns
[TState](NormalizedState_TKey,TEntity,TState_.md#R3dux.NormalizedState_TKey,TEntity,TState_.TState 'R3dux.NormalizedState<TKey,TEntity,TState>.TState')  
A new state with the entities merged.

#### Exceptions

[R3dux.R3duxException](https://docs.microsoft.com/en-us/dotnet/api/R3dux.R3duxException 'R3dux.R3duxException')  
The state must be of type TState.
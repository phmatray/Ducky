#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux').[INormalizedStateCollectionMethods&lt;TKey,TEntity,TState&gt;](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>')

## INormalizedStateCollectionMethods<TKey,TEntity,TState>.Map(Func<TEntity,TEntity>) Method

Updates multiple entities in the collection by defining a map function.

```csharp
TState Map(System.Func<TEntity,TEntity> map);
```
#### Parameters

<a name='R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.Map(System.Func_TEntity,TEntity_).map'></a>

`map` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TEntity 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TEntity')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TEntity](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TEntity 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TEntity')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

The map function to apply to the entities.

#### Returns
[TState](INormalizedStateCollectionMethods_TKey,TEntity,TState_.md#R3dux.INormalizedStateCollectionMethods_TKey,TEntity,TState_.TState 'R3dux.INormalizedStateCollectionMethods<TKey,TEntity,TState>.TState')  
The new state with the entities mapped.

#### Exceptions

[System.ArgumentNullException](https://docs.microsoft.com/en-us/dotnet/api/System.ArgumentNullException 'System.ArgumentNullException')  
The map function must not be null.
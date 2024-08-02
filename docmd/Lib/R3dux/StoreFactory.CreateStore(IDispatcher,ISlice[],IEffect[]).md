#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[StoreFactory](StoreFactory.md 'R3dux.StoreFactory')

## StoreFactory.CreateStore(IDispatcher, ISlice[], IEffect[]) Method

Creates a new instance of [R3duxStore](R3duxStore.md 'R3dux.R3duxStore').

```csharp
public static R3dux.R3duxStore CreateStore(R3dux.IDispatcher dispatcher, R3dux.ISlice[] slices, R3dux.IEffect[] effects);
```
#### Parameters

<a name='R3dux.StoreFactory.CreateStore(R3dux.IDispatcher,R3dux.ISlice[],R3dux.IEffect[]).dispatcher'></a>

`dispatcher` [R3dux.IDispatcher](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IDispatcher 'R3dux.IDispatcher')

The dispatcher to be used by the store.

<a name='R3dux.StoreFactory.CreateStore(R3dux.IDispatcher,R3dux.ISlice[],R3dux.IEffect[]).slices'></a>

`slices` [R3dux.ISlice](https://docs.microsoft.com/en-us/dotnet/api/R3dux.ISlice 'R3dux.ISlice')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The collection of slices to be added to the store.

<a name='R3dux.StoreFactory.CreateStore(R3dux.IDispatcher,R3dux.ISlice[],R3dux.IEffect[]).effects'></a>

`effects` [R3dux.IEffect](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IEffect 'R3dux.IEffect')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The collection of effects to be added to the store.

#### Returns
[R3duxStore](R3duxStore.md 'R3dux.R3duxStore')  
A new instance of [R3duxStore](R3duxStore.md 'R3dux.R3duxStore').
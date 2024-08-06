#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IStore Interface

Represents a store that manages application state and handles actions.

```csharp
internal interface IStore
```

| Properties | |
| :--- | :--- |
| [RootStateObservable](IStore.RootStateObservable.md 'R3dux.IStore.RootStateObservable') | Gets an observable stream of the root state of the application. |

| Methods | |
| :--- | :--- |
| [AddEffect(IEffect)](IStore.AddEffect(IEffect).md 'R3dux.IStore.AddEffect(R3dux.IEffect)') | Adds a single effect to the store. |
| [AddEffects(IEffect[])](IStore.AddEffects(IEffect[]).md 'R3dux.IStore.AddEffects(R3dux.IEffect[])') | Adds multiple effects to the store. |
| [AddSlice(ISlice)](IStore.AddSlice(ISlice).md 'R3dux.IStore.AddSlice(R3dux.ISlice)') | Adds a single slice to the store. |
| [AddSlices(ISlice[])](IStore.AddSlices(ISlice[]).md 'R3dux.IStore.AddSlices(R3dux.ISlice[])') | Adds multiple slices to the store. |

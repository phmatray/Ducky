#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## R3duxStore Class

Represents a store that manages application state and handles actions.

```csharp
public sealed class R3duxStore :
System.IDisposable
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; R3duxStore

Implements [System.IDisposable](https://docs.microsoft.com/en-us/dotnet/api/System.IDisposable 'System.IDisposable')

| Constructors | |
| :--- | :--- |
| [R3duxStore(IDispatcher)](R3duxStore.R3duxStore(IDispatcher).md 'R3dux.R3duxStore.R3duxStore(R3dux.IDispatcher)') | Initializes a new instance of the [R3duxStore](R3duxStore.md 'R3dux.R3duxStore') class. |

| Properties | |
| :--- | :--- |
| [RootStateObservable](R3duxStore.RootStateObservable.md 'R3dux.R3duxStore.RootStateObservable') | Gets an observable stream of the root state of the application. |

| Methods | |
| :--- | :--- |
| [AddEffect(IEffect)](R3duxStore.AddEffect(IEffect).md 'R3dux.R3duxStore.AddEffect(R3dux.IEffect)') | Adds a single effect to the store. |
| [AddEffects(IEffect[])](R3duxStore.AddEffects(IEffect[]).md 'R3dux.R3duxStore.AddEffects(R3dux.IEffect[])') | Adds multiple effects to the store. |
| [AddSlice(ISlice)](R3duxStore.AddSlice(ISlice).md 'R3dux.R3duxStore.AddSlice(R3dux.ISlice)') | Adds a single slice to the store. |
| [AddSlices(ISlice[])](R3duxStore.AddSlices(ISlice[]).md 'R3dux.R3duxStore.AddSlices(R3dux.ISlice[])') | Adds multiple slices to the store. |

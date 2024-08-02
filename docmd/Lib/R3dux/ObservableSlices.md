#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## ObservableSlices Class

Manages a collection of observable slices and provides an observable root state.

```csharp
public sealed class ObservableSlices :
System.IDisposable
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; ObservableSlices

Implements [System.IDisposable](https://docs.microsoft.com/en-us/dotnet/api/System.IDisposable 'System.IDisposable')

| Constructors | |
| :--- | :--- |
| [ObservableSlices()](ObservableSlices.ObservableSlices().md 'R3dux.ObservableSlices.ObservableSlices()') | Initializes a new instance of the [ObservableSlices](ObservableSlices.md 'R3dux.ObservableSlices') class. |

| Properties | |
| :--- | :--- |
| [RootStateObservable](ObservableSlices.RootStateObservable.md 'R3dux.ObservableSlices.RootStateObservable') | Gets an observable that emits the root state whenever a slice is added, removed, or replaced. |

| Methods | |
| :--- | :--- |
| [AddSlice(ISlice)](ObservableSlices.AddSlice(ISlice).md 'R3dux.ObservableSlices.AddSlice(R3dux.ISlice)') | Adds a new slice with the specified key and data. |
| [CreateRootState()](ObservableSlices.CreateRootState().md 'R3dux.ObservableSlices.CreateRootState()') | Creates a new root state based on the current slices. |
| [UpdateRootState()](ObservableSlices.UpdateRootState().md 'R3dux.ObservableSlices.UpdateRootState()') | Updates the root state. |

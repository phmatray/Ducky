#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[ObservableExtensions](ObservableExtensions.md 'R3dux.ObservableExtensions')

## ObservableExtensions.FirstSync<T>(this Observable<T>) Method

Returns the first element of an observable sequence synchronously.

```csharp
public static T FirstSync<T>(this R3.Observable<T> observable);
```
#### Type parameters

<a name='R3dux.ObservableExtensions.FirstSync_T_(thisR3.Observable_T_).T'></a>

`T`

The type of the elements in the sequence.
#### Parameters

<a name='R3dux.ObservableExtensions.FirstSync_T_(thisR3.Observable_T_).observable'></a>

`observable` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[T](ObservableExtensions.FirstSync_T_(thisObservable_T_).md#R3dux.ObservableExtensions.FirstSync_T_(thisR3.Observable_T_).T 'R3dux.ObservableExtensions.FirstSync<T>(this R3.Observable<T>).T')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The observable sequence to return the first element of.

#### Returns
[T](ObservableExtensions.FirstSync_T_(thisObservable_T_).md#R3dux.ObservableExtensions.FirstSync_T_(thisR3.Observable_T_).T 'R3dux.ObservableExtensions.FirstSync<T>(this R3.Observable<T>).T')  
The first element of the observable sequence.
#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.OfType<TAction>(this Observable<IAction>) Method

Filters the observable sequence to include only elements of a specified type.

```csharp
public static R3.Observable<TAction> OfType<TAction>(this R3.Observable<R3dux.IAction> source)
    where TAction : R3dux.IAction;
```
#### Type parameters

<a name='R3dux.CustomOperators.OfType_TAction_(thisR3.Observable_R3dux.IAction_).TAction'></a>

`TAction`

The type of elements to filter.
#### Parameters

<a name='R3dux.CustomOperators.OfType_TAction_(thisR3.Observable_R3dux.IAction_).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TAction](CustomOperators.OfType_TAction_(thisObservable_IAction_).md#R3dux.CustomOperators.OfType_TAction_(thisR3.Observable_R3dux.IAction_).TAction 'R3dux.CustomOperators.OfType<TAction>(this R3.Observable<R3dux.IAction>).TAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence that contains elements from the input sequence of type TAction.
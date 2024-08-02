#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.SelectAction<TSource,TResult>(this Observable<TSource>, Func<TSource,TResult>) Method

Projects each element of an observable sequence into a new form and casts it to IAction.

```csharp
public static R3.Observable<R3dux.IAction> SelectAction<TSource,TResult>(this R3.Observable<TSource> source, System.Func<TSource,TResult> selector);
```
#### Type parameters

<a name='R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).TSource'></a>

`TSource`

The type of the source elements.

<a name='R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).TResult'></a>

`TResult`

The type of the result elements, which must implement IAction.
#### Parameters

<a name='R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TSource](CustomOperators.SelectAction_TSource,TResult_(thisObservable_TSource_,Func_TSource,TResult_).md#R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).TSource 'R3dux.CustomOperators.SelectAction<TSource,TResult>(this R3.Observable<TSource>, System.Func<TSource,TResult>).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).selector'></a>

`selector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TSource](CustomOperators.SelectAction_TSource,TResult_(thisObservable_TSource_,Func_TSource,TResult_).md#R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).TSource 'R3dux.CustomOperators.SelectAction<TSource,TResult>(this R3.Observable<TSource>, System.Func<TSource,TResult>).TSource')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TResult](CustomOperators.SelectAction_TSource,TResult_(thisObservable_TSource_,Func_TSource,TResult_).md#R3dux.CustomOperators.SelectAction_TSource,TResult_(thisR3.Observable_TSource_,System.Func_TSource,TResult_).TResult 'R3dux.CustomOperators.SelectAction<TSource,TResult>(this R3.Observable<TSource>, System.Func<TSource,TResult>).TResult')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A transform function to apply to each element.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence of IAction.
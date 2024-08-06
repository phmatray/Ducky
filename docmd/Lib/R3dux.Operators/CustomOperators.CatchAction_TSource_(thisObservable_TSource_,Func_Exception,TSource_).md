#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.CatchAction<TSource>(this Observable<TSource>, Func<Exception,TSource>) Method

Catches exceptions in the observable sequence and replaces the exception with a specified value.

```csharp
public static R3.Observable<TSource> CatchAction<TSource>(this R3.Observable<TSource> source, System.Func<System.Exception,TSource> selector);
```
#### Type parameters

<a name='R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).TSource'></a>

`TSource`

The type of the source elements.
#### Parameters

<a name='R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TSource](CustomOperators.CatchAction_TSource_(thisObservable_TSource_,Func_Exception,TSource_).md#R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).TSource 'R3dux.CustomOperators.CatchAction<TSource>(this R3.Observable<TSource>, System.Func<System.Exception,TSource>).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).selector'></a>

`selector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[System.Exception](https://docs.microsoft.com/en-us/dotnet/api/System.Exception 'System.Exception')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TSource](CustomOperators.CatchAction_TSource_(thisObservable_TSource_,Func_Exception,TSource_).md#R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).TSource 'R3dux.CustomOperators.CatchAction<TSource>(this R3.Observable<TSource>, System.Func<System.Exception,TSource>).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A transform function to apply to each exception.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TSource](CustomOperators.CatchAction_TSource_(thisObservable_TSource_,Func_Exception,TSource_).md#R3dux.CustomOperators.CatchAction_TSource_(thisR3.Observable_TSource_,System.Func_System.Exception,TSource_).TSource 'R3dux.CustomOperators.CatchAction<TSource>(this R3.Observable<TSource>, System.Func<System.Exception,TSource>).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence containing the source elements and replacing exceptions with the result of the selector function.
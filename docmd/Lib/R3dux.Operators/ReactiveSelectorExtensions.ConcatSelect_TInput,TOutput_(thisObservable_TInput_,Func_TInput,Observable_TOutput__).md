#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[ReactiveSelectorExtensions](ReactiveSelectorExtensions.md 'R3dux.ReactiveSelectorExtensions')

## ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this Observable<TInput>, Func<TInput,Observable<TOutput>>) Method

Projects each element of an observable sequence into a new observable sequence and concatenates the resulting observable sequences.

```csharp
public static R3.Observable<TOutput> ConcatSelect<TInput,TOutput>(this R3.Observable<TInput> source, System.Func<TInput,R3.Observable<TOutput>> selector);
```
#### Type parameters

<a name='R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TInput'></a>

`TInput`

The type of elements in the source observable sequence.

<a name='R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TOutput'></a>

`TOutput`

The type of elements in the projected observable sequences.
#### Parameters

<a name='R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TInput](ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md#R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TInput 'R3dux.ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>).TInput')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).selector'></a>

`selector` [System.Func&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[TInput](ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md#R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TInput 'R3dux.ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>).TInput')[,](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TOutput](ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md#R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TOutput 'R3dux.ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>).TOutput')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Func-2 'System.Func`2')

A transform function to apply to each element in the input sequence.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TOutput](ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md#R3dux.ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisR3.Observable_TInput_,System.Func_TInput,R3.Observable_TOutput__).TOutput 'R3dux.ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>).TOutput')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence whose elements are the result of invoking the transform function on each element of the source and concatenating the resulting sequences.
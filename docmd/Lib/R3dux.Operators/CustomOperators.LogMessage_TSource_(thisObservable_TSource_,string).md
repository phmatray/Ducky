#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.LogMessage<TSource>(this Observable<TSource>, string) Method

Logs a message to the console for each element in the observable sequence.

```csharp
public static R3.Observable<TSource> LogMessage<TSource>(this R3.Observable<TSource> source, string message);
```
#### Type parameters

<a name='R3dux.CustomOperators.LogMessage_TSource_(thisR3.Observable_TSource_,string).TSource'></a>

`TSource`

The type of the source elements.
#### Parameters

<a name='R3dux.CustomOperators.LogMessage_TSource_(thisR3.Observable_TSource_,string).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TSource](CustomOperators.LogMessage_TSource_(thisObservable_TSource_,string).md#R3dux.CustomOperators.LogMessage_TSource_(thisR3.Observable_TSource_,string).TSource 'R3dux.CustomOperators.LogMessage<TSource>(this R3.Observable<TSource>, string).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.CustomOperators.LogMessage_TSource_(thisR3.Observable_TSource_,string).message'></a>

`message` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The message to log to the console.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TSource](CustomOperators.LogMessage_TSource_(thisObservable_TSource_,string).md#R3dux.CustomOperators.LogMessage_TSource_(thisR3.Observable_TSource_,string).TSource 'R3dux.CustomOperators.LogMessage<TSource>(this R3.Observable<TSource>, string).TSource')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
The source sequence with added side-effects of logging each element.
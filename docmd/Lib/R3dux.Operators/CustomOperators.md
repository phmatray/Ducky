#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux')

## CustomOperators Class

Provides custom operators for working with observable sequences in the R3dux application.

```csharp
public static class CustomOperators
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; CustomOperators

| Methods | |
| :--- | :--- |
| [CatchAction&lt;TSource&gt;(this Observable&lt;TSource&gt;, Func&lt;Exception,TSource&gt;)](CustomOperators.CatchAction_TSource_(thisObservable_TSource_,Func_Exception,TSource_).md 'R3dux.CustomOperators.CatchAction<TSource>(this R3.Observable<TSource>, System.Func<System.Exception,TSource>)') | Catches exceptions in the observable sequence and replaces the exception with a specified value. |
| [InvokeService&lt;TAction,TResult&gt;(this Observable&lt;TAction&gt;, Func&lt;TAction,ValueTask&lt;TResult&gt;&gt;, Func&lt;TResult,IAction&gt;, Func&lt;Exception,IAction&gt;)](CustomOperators.InvokeService_TAction,TResult_(thisObservable_TAction_,Func_TAction,ValueTask_TResult__,Func_TResult,IAction_,Func_Exception,IAction_).md 'R3dux.CustomOperators.InvokeService<TAction,TResult>(this R3.Observable<TAction>, System.Func<TAction,System.Threading.Tasks.ValueTask<TResult>>, System.Func<TResult,R3dux.IAction>, System.Func<System.Exception,R3dux.IAction>)') | Invokes a service call for each element in the observable sequence, returning a sequence of actions based on the result or error. |
| [LogMessage&lt;TSource&gt;(this Observable&lt;TSource&gt;, string)](CustomOperators.LogMessage_TSource_(thisObservable_TSource_,string).md 'R3dux.CustomOperators.LogMessage<TSource>(this R3.Observable<TSource>, string)') | Logs a message to the console for each element in the observable sequence. |
| [OfType&lt;TAction&gt;(this Observable&lt;IAction&gt;)](CustomOperators.OfType_TAction_(thisObservable_IAction_).md 'R3dux.CustomOperators.OfType<TAction>(this R3.Observable<R3dux.IAction>)') | Filters the observable sequence to include only elements of a specified type. |
| [SelectAction&lt;TSource,TResult&gt;(this Observable&lt;TSource&gt;, Func&lt;TSource,TResult&gt;)](CustomOperators.SelectAction_TSource,TResult_(thisObservable_TSource_,Func_TSource,TResult_).md 'R3dux.CustomOperators.SelectAction<TSource,TResult>(this R3.Observable<TSource>, System.Func<TSource,TResult>)') | Projects each element of an observable sequence into a new form and casts it to IAction. |
| [WithSliceState&lt;TState,TAction&gt;(this Observable&lt;TAction&gt;, Observable&lt;IRootState&gt;, string)](CustomOperators.WithSliceState_TState,TAction_(thisObservable_TAction_,Observable_IRootState_,string).md 'R3dux.CustomOperators.WithSliceState<TState,TAction>(this R3.Observable<TAction>, R3.Observable<R3dux.IRootState>, string)') | Combines the observable sequence with the state of a slice and projects the result into a new form. |

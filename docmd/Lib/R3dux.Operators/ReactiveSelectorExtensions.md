#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux')

## ReactiveSelectorExtensions Class

Provides extension methods for working with observables.

```csharp
public static class ReactiveSelectorExtensions
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; ReactiveSelectorExtensions

| Methods | |
| :--- | :--- |
| [ConcatSelect&lt;TInput,TOutput&gt;(this Observable&lt;TInput&gt;, Func&lt;TInput,Observable&lt;TOutput&gt;&gt;)](ReactiveSelectorExtensions.ConcatSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md 'R3dux.ReactiveSelectorExtensions.ConcatSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>)') | Projects each element of an observable sequence into a new observable sequence and concatenates the resulting observable sequences. |
| [MergeSelect&lt;TInput,TOutput&gt;(this Observable&lt;TInput&gt;, Func&lt;TInput,Observable&lt;TOutput&gt;&gt;)](ReactiveSelectorExtensions.MergeSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md 'R3dux.ReactiveSelectorExtensions.MergeSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>)') | Projects each element of an observable sequence into a new observable sequence and merges the resulting observable sequences. |
| [SwitchSelect&lt;TInput,TOutput&gt;(this Observable&lt;TInput&gt;, Func&lt;TInput,Observable&lt;TOutput&gt;&gt;)](ReactiveSelectorExtensions.SwitchSelect_TInput,TOutput_(thisObservable_TInput_,Func_TInput,Observable_TOutput__).md 'R3dux.ReactiveSelectorExtensions.SwitchSelect<TInput,TOutput>(this R3.Observable<TInput>, System.Func<TInput,R3.Observable<TOutput>>)') | Projects each element of an observable sequence into a new observable sequence and then switches to the latest observable sequence. |

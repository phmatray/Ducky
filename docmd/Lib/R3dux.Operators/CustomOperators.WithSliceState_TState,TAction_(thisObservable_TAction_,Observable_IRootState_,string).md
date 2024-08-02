#### [R3dux.Operators](R3dux.Operators.md 'R3dux.Operators')
### [R3dux](R3dux.Operators.md#R3dux 'R3dux').[CustomOperators](CustomOperators.md 'R3dux.CustomOperators')

## CustomOperators.WithSliceState<TState,TAction>(this Observable<TAction>, Observable<IRootState>, string) Method

Combines the observable sequence with the state of a slice and projects the result into a new form.

```csharp
public static R3.Observable<R3dux.StateActionPair<TState,TAction>> WithSliceState<TState,TAction>(this R3.Observable<TAction> source, R3.Observable<R3dux.IRootState> rootStateObs, string? sliceKey=null)
    where TState : notnull
    where TAction : R3dux.IAction;
```
#### Type parameters

<a name='R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).TState'></a>

`TState`

The type of the state.

<a name='R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).TAction'></a>

`TAction`

The type of the action.
#### Parameters

<a name='R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).source'></a>

`source` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[TAction](CustomOperators.WithSliceState_TState,TAction_(thisObservable_TAction_,Observable_IRootState_,string).md#R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).TAction 'R3dux.CustomOperators.WithSliceState<TState,TAction>(this R3.Observable<TAction>, R3.Observable<R3dux.IRootState>, string).TAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence.

<a name='R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).rootStateObs'></a>

`rootStateObs` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The observable sequence of the root state.

<a name='R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).sliceKey'></a>

`sliceKey` [System.String](https://docs.microsoft.com/en-us/dotnet/api/System.String 'System.String')

The key of the slice to select.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.StateActionPair&lt;](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[TState](CustomOperators.WithSliceState_TState,TAction_(thisObservable_TAction_,Observable_IRootState_,string).md#R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).TState 'R3dux.CustomOperators.WithSliceState<TState,TAction>(this R3.Observable<TAction>, R3.Observable<R3dux.IRootState>, string).TState')[,](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[TAction](CustomOperators.WithSliceState_TState,TAction_(thisObservable_TAction_,Observable_IRootState_,string).md#R3dux.CustomOperators.WithSliceState_TState,TAction_(thisR3.Observable_TAction_,R3.Observable_R3dux.IRootState_,string).TAction 'R3dux.CustomOperators.WithSliceState<TState,TAction>(this R3.Observable<TAction>, R3.Observable<R3dux.IRootState>, string).TAction')[&gt;](StateActionPair_TState,TAction_.md 'R3dux.StateActionPair<TState,TAction>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence of StateActionPair.
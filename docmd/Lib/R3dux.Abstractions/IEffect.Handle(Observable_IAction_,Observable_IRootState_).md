#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux').[IEffect](IEffect.md 'R3dux.IEffect')

## IEffect.Handle(Observable<IAction>, Observable<IRootState>) Method

Handles a stream of actions and produces a stream of resulting actions.

```csharp
R3.Observable<R3dux.IAction> Handle(R3.Observable<R3dux.IAction> actions, R3.Observable<R3dux.IRootState> rootState);
```
#### Parameters

<a name='R3dux.IEffect.Handle(R3.Observable_R3dux.IAction_,R3.Observable_R3dux.IRootState_).actions'></a>

`actions` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[IAction](IAction.md 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence of actions.

<a name='R3dux.IEffect.Handle(R3.Observable_R3dux.IAction_,R3.Observable_R3dux.IRootState_).rootState'></a>

`rootState` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[IRootState](IRootState.md 'R3dux.IRootState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence of the root state.

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[IAction](IAction.md 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence of resulting actions.
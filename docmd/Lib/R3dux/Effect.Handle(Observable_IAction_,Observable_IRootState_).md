#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[Effect](Effect.md 'R3dux.Effect')

## Effect.Handle(Observable<IAction>, Observable<IRootState>) Method

Handles a stream of actions and produces a stream of resulting actions.

```csharp
public virtual R3.Observable<R3dux.IAction> Handle(R3.Observable<R3dux.IAction> actions, R3.Observable<R3dux.IRootState> rootState);
```
#### Parameters

<a name='R3dux.Effect.Handle(R3.Observable_R3dux.IAction_,R3.Observable_R3dux.IRootState_).actions'></a>

`actions` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence of actions.

<a name='R3dux.Effect.Handle(R3.Observable_R3dux.IAction_,R3.Observable_R3dux.IRootState_).rootState'></a>

`rootState` [R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IRootState](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IRootState 'R3dux.IRootState')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')

The source observable sequence of the root state.

Implements [Handle(Observable&lt;IAction&gt;, Observable&lt;IRootState&gt;)](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IEffect.Handle#R3dux_IEffect_Handle_R3_Observable{R3dux_IAction},R3_Observable{R3dux_IRootState}_ 'R3dux.IEffect.Handle(R3.Observable{R3dux.IAction},R3.Observable{R3dux.IRootState})')

#### Returns
[R3.Observable&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')[R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3.Observable-1 'R3.Observable`1')  
An observable sequence of resulting actions.
#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IEffect Interface

Represents an effect that handles a stream of actions and interacts with the store's state.

```csharp
public interface IEffect
```

| Methods | |
| :--- | :--- |
| [GetAssemblyName()](IEffect.GetAssemblyName().md 'R3dux.IEffect.GetAssemblyName()') | Gets the assembly-qualified name of the effect. |
| [GetKey()](IEffect.GetKey().md 'R3dux.IEffect.GetKey()') | Gets the key that identifies the effect. |
| [Handle(Observable&lt;IAction&gt;, Observable&lt;IRootState&gt;)](IEffect.Handle(Observable_IAction_,Observable_IRootState_).md 'R3dux.IEffect.Handle(R3.Observable<R3dux.IAction>, R3.Observable<R3dux.IRootState>)') | Handles a stream of actions and produces a stream of resulting actions. |

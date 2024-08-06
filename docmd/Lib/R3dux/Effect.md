#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## Effect Class

Represents an effect that handles a stream of actions and interacts with the store's state.

```csharp
public abstract class Effect :
R3dux.IEffect
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; Effect

Implements [R3dux.IEffect](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IEffect 'R3dux.IEffect')

| Properties | |
| :--- | :--- |
| [TimeProvider](Effect.TimeProvider.md 'R3dux.Effect.TimeProvider') | Gets or init the time provider used to provide the current time. |

| Methods | |
| :--- | :--- |
| [GetAssemblyName()](Effect.GetAssemblyName().md 'R3dux.Effect.GetAssemblyName()') | Gets the assembly-qualified name of the effect. |
| [GetKey()](Effect.GetKey().md 'R3dux.Effect.GetKey()') | Gets the key that identifies the effect. |
| [Handle(Observable&lt;IAction&gt;, Observable&lt;IRootState&gt;)](Effect.Handle(Observable_IAction_,Observable_IRootState_).md 'R3dux.Effect.Handle(R3.Observable<R3dux.IAction>, R3.Observable<R3dux.IRootState>)') | Handles a stream of actions and produces a stream of resulting actions. |

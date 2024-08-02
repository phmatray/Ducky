#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## Fsa Class

A Flux Standard action without payload or metadata properties.

```csharp
public abstract class Fsa :
R3dux.IKeyedAction,
R3dux.IAction,
System.IEquatable<R3dux.Fsa>
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; Fsa

Derived  
&#8627; [Fsa&lt;TPayload&gt;](Fsa_TPayload_.md 'R3dux.Fsa<TPayload>')  
&#8627; [FsaError](FsaError.md 'R3dux.FsaError')  
&#8627; [FsaMeta&lt;TMeta&gt;](FsaMeta_TMeta_.md 'R3dux.FsaMeta<TMeta>')

Implements [R3dux.IKeyedAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IKeyedAction 'R3dux.IKeyedAction'), [R3dux.IAction](https://docs.microsoft.com/en-us/dotnet/api/R3dux.IAction 'R3dux.IAction'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[Fsa](Fsa.md 'R3dux.Fsa')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Properties | |
| :--- | :--- |
| [TypeKey](Fsa.TypeKey.md 'R3dux.Fsa.TypeKey') | Gets the `type` of an action identifies to the consumer the nature of the action that has occurred. |

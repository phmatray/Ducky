#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## FsaError Class

A Flux Standard action with an error property.

```csharp
public abstract class FsaError : R3dux.Fsa,
R3dux.IFsaPayload<System.Exception>,
System.IEquatable<R3dux.FsaError>
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Fsa](Fsa.md 'R3dux.Fsa') &#129106; FsaError

Derived  
&#8627; [FsaError&lt;TMeta&gt;](FsaError_TMeta_.md 'R3dux.FsaError<TMeta>')

Implements [R3dux.IFsaPayload&lt;](IFsaPayload_TPayload_.md 'R3dux.IFsaPayload<TPayload>')[System.Exception](https://docs.microsoft.com/en-us/dotnet/api/System.Exception 'System.Exception')[&gt;](IFsaPayload_TPayload_.md 'R3dux.IFsaPayload<TPayload>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[FsaError](FsaError.md 'R3dux.FsaError')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [FsaError(Exception)](FsaError.FsaError(Exception).md 'R3dux.FsaError.FsaError(System.Exception)') | A Flux Standard action with an error property. |

| Properties | |
| :--- | :--- |
| [Error](FsaError.Error.md 'R3dux.FsaError.Error') | Gets a value indicating whether the action is an error. |
| [Payload](FsaError.Payload.md 'R3dux.FsaError.Payload') | Gets the error payload of the action. |

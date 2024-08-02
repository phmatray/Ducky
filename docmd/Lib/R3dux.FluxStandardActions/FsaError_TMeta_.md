#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## FsaError<TMeta> Class

A Flux Standard action with an error property and metadata properties.

```csharp
public abstract class FsaError<TMeta> : R3dux.FsaError,
R3dux.IFsaMeta<TMeta>,
System.IEquatable<R3dux.FsaError<TMeta>>
```
#### Type parameters

<a name='R3dux.FsaError_TMeta_.TMeta'></a>

`TMeta`

The type of the metadata.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Fsa](Fsa.md 'R3dux.Fsa') &#129106; [FsaError](FsaError.md 'R3dux.FsaError') &#129106; FsaError<TMeta>

Implements [R3dux.IFsaMeta&lt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>')[TMeta](FsaError_TMeta_.md#R3dux.FsaError_TMeta_.TMeta 'R3dux.FsaError<TMeta>.TMeta')[&gt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.FsaError&lt;](FsaError_TMeta_.md 'R3dux.FsaError<TMeta>')[TMeta](FsaError_TMeta_.md#R3dux.FsaError_TMeta_.TMeta 'R3dux.FsaError<TMeta>.TMeta')[&gt;](FsaError_TMeta_.md 'R3dux.FsaError<TMeta>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [FsaError(Exception, TMeta)](FsaError_TMeta_.FsaError(Exception,TMeta).md 'R3dux.FsaError<TMeta>.FsaError(System.Exception, TMeta)') | A Flux Standard action with an error property and metadata properties. |

| Properties | |
| :--- | :--- |
| [Meta](FsaError_TMeta_.Meta.md 'R3dux.FsaError<TMeta>.Meta') | Gets the optional `meta` property MAY be any type of value. |

#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## FsaMeta<TMeta> Class

A Flux Standard action with metadata properties.

```csharp
public abstract class FsaMeta<TMeta> : R3dux.Fsa,
R3dux.IFsaMeta<TMeta>,
System.IEquatable<R3dux.FsaMeta<TMeta>>
```
#### Type parameters

<a name='R3dux.FsaMeta_TMeta_.TMeta'></a>

`TMeta`

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Fsa](Fsa.md 'R3dux.Fsa') &#129106; FsaMeta<TMeta>

Implements [R3dux.IFsaMeta&lt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>')[TMeta](FsaMeta_TMeta_.md#R3dux.FsaMeta_TMeta_.TMeta 'R3dux.FsaMeta<TMeta>.TMeta')[&gt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.FsaMeta&lt;](FsaMeta_TMeta_.md 'R3dux.FsaMeta<TMeta>')[TMeta](FsaMeta_TMeta_.md#R3dux.FsaMeta_TMeta_.TMeta 'R3dux.FsaMeta<TMeta>.TMeta')[&gt;](FsaMeta_TMeta_.md 'R3dux.FsaMeta<TMeta>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [FsaMeta(TMeta)](FsaMeta_TMeta_.FsaMeta(TMeta).md 'R3dux.FsaMeta<TMeta>.FsaMeta(TMeta)') | A Flux Standard action with metadata properties. |

| Properties | |
| :--- | :--- |
| [Meta](FsaMeta_TMeta_.Meta.md 'R3dux.FsaMeta<TMeta>.Meta') | Gets the optional `meta` property MAY be any type of value. |

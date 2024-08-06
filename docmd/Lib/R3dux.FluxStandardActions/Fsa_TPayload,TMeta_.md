#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## Fsa<TPayload,TMeta> Class

A Flux Standard action with a generic payload and metadata type.

```csharp
public abstract class Fsa<TPayload,TMeta> : R3dux.Fsa<TPayload>,
R3dux.IFsaMeta<TMeta>,
System.IEquatable<R3dux.Fsa<TPayload, TMeta>>
```
#### Type parameters

<a name='R3dux.Fsa_TPayload,TMeta_.TPayload'></a>

`TPayload`

The type of the payload.

<a name='R3dux.Fsa_TPayload,TMeta_.TMeta'></a>

`TMeta`

The type of the metadata.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Fsa](Fsa.md 'R3dux.Fsa') &#129106; [R3dux.Fsa&lt;](Fsa_TPayload_.md 'R3dux.Fsa<TPayload>')[TPayload](Fsa_TPayload,TMeta_.md#R3dux.Fsa_TPayload,TMeta_.TPayload 'R3dux.Fsa<TPayload,TMeta>.TPayload')[&gt;](Fsa_TPayload_.md 'R3dux.Fsa<TPayload>') &#129106; Fsa<TPayload,TMeta>

Implements [R3dux.IFsaMeta&lt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>')[TMeta](Fsa_TPayload,TMeta_.md#R3dux.Fsa_TPayload,TMeta_.TMeta 'R3dux.Fsa<TPayload,TMeta>.TMeta')[&gt;](IFsaMeta_TMeta_.md 'R3dux.IFsaMeta<TMeta>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.Fsa&lt;](Fsa_TPayload,TMeta_.md 'R3dux.Fsa<TPayload,TMeta>')[TPayload](Fsa_TPayload,TMeta_.md#R3dux.Fsa_TPayload,TMeta_.TPayload 'R3dux.Fsa<TPayload,TMeta>.TPayload')[,](Fsa_TPayload,TMeta_.md 'R3dux.Fsa<TPayload,TMeta>')[TMeta](Fsa_TPayload,TMeta_.md#R3dux.Fsa_TPayload,TMeta_.TMeta 'R3dux.Fsa<TPayload,TMeta>.TMeta')[&gt;](Fsa_TPayload,TMeta_.md 'R3dux.Fsa<TPayload,TMeta>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [Fsa(TPayload, TMeta)](Fsa_TPayload,TMeta_.Fsa(TPayload,TMeta).md 'R3dux.Fsa<TPayload,TMeta>.Fsa(TPayload, TMeta)') | A Flux Standard action with a generic payload and metadata type. |

| Properties | |
| :--- | :--- |
| [Meta](Fsa_TPayload,TMeta_.Meta.md 'R3dux.Fsa<TPayload,TMeta>.Meta') | Gets the optional `meta` property MAY be any type of value. |

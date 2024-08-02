#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## IFsaMeta<TMeta> Interface

Represents an action with a metadata property.

```csharp
public interface IFsaMeta<TMeta>
```
#### Type parameters

<a name='R3dux.IFsaMeta_TMeta_.TMeta'></a>

`TMeta`

The type of the metadata.

Derived  
&#8627; [Fsa&lt;TPayload,TMeta&gt;](Fsa_TPayload,TMeta_.md 'R3dux.Fsa<TPayload,TMeta>')  
&#8627; [FsaError&lt;TMeta&gt;](FsaError_TMeta_.md 'R3dux.FsaError<TMeta>')  
&#8627; [FsaMeta&lt;TMeta&gt;](FsaMeta_TMeta_.md 'R3dux.FsaMeta<TMeta>')

| Properties | |
| :--- | :--- |
| [Meta](IFsaMeta_TMeta_.Meta.md 'R3dux.IFsaMeta<TMeta>.Meta') | Gets the optional `meta` property MAY be any type of value. |

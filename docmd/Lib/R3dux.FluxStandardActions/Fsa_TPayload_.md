#### [R3dux.FluxStandardActions](R3dux.FluxStandardActions.md 'R3dux.FluxStandardActions')
### [R3dux](R3dux.FluxStandardActions.md#R3dux 'R3dux')

## Fsa<TPayload> Class

A Flux Standard action with a generic payload type.

```csharp
public abstract class Fsa<TPayload> : R3dux.Fsa,
R3dux.IFsaPayload<TPayload>,
System.IEquatable<R3dux.Fsa<TPayload>>
```
#### Type parameters

<a name='R3dux.Fsa_TPayload_.TPayload'></a>

`TPayload`

The type of the payload.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Fsa](Fsa.md 'R3dux.Fsa') &#129106; Fsa<TPayload>

Derived  
&#8627; [Fsa&lt;TPayload,TMeta&gt;](Fsa_TPayload,TMeta_.md 'R3dux.Fsa<TPayload,TMeta>')

Implements [R3dux.IFsaPayload&lt;](IFsaPayload_TPayload_.md 'R3dux.IFsaPayload<TPayload>')[TPayload](Fsa_TPayload_.md#R3dux.Fsa_TPayload_.TPayload 'R3dux.Fsa<TPayload>.TPayload')[&gt;](IFsaPayload_TPayload_.md 'R3dux.IFsaPayload<TPayload>'), [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[R3dux.Fsa&lt;](Fsa_TPayload_.md 'R3dux.Fsa<TPayload>')[TPayload](Fsa_TPayload_.md#R3dux.Fsa_TPayload_.TPayload 'R3dux.Fsa<TPayload>.TPayload')[&gt;](Fsa_TPayload_.md 'R3dux.Fsa<TPayload>')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [Fsa(TPayload)](Fsa_TPayload_.Fsa(TPayload).md 'R3dux.Fsa<TPayload>.Fsa(TPayload)') | A Flux Standard action with a generic payload type. |

| Properties | |
| :--- | :--- |
| [Payload](Fsa_TPayload_.Payload.md 'R3dux.Fsa<TPayload>.Payload') | Gets the optional `payload` property MAY be any type of value. |

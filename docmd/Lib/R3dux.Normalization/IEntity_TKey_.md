#### [R3dux.Normalization](R3dux.Normalization.md 'R3dux.Normalization')
### [R3dux](R3dux.Normalization.md#R3dux 'R3dux')

## IEntity<TKey> Interface

Represents an entity with an identifier.

```csharp
public interface IEntity<out TKey>
    where TKey : notnull
```
#### Type parameters

<a name='R3dux.IEntity_TKey_.TKey'></a>

`TKey`

The type of the entity's key.

| Properties | |
| :--- | :--- |
| [Id](IEntity_TKey_.Id.md 'R3dux.IEntity<TKey>.Id') | Gets the identifier of the entity. |

#### [R3dux.Abstractions](R3dux.Abstractions.md 'R3dux.Abstractions')
### [R3dux](R3dux.Abstractions.md#R3dux 'R3dux')

## IKeyedAction Interface

Defines a contract for an action with a `type` key.

```csharp
public interface IKeyedAction :
R3dux.IAction
```

Implements [IAction](IAction.md 'R3dux.IAction')

| Properties | |
| :--- | :--- |
| [TypeKey](IKeyedAction.TypeKey.md 'R3dux.IKeyedAction.TypeKey') | Gets the `type` of an action identifies to the consumer the nature of the action that has occurred. |

#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor.Router.Actions](R3dux.Blazor.md#R3dux.Blazor.Router.Actions 'R3dux.Blazor.Router.Actions')

## RouterRequestAction Class

An action dispatched when a router navigation request is fired.

```csharp
public sealed class RouterRequestAction : R3dux.Fsa<R3dux.Blazor.Router.Actions.RouterRequestAction.ActionPayload>,
System.IEquatable<R3dux.Blazor.Router.Actions.RouterRequestAction>
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [R3dux.Fsa](https://docs.microsoft.com/en-us/dotnet/api/R3dux.Fsa 'R3dux.Fsa') &#129106; [R3dux.Fsa&lt;](https://docs.microsoft.com/en-us/dotnet/api/R3dux.Fsa-1 'R3dux.Fsa`1')[ActionPayload](RouterRequestAction.ActionPayload.md 'R3dux.Blazor.Router.Actions.RouterRequestAction.ActionPayload')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/R3dux.Fsa-1 'R3dux.Fsa`1') &#129106; RouterRequestAction

Implements [System.IEquatable&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')[RouterRequestAction](RouterRequestAction.md 'R3dux.Blazor.Router.Actions.RouterRequestAction')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.IEquatable-1 'System.IEquatable`1')

| Constructors | |
| :--- | :--- |
| [RouterRequestAction(ActionPayload)](RouterRequestAction.RouterRequestAction(ActionPayload).md 'R3dux.Blazor.Router.Actions.RouterRequestAction.RouterRequestAction(R3dux.Blazor.Router.Actions.RouterRequestAction.ActionPayload)') | An action dispatched when a router navigation request is fired. |

| Properties | |
| :--- | :--- |
| [TypeKey](RouterRequestAction.TypeKey.md 'R3dux.Blazor.Router.Actions.RouterRequestAction.TypeKey') | Gets the `type` of an action identifies to the consumer the nature of the action that has occurred. |

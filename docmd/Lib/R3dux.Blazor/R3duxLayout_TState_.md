#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor](R3dux.Blazor.md#R3dux.Blazor 'R3dux.Blazor')

## R3duxLayout<TState> Class

A base layout class for R3dux components that manages state and dispatches actions.

```csharp
public abstract class R3duxLayout<TState> : R3dux.Blazor.R3duxComponent<TState>
    where TState : notnull
```
#### Type parameters

<a name='R3dux.Blazor.R3duxLayout_TState_.TState'></a>

`TState`

The type of the state managed by this component.

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; [Microsoft.AspNetCore.Components.ComponentBase](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ComponentBase 'Microsoft.AspNetCore.Components.ComponentBase') &#129106; [R3dux.Blazor.R3duxComponent&lt;](R3duxComponent_TState_.md 'R3dux.Blazor.R3duxComponent<TState>')[TState](R3duxLayout_TState_.md#R3dux.Blazor.R3duxLayout_TState_.TState 'R3dux.Blazor.R3duxLayout<TState>.TState')[&gt;](R3duxComponent_TState_.md 'R3dux.Blazor.R3duxComponent<TState>') &#129106; R3duxLayout<TState>

| Properties | |
| :--- | :--- |
| [Body](R3duxLayout_TState_.Body.md 'R3dux.Blazor.R3duxLayout<TState>.Body') | Gets or sets the content to be rendered inside the layout. |

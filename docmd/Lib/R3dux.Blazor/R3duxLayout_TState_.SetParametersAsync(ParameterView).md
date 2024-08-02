#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor](R3dux.Blazor.md#R3dux.Blazor 'R3dux.Blazor').[R3duxLayout&lt;TState&gt;](R3duxLayout_TState_.md 'R3dux.Blazor.R3duxLayout<TState>')

## R3duxLayout<TState>.SetParametersAsync(ParameterView) Method

Sets parameters supplied by the component's parent in the render tree.

```csharp
public override System.Threading.Tasks.Task SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView parameters);
```
#### Parameters

<a name='R3dux.Blazor.R3duxLayout_TState_.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView).parameters'></a>

`parameters` [Microsoft.AspNetCore.Components.ParameterView](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ParameterView 'Microsoft.AspNetCore.Components.ParameterView')

The parameters.

Implements [SetParametersAsync(ParameterView)](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.IComponent.SetParametersAsync#Microsoft_AspNetCore_Components_IComponent_SetParametersAsync_Microsoft_AspNetCore_Components_ParameterView_ 'Microsoft.AspNetCore.Components.IComponent.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView)')

#### Returns
[System.Threading.Tasks.Task](https://docs.microsoft.com/en-us/dotnet/api/System.Threading.Tasks.Task 'System.Threading.Tasks.Task')  
A [System.Threading.Tasks.Task](https://docs.microsoft.com/en-us/dotnet/api/System.Threading.Tasks.Task 'System.Threading.Tasks.Task') that completes when the component has finished updating and rendering itself.

### Remarks
  
Parameters are passed when [Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView)](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync#Microsoft_AspNetCore_Components_ComponentBase_SetParametersAsync_Microsoft_AspNetCore_Components_ParameterView_ 'Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView)') is called. It is not required that  
the caller supply a parameter value for all of the parameters that are logically understood by the component.  
  
The default implementation of [Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView)](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync#Microsoft_AspNetCore_Components_ComponentBase_SetParametersAsync_Microsoft_AspNetCore_Components_ParameterView_ 'Microsoft.AspNetCore.Components.ComponentBase.SetParametersAsync(Microsoft.AspNetCore.Components.ParameterView)') will set the value of each property  
decorated with [Microsoft.AspNetCore.Components.ParameterAttribute](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ParameterAttribute 'Microsoft.AspNetCore.Components.ParameterAttribute') or [Microsoft.AspNetCore.Components.CascadingParameterAttribute](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.CascadingParameterAttribute 'Microsoft.AspNetCore.Components.CascadingParameterAttribute') that has  
a corresponding value in the [Microsoft.AspNetCore.Components.ParameterView](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.AspNetCore.Components.ParameterView 'Microsoft.AspNetCore.Components.ParameterView'). Parameters that do not have a corresponding value  
will be unchanged.
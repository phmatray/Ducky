#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor](R3dux.Blazor.md#R3dux.Blazor 'R3dux.Blazor').[DependencyInjections](DependencyInjections.md 'R3dux.Blazor.DependencyInjections')

## DependencyInjections.AddR3dux(this IServiceCollection, Action<R3duxOptions>) Method

Adds R3dux services to the specified [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection'). This method registers the BlazorR3 services, dispatcher, slices, and effects.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddR3dux(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Action<R3dux.R3duxOptions>? configureOptions=null);
```
#### Parameters

<a name='R3dux.Blazor.DependencyInjections.AddR3dux(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_R3dux.R3duxOptions_).services'></a>

`services` [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection')

The [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection') to add the services to.

<a name='R3dux.Blazor.DependencyInjections.AddR3dux(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Action_R3dux.R3duxOptions_).configureOptions'></a>

`configureOptions` [System.Action&lt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')[R3dux.R3duxOptions](https://docs.microsoft.com/en-us/dotnet/api/R3dux.R3duxOptions 'R3dux.R3duxOptions')[&gt;](https://docs.microsoft.com/en-us/dotnet/api/System.Action-1 'System.Action`1')

An optional action to configure the [R3dux.R3duxOptions](https://docs.microsoft.com/en-us/dotnet/api/R3dux.R3duxOptions 'R3dux.R3duxOptions').

#### Returns
[Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection')  
The [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection') so that additional calls can be chained.
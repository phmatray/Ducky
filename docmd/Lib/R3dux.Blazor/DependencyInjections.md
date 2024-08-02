#### [R3dux.Blazor](R3dux.Blazor.md 'R3dux.Blazor')
### [R3dux.Blazor](R3dux.Blazor.md#R3dux.Blazor 'R3dux.Blazor')

## DependencyInjections Class

Extension methods for adding R3dux services to the dependency injection container.

```csharp
public static class DependencyInjections
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; DependencyInjections

| Methods | |
| :--- | :--- |
| [AddR3dux(this IServiceCollection, IConfiguration, Action&lt;R3duxOptions&gt;)](DependencyInjections.AddR3dux(thisIServiceCollection,IConfiguration,Action_R3duxOptions_).md 'R3dux.Blazor.DependencyInjections.AddR3dux(this Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, System.Action<R3dux.R3duxOptions>)') | Adds R3dux services to the specified [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection'). This method registers the BlazorR3 services, dispatcher, slices, and effects. |
| [AddR3dux(this IServiceCollection, Action&lt;R3duxOptions&gt;)](DependencyInjections.AddR3dux(thisIServiceCollection,Action_R3duxOptions_).md 'R3dux.Blazor.DependencyInjections.AddR3dux(this Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Action<R3dux.R3duxOptions>)') | Adds R3dux services to the specified [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection'). This method registers the BlazorR3 services, dispatcher, slices, and effects. |

#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[DependencyInjections](DependencyInjections.md 'R3dux.DependencyInjections')

## DependencyInjections.AddR3duxCore(this IServiceCollection, R3duxOptions) Method

Core method to add R3dux services to the specified [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection'). This method registers the BlazorR3 services, dispatcher, slices, and effects.

```csharp
public static Microsoft.Extensions.DependencyInjection.IServiceCollection AddR3duxCore(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, R3dux.R3duxOptions options);
```
#### Parameters

<a name='R3dux.DependencyInjections.AddR3duxCore(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,R3dux.R3duxOptions).services'></a>

`services` [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection')

The [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection') to add the services to.

<a name='R3dux.DependencyInjections.AddR3duxCore(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,R3dux.R3duxOptions).options'></a>

`options` [R3duxOptions](R3duxOptions.md 'R3dux.R3duxOptions')

The configured [R3duxOptions](R3duxOptions.md 'R3dux.R3duxOptions').

#### Returns
[Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection')  
The [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection') so that additional calls can be chained.
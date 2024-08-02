#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux').[DependencyInjections](DependencyInjections.md 'R3dux.DependencyInjections')

## DependencyInjections.ScanAndRegister<T>(this IServiceCollection, Assembly[]) Method

Scans the specified assemblies for classes assignable to the specified type and registers them as singleton services.

```csharp
private static void ScanAndRegister<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection services, System.Reflection.Assembly[] assemblies);
```
#### Type parameters

<a name='R3dux.DependencyInjections.ScanAndRegister_T_(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Reflection.Assembly[]).T'></a>

`T`
#### Parameters

<a name='R3dux.DependencyInjections.ScanAndRegister_T_(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Reflection.Assembly[]).services'></a>

`services` [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection')

The [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection') to add the services to.

<a name='R3dux.DependencyInjections.ScanAndRegister_T_(thisMicrosoft.Extensions.DependencyInjection.IServiceCollection,System.Reflection.Assembly[]).assemblies'></a>

`assemblies` [System.Reflection.Assembly](https://docs.microsoft.com/en-us/dotnet/api/System.Reflection.Assembly 'System.Reflection.Assembly')[[]](https://docs.microsoft.com/en-us/dotnet/api/System.Array 'System.Array')

The assemblies to scan for classes.
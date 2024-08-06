#### [R3dux](R3dux.md 'R3dux')
### [R3dux](R3dux.md#R3dux 'R3dux')

## DependencyInjections Class

Extension methods for adding R3dux services to the dependency injection container.

```csharp
public static class DependencyInjections
```

Inheritance [System.Object](https://docs.microsoft.com/en-us/dotnet/api/System.Object 'System.Object') &#129106; DependencyInjections

| Methods | |
| :--- | :--- |
| [AddR3duxCore(this IServiceCollection, R3duxOptions)](DependencyInjections.AddR3duxCore(thisIServiceCollection,R3duxOptions).md 'R3dux.DependencyInjections.AddR3duxCore(this Microsoft.Extensions.DependencyInjection.IServiceCollection, R3dux.R3duxOptions)') | Core method to add R3dux services to the specified [Microsoft.Extensions.DependencyInjection.IServiceCollection](https://docs.microsoft.com/en-us/dotnet/api/Microsoft.Extensions.DependencyInjection.IServiceCollection 'Microsoft.Extensions.DependencyInjection.IServiceCollection'). This method registers the BlazorR3 services, dispatcher, slices, and effects. |
| [ScanAndRegister&lt;T&gt;(this IServiceCollection, Assembly[])](DependencyInjections.ScanAndRegister_T_(thisIServiceCollection,Assembly[]).md 'R3dux.DependencyInjections.ScanAndRegister<T>(this Microsoft.Extensions.DependencyInjection.IServiceCollection, System.Reflection.Assembly[])') | Scans the specified assemblies for classes assignable to the specified type and registers them as singleton services. |

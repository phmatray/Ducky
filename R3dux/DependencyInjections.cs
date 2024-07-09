using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace R3dux;

/// <summary>
/// Extension methods for adding R3dux services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Adds R3dux services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddR3dux(
        this IServiceCollection services,
        Action<R3duxOptions>? configureOptions = null)
    {
        // Configure options
        R3duxOptions options = new();
        // TODO: AddR3dux with IConfiguration
        configureOptions?.Invoke(options);

        // Add Reactive Extensions
        services.AddBlazorR3();

        // Add Store services
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.AddSingleton<IStoreFactory, StoreFactory>();
        services.AddSingleton<IRootStateSerializer, RootStateSerializer>();
        
        // Scan and register all Slices an Effects
        services.ScanAndRegister<ISlice>(options.Assemblies);
        services.ScanAndRegister<IEffect>(options.Assemblies);
        
        // Add Store
        services.AddSingleton<IStore, Store>(sp =>
        {
            var storeFactory = sp.GetRequiredService<IStoreFactory>();
            var dispatcher = sp.GetRequiredService<IDispatcher>();
            var slices = sp.GetServices<ISlice>();
            var effects = sp.GetServices<IEffect>();

            return storeFactory.CreateStore(dispatcher, slices, effects);
        });
        
        return services;
    }
    
    /// <summary>
    /// Scans the specified assemblies for classes assignable to the specified type and registers them as singleton services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="assemblies">The assemblies to scan for classes.</param>
    private static void ScanAndRegister<T>(
        this IServiceCollection services,
        Assembly[] assemblies)
    {
        services.Scan(scan => scan
            .FromAssemblies(assemblies)
            .AddClasses(classes => classes.AssignableTo(typeof(T)))
            .AsImplementedInterfaces()
            .WithSingletonLifetime());
    }
}

/// <summary>
/// Options for configuring R3dux services.
/// </summary>
public class R3duxOptions
{
    /// <summary>
    /// Gets or sets the assemblies to scan for slices and effects. Defaults to the executing assembly.
    /// </summary>
    public Assembly[] Assemblies { get; set; } = GetDefaultAssemblies();

    private static Assembly[] GetDefaultAssemblies()
    {
        var entryAssembly = 
            Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to determine the entry assembly.");
        
        return [entryAssembly];
    }
}
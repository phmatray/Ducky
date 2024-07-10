using System.Reflection;
using Microsoft.Extensions.Configuration;
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
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddR3dux(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<R3duxOptions>? configureOptions = null)
    {
        // Configure options
        R3duxOptions options = new();
        configuration.GetSection("R3dux").Bind(options); // Bind configuration section to R3duxOptions
        configureOptions?.Invoke(options);

        return services.AddR3duxCore(options);
    }

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
        configureOptions?.Invoke(options);

        return services.AddR3duxCore(options);
    }
    
    /// <summary>
    /// Core method to add R3dux services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="options">The configured <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    private static IServiceCollection AddR3duxCore(
        this IServiceCollection services,
        R3duxOptions options)
    {
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
            var slices = sp.GetServices<ISlice>().ToArray();
            var effects = sp.GetServices<IEffect>().ToArray();

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
    /// Gets or sets the assemblies to scan for slices and effects.
    /// </summary>
    public string[] AssemblyNames { get; set; } = [];
    
    /// <summary>
    /// Gets or sets the assemblies to scan for slices and effects. Defaults to the executing assembly.
    /// </summary>
    public Assembly[] Assemblies
        => GetAssemblies();

    private Assembly[] GetAssemblies()
    {
        if (AssemblyNames.Length == 0)
        {
            return GetDefaultAssemblies();
        }

        return AssemblyNames
            .Select(name => Assembly.Load(name))
            .ToArray();
    }

    private static Assembly[] GetDefaultAssemblies()
    {
        var entryAssembly = 
            Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("Unable to determine the entry assembly.");
        
        return [entryAssembly];
    }
}
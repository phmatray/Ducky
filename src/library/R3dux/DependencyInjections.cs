// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<IRootStateSerializer, RootStateSerializer>();

        // Scan and register all Slices an Effects
        services.ScanAndRegister<ISlice>(options.Assemblies);
        services.ScanAndRegister<IEffect>(options.Assemblies);

        // Add Store
        services.AddScoped<IStore, Store>(sp =>
        {
            var dispatcher = sp.GetRequiredService<IDispatcher>();
            var logger = sp.GetRequiredService<ILogger<Store>>();
            var slices = sp.GetServices<ISlice>().ToArray();
            var effects = sp.GetServices<IEffect>().ToArray();

            return StoreFactory.CreateStore(dispatcher, logger, slices, effects);
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
            .WithScopedLifetime());
    }
}

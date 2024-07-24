// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace R3dux;

/// <summary>
/// Extension methods for adding R3dux services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Core method to add R3dux services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="options">The configured <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddR3duxCore(
        this IServiceCollection services,
        R3duxOptions options)
    {
        // Add Store services
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.AddSingleton<IRootStateSerializer, RootStateSerializer>();

        // Scan and register all Slices an Effects
        services.ScanAndRegister<ISlice>(options.Assemblies);
        services.ScanAndRegister<IEffect>(options.Assemblies);

        // Add Store
        services.AddScoped<R3duxStore>(sp =>
        {
            var dispatcher = sp.GetRequiredService<IDispatcher>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var slices = sp.GetServices<ISlice>().ToArray();
            var effects = sp.GetServices<IEffect>().ToArray();

            // Configure the logger provider
            LoggerProvider.Configure(loggerFactory);

            return StoreFactory.CreateStore(dispatcher, slices, effects);
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

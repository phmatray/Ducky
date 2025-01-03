// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Ducky;

/// <summary>
/// Extension methods for adding Ducky services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Core method to add Ducky services to the specified <see cref="IServiceCollection"/>.
    /// This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="options">The configured <see cref="DuckyOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddDuckyCore(
        this IServiceCollection services,
        DuckyOptions options)
    {
        // Add Store services
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.AddSingleton<IRootStateSerializer, RootStateSerializer>();

        // Scan and register all Slices an Effects
        services.ScanAndRegister<ISlice>(options.Assemblies);
        services.ScanAndRegister<IAsyncEffect>(options.Assemblies);
        services.ScanAndRegister<IReactiveEffect>(options.Assemblies);

        // Add Store
        services.AddScoped<DuckyStore>(sp =>
        {
            IDispatcher dispatcher = sp.GetRequiredService<IDispatcher>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            IEnumerable<ISlice> slices = sp.GetServices<ISlice>();
            IEnumerable<IAsyncEffect> asyncEffects = sp.GetServices<IAsyncEffect>();
            IEnumerable<IReactiveEffect> reactiveEffects = sp.GetServices<IReactiveEffect>();

            // Configure the logger provider
            LoggerProvider.Configure(loggerFactory);

            return DuckyStoreFactory.CreateStore(dispatcher, slices, asyncEffects, reactiveEffects);
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
        IEnumerable<Assembly> assemblies)
    {
        foreach (Assembly assembly in assemblies)
        {
            IEnumerable<ServiceDescriptor> serviceDescriptors = assembly.DefinedTypes
                .Where(type => type is { IsAbstract: false, IsInterface: false })
                .Where(type => typeof(T).IsAssignableFrom(type))
                .Select(type => ServiceDescriptor.Scoped(typeof(T), type));

            services.TryAddEnumerable(serviceDescriptors);
        }
    }
}

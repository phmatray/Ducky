// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Ducky.Pipeline;
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
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IStoreEventPublisher, StoreEventPublisher>();
        services.AddScoped<IRootStateSerializer, RootStateSerializer>();
        services.AddScoped<StoreLogger>();

        // Scan and register all Slices, Effects and Middlewares
        services.ScanAndRegister<ISlice>(options.Assemblies);
        services.ScanAndRegister<IAsyncEffect>(options.Assemblies);
        services.ScanAndRegister<IReactiveEffect>(options.Assemblies);

        // Add Store
        services.AddScoped<IStore, DuckyStore>(sp =>
        {
            IDispatcher dispatcher = sp.GetRequiredService<IDispatcher>();
            IStoreEventPublisher eventPublisher = sp.GetRequiredService<IStoreEventPublisher>();
            IEnumerable<ISlice> slices = sp.GetServices<ISlice>();

            // Ensure StoreLogger is created to start listening to events
            _ = sp.GetRequiredService<StoreLogger>();

            ILogger<ActionPipeline> logger = sp.GetRequiredService<ILogger<ActionPipeline>>();
            ActionPipeline pipeline = new(sp, logger);

            // Configure pipeline with legacy method if provided
            options.ConfigurePipeline?.Invoke(pipeline);

            // Configure pipeline with service provider if provided
            options.ConfigurePipelineWithServices?.Invoke(pipeline, sp);

            return new DuckyStore(dispatcher, pipeline, eventPublisher, slices);
        });

        return services;
    }

    /// <summary>
    /// Adds Ducky services to the specified <see cref="IServiceCollection"/> with middleware configuration.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configure">An action to configure the Ducky options.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddDucky(
        this IServiceCollection services,
        Action<DuckyOptions> configure)
    {
        DuckyOptions options = new();
        configure(options);

        return services.AddDuckyCore(options);
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
                .Where(type => type is { IsAbstract: false, IsInterface: false } && typeof(T).IsAssignableFrom(type))
                .Select(type => ServiceDescriptor.Scoped(typeof(T), type));

            services.TryAddEnumerable(serviceDescriptors);
        }
    }
}

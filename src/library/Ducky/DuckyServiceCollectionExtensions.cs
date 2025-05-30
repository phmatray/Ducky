// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky;

/// <summary>
/// Extension methods for configuring Ducky services.
/// </summary>
public static class DuckyServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ducky services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDucky(this IServiceCollection services)
    {
        return services.AddDuckyCore(new DuckyOptions());
    }

    /// <summary>
    /// Adds Ducky services with specified assemblies to scan.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies to scan for slices and effects.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDucky(this IServiceCollection services, params Assembly[] assemblies)
    {
        DuckyOptions options = new() { AssemblyNames = assemblies.Select(a => a.GetName().Name!).ToArray() };

        return services.AddDuckyCore(options);
    }

    /// <summary>
    /// Adds Ducky services with middleware types that will be automatically resolved and added to the pipeline.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="middlewareTypes">The middleware types to use in order.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyWithMiddleware(
        this IServiceCollection services,
        params Type[] middlewareTypes)
    {
        DuckyOptions options = new()
        {
            ConfigurePipelineWithServices = (pipeline, sp) =>
            {
                foreach (Type middlewareType in middlewareTypes)
                {
                    if (!typeof(IActionMiddleware).IsAssignableFrom(middlewareType))
                    {
                        throw new ArgumentException(
                            $"Type {middlewareType.Name} must implement IActionMiddleware",
                            nameof(middlewareTypes));
                    }

                    var middleware = (IActionMiddleware)sp.GetRequiredService(middlewareType);
                    pipeline.Use(middleware);
                }
            }
        };

        return services.AddDuckyCore(options);
    }

    /// <summary>
    /// Adds Ducky services with a fluent configuration builder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDucky(
        this IServiceCollection services,
        Action<IDuckyConfigurationBuilder> configure)
    {
        DuckyConfigurationBuilder builder = new();
        configure(builder);
        DuckyOptions options = builder.Build();

        return services.AddDuckyCore(options);
    }

    /// <summary>
    /// Adds Ducky services with a store builder for robust configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The store builder configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyStore(
        this IServiceCollection services,
        Action<Builder.IStoreBuilder> configure)
    {
        Builder.StoreBuilder storeBuilder = new(services);
        configure(storeBuilder);

        // Extract middleware types from the builder
        List<Type> middlewareTypes = storeBuilder.GetMiddlewareTypes();

        DuckyOptions options = new()
        {
            ConfigurePipelineWithServices = (pipeline, sp) =>
            {
                foreach (Type middlewareType in middlewareTypes)
                {
                    var middleware = (IActionMiddleware)sp.GetRequiredService(middlewareType);
                    pipeline.Use(middleware);
                }
            }
        };

        return services.AddDuckyCore(options);
    }
}

/// <summary>
/// Interface for the Ducky configuration builder.
/// </summary>
public interface IDuckyConfigurationBuilder
{
    /// <summary>
    /// Adds assemblies to scan for slices and effects.
    /// </summary>
    IDuckyConfigurationBuilder AddAssemblies(params Assembly[] assemblies);

    /// <summary>
    /// Adds assembly names to scan for slices and effects.
    /// </summary>
    IDuckyConfigurationBuilder AddAssemblyNames(params string[] assemblyNames);

    /// <summary>
    /// Configures the middleware pipeline.
    /// </summary>
    IDuckyConfigurationBuilder ConfigurePipeline(Action<ActionPipeline, IServiceProvider> configurePipeline);

    /// <summary>
    /// Uses the specified middleware types in the pipeline.
    /// </summary>
    IDuckyConfigurationBuilder UseMiddleware(params Type[] middlewareTypes);

    /// <summary>
    /// Uses the specified middleware type in the pipeline.
    /// </summary>
    IDuckyConfigurationBuilder UseMiddleware<TMiddleware>() where TMiddleware : IActionMiddleware;
}

/// <summary>
/// Implementation of the Ducky configuration builder.
/// </summary>
internal class DuckyConfigurationBuilder : IDuckyConfigurationBuilder
{
    private readonly List<string> _assemblyNames = [];
    private readonly List<Action<ActionPipeline, IServiceProvider>> _pipelineConfigurators = [];

    public IDuckyConfigurationBuilder AddAssemblies(params Assembly[] assemblies)
    {
        _assemblyNames.AddRange(assemblies.Select(a => a.GetName().Name!));
        return this;
    }

    public IDuckyConfigurationBuilder AddAssemblyNames(params string[] assemblyNames)
    {
        _assemblyNames.AddRange(assemblyNames);
        return this;
    }

    public IDuckyConfigurationBuilder ConfigurePipeline(Action<ActionPipeline, IServiceProvider> configurePipeline)
    {
        _pipelineConfigurators.Add(configurePipeline);
        return this;
    }

    public IDuckyConfigurationBuilder UseMiddleware(params Type[] middlewareTypes)
    {
        _pipelineConfigurators.Add((pipeline, sp) =>
        {
            foreach (Type middlewareType in middlewareTypes)
            {
                if (!typeof(IActionMiddleware).IsAssignableFrom(middlewareType))
                {
                    throw new ArgumentException(
                        $"Type {middlewareType.Name} must implement IActionMiddleware",
                        nameof(middlewareTypes));
                }

                var middleware = (IActionMiddleware)sp.GetRequiredService(middlewareType);
                pipeline.Use(middleware);
            }
        });
        return this;
    }

    public IDuckyConfigurationBuilder UseMiddleware<TMiddleware>() where TMiddleware : IActionMiddleware
    {
        _pipelineConfigurators.Add((pipeline, sp) =>
        {
            TMiddleware middleware = sp.GetRequiredService<TMiddleware>();
            pipeline.Use(middleware);
        });
        return this;
    }

    internal DuckyOptions Build()
    {
        DuckyOptions options = new() { AssemblyNames = _assemblyNames.ToArray() };

        if (_pipelineConfigurators.Count > 0)
        {
            options.ConfigurePipelineWithServices = (pipeline, sp) =>
            {
                foreach (Action<ActionPipeline, IServiceProvider> configurator in _pipelineConfigurators)
                {
                    configurator(pipeline, sp);
                }
            };
        }

        return options;
    }
}

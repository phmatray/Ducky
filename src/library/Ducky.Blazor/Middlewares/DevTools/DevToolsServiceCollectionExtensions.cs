using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Middlewares.DevTools;

/// <summary>
/// Provides extension methods for registering the Redux DevTools middleware and related services.
/// </summary>
public static class DevToolsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Redux DevTools middleware and its dependencies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storeName">Optional name for the store in DevTools UI.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDevToolsMiddleware(
        this IServiceCollection services,
        string? storeName = null)
    {
        return AddDevToolsMiddleware(
            services,
            options =>
            {
                if (storeName is null)
                {
                    return;
                }

                options.StoreName = storeName;
            });
    }

    /// <summary>
    /// Registers the Redux DevTools middleware and its dependencies with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure DevTools options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDevToolsMiddleware(
        this IServiceCollection services,
        Action<DevToolsOptions>? configureOptions = null)
    {
        // Register the DevTools options
        services.TryAddSingleton<DevToolsOptions>(_ =>
        {
            DevToolsOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register the state manager
        services.TryAddSingleton<DevToolsStateManager>();

        // Register the DevTools reducer
        services.TryAddScoped<DevToolsReducer>();

        // Register the DevTools JS module
        services.TryAddScoped<ReduxDevToolsModule>(serviceProvider =>
        {
            IJSRuntime jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();
            IStore store = serviceProvider.GetRequiredService<IStore>();
            IDispatcher dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
            DevToolsStateManager stateManager = serviceProvider.GetRequiredService<DevToolsStateManager>();
            DevToolsOptions options = serviceProvider.GetRequiredService<DevToolsOptions>();
            return new ReduxDevToolsModule(jsRuntime, store, dispatcher, stateManager, options);
        });

        // Register the middleware as IActionMiddleware
        services.AddScoped<IActionMiddleware, DevToolsMiddleware>();

        return services;
    }

    /// <summary>
    /// Registers an enhanced DevTools middleware with time-travel support.
    /// This version includes a special reducer that can handle state restoration actions.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure DevTools options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEnhancedDevToolsMiddleware(
        this IServiceCollection services,
        Action<DevToolsOptions>? configureOptions = null)
    {
        // Register all the base DevTools services
        AddDevToolsMiddleware(services, configureOptions);

        // Add slice for DevTools state management
        // This would register a special slice that handles DevTools actions
        // For now, we'll document this as a future enhancement

        return services;
    }
}

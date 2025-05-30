using Blazored.LocalStorage;
using Ducky;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Provides extension methods for registering the persistence middleware
/// and related services in the service collection.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the persistence middleware with default options and LocalStorage provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storageKey">Optional storage key. If null, a default key will be used.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistenceMiddleware(
        this IServiceCollection services,
        string? storageKey = null)
    {
        return AddPersistenceMiddleware(
            services,
            options =>
            {
                if (storageKey is null)
                {
                    return;
                }

                options.StorageKey = storageKey;
            });
    }

    /// <summary>
    /// Registers the persistence middleware with configuration options and LocalStorage provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistenceMiddleware(
        this IServiceCollection services,
        Action<PersistenceOptions>? configureOptions = null)
    {
        // Register options
        services.TryAddSingleton<PersistenceOptions>(_ =>
        {
            PersistenceOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register hydration manager
        services.TryAddScoped<HydrationManager>();

        // Register enhanced persistence provider using LocalStorage
        services.TryAddScoped<IEnhancedPersistenceProvider<IRootState>>(serviceProvider =>
        {
            ILocalStorageService localStorage = serviceProvider.GetRequiredService<ILocalStorageService>();
            PersistenceOptions options = serviceProvider.GetRequiredService<PersistenceOptions>();
            return new LocalStoragePersistenceProvider<IRootState>(localStorage, options.StorageKey);
        });

        // Register persistence middleware as IActionMiddleware
        services.AddScoped<IActionMiddleware, PersistenceMiddleware>();

        return services;
    }

    /// <summary>
    /// Registers the persistence middleware with a custom enhanced persistence provider.
    /// </summary>
    /// <typeparam name="TProvider">The type of the custom persistence provider.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistenceMiddleware<TProvider>(
        this IServiceCollection services,
        Action<PersistenceOptions>? configureOptions = null)
        where TProvider : class, IEnhancedPersistenceProvider<IRootState>
    {
        // Register options
        services.TryAddSingleton<PersistenceOptions>(_ =>
        {
            PersistenceOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register hydration manager
        services.TryAddScoped<HydrationManager>();

        // Register custom persistence provider
        services.TryAddScoped<IEnhancedPersistenceProvider<IRootState>, TProvider>();

        // Register persistence middleware as IActionMiddleware
        services.AddScoped<IActionMiddleware, PersistenceMiddleware>();

        return services;
    }

    /// <summary>
    /// Registers the persistence middleware with a custom enhanced persistence provider factory.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="providerFactory">Factory function to create the persistence provider.</param>
    /// <param name="configureOptions">Action to configure persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPersistenceMiddleware(
        this IServiceCollection services,
        Func<IServiceProvider, IEnhancedPersistenceProvider<IRootState>> providerFactory,
        Action<PersistenceOptions>? configureOptions = null)
    {
        // Register options
        services.TryAddSingleton<PersistenceOptions>(_ =>
        {
            PersistenceOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register hydration manager
        services.TryAddScoped<HydrationManager>();

        // Register custom persistence provider using factory
        services.TryAddScoped(providerFactory);

        // Register persistence middleware as IActionMiddleware
        services.AddScoped<IActionMiddleware, PersistenceMiddleware>();

        return services;
    }

    /// <summary>
    /// Registers an enhanced persistence middleware that supports advanced features.
    /// This includes throttling, debouncing, filtering, and comprehensive error handling.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure advanced persistence options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEnhancedPersistenceMiddleware(
        this IServiceCollection services,
        Action<PersistenceOptions>? configureOptions = null)
    {
        return AddPersistenceMiddleware(
            services,
            options =>
            {
                // Set enhanced defaults
                options.EnableLogging = true;
                options.ThrottleDelayMs = 1000;
                options.DebounceDelayMs = 500;
                options.MaxHydrationRetries = 3;
                options.HydrationRetryDelayMs = 1000;
                options.PersistInitialState = true;
                options.QueueActionsOnHydration = true;

                // Apply custom configuration
                configureOptions?.Invoke(options);
            });
    }

    /// <summary>
    /// Registers basic persistence middleware with minimal configuration for simple use cases.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storageKey">The storage key to use for persistence.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBasicPersistenceMiddleware(
        this IServiceCollection services,
        string? storageKey = null)
    {
        return AddPersistenceMiddleware(
            services,
            options =>
            {
                // Basic configuration
                options.StorageKey = storageKey;
                options.EnableLogging = false;
                options.ThrottleDelayMs = 0;
                options.DebounceDelayMs = 1000; // Only debouncing for basic
                options.MaxHydrationRetries = 1;
                options.HydrationRetryDelayMs = 500;
                options.PersistInitialState = false;
                options.QueueActionsOnHydration = true;
                options.ErrorHandling = PersistenceErrorHandling.LogAndContinue;
            });
    }
}

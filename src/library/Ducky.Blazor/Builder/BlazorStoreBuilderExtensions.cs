using Ducky.Blazor.Middlewares.DevTools;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Blazor.Middlewares.Persistence;
using Ducky.Builder;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;

namespace Ducky.Blazor.Builder;

/// <summary>
/// Blazor-specific extension methods for IStoreBuilder.
/// </summary>
public static class BlazorStoreBuilderExtensions
{
    /// <summary>
    /// Adds JavaScript console logging middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddJsLoggingMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the JS module
        builder.Services.AddScoped<JsConsoleLoggerModule>(sp =>
        {
            IJSRuntime jsRuntime = sp.GetRequiredService<IJSRuntime>();
            return new JsConsoleLoggerModule(jsRuntime);
        });

        return builder.AddMiddleware<JsLoggingMiddleware>();
    }

    /// <summary>
    /// Adds Redux DevTools middleware to the store pipeline with optional configuration.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <param name="configureOptions">Optional action to configure DevTools options.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddDevToolsMiddleware(
        this IStoreBuilder builder,
        Action<DevToolsOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the DevTools options
        builder.Services.TryAddSingleton<DevToolsOptions>(_ =>
        {
            DevToolsOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register supporting services
        builder.Services.TryAddSingleton<DevToolsStateManager>();
        builder.Services.AddScoped<DevToolsReducer>();

        // Register the DevTools JS module
        builder.Services.AddScoped<ReduxDevToolsModule>(sp =>
        {
            IJSRuntime jsRuntime = sp.GetRequiredService<IJSRuntime>();
            IStore store = sp.GetRequiredService<IStore>();
            IDispatcher dispatcher = sp.GetRequiredService<IDispatcher>();
            DevToolsStateManager stateManager = sp.GetRequiredService<DevToolsStateManager>();
            DevToolsOptions options = sp.GetRequiredService<DevToolsOptions>();
            return new ReduxDevToolsModule(jsRuntime, store, dispatcher, stateManager, options);
        });

        // Register the middleware with factory
        return builder.AddMiddleware<DevToolsMiddleware>(sp =>
        {
            ReduxDevToolsModule devTools = sp.GetRequiredService<ReduxDevToolsModule>();
            return new DevToolsMiddleware(devTools);
        });
    }

    /// <summary>
    /// Adds persistence middleware to the store pipeline with optional configuration.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <param name="configureOptions">Optional action to configure persistence options.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddPersistenceMiddleware(
        this IStoreBuilder builder,
        Action<PersistenceOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register the persistence options
        builder.Services.TryAddSingleton<PersistenceOptions>(_ =>
        {
            PersistenceOptions options = new();
            configureOptions?.Invoke(options);
            return options;
        });

        // Register the hydration manager
        builder.Services.AddScoped<HydrationManager>();

        // Register the persistence provider (default to LocalStorage)
        builder.Services.AddScoped(typeof(IPersistenceProvider<>), typeof(LocalStoragePersistenceProvider<>));
        builder.Services.AddScoped(typeof(IEnhancedPersistenceProvider<>), typeof(LocalStoragePersistenceProvider<>));
        
        // Register the enhanced persistence provider specifically for IRootState
        builder.Services.AddScoped<IEnhancedPersistenceProvider<IRootState>>(sp =>
        {
            Blazored.LocalStorage.ILocalStorageService localStorage = sp.GetRequiredService<Blazored.LocalStorage.ILocalStorageService>();
            PersistenceOptions options = sp.GetRequiredService<PersistenceOptions>();
            return new LocalStoragePersistenceProvider<IRootState>(localStorage, options.StorageKey);
        });

        // Register the middleware with factory
        return builder.AddMiddleware<PersistenceMiddleware>(sp =>
        {
            IEnhancedPersistenceProvider<IRootState> persistenceProvider = 
                sp.GetRequiredService<IEnhancedPersistenceProvider<IRootState>>();
            HydrationManager hydrationManager = sp.GetRequiredService<HydrationManager>();
            PersistenceOptions options = sp.GetRequiredService<PersistenceOptions>();
            return new PersistenceMiddleware(persistenceProvider, hydrationManager, options);
        });
    }

    /// <summary>
    /// Adds all recommended Blazor middlewares to the store pipeline.
    /// Includes: JsLogging, DevTools (in development), and Persistence.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <param name="isDevelopment">Whether the application is running in development mode.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddBlazorMiddlewares(
        this IStoreBuilder builder,
        bool isDevelopment = false)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddJsLoggingMiddleware()
            .AddDevToolsMiddleware(options => options.Enabled = isDevelopment)
            .AddPersistenceMiddleware();
    }
}

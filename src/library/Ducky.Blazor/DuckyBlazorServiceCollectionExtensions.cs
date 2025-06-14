using Blazored.LocalStorage;
using Ducky.Blazor.CrossTabSync;
using Ducky.Blazor.Middlewares.DevTools;
using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Blazor.Middlewares.Persistence;
using Ducky.Blazor.Services;
using Ducky.Builder;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Ducky.Blazor;

/// <summary>
/// Extension methods for configuring Ducky in Blazor applications.
/// </summary>
public static class DuckyBlazorServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ducky services optimized for Blazor applications with sensible defaults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // Basic Blazor setup
    /// builder.Services.AddDuckyBlazor();
    /// 
    /// // With configuration
    /// builder.Services.AddDuckyBlazor(ducky => ducky
    ///     .AddEffect&lt;MyEffect&gt;()
    ///     .EnablePersistence()
    ///     .EnableCrossTabSync());
    /// </code>
    /// </example>
    public static IServiceCollection AddDuckyBlazor(
        this IServiceCollection services,
        Action<BlazorDuckyBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Use the core Ducky builder
        services.AddDucky(builder =>
        {
            // Apply default middlewares
            builder.UseDefaultMiddlewares();

            // Create Blazor-specific builder wrapper
            BlazorDuckyBuilder blazorBuilder = new(builder, services);

            // Apply Blazor defaults
            blazorBuilder.UseBlazorDefaults();

            // Apply user configuration
            configure?.Invoke(blazorBuilder);
        });

        return services;
    }
}

/// <summary>
/// Blazor-specific builder that wraps the core DuckyBuilder with additional Blazor features.
/// </summary>
public class BlazorDuckyBuilder
{
    private readonly DuckyBuilder _innerBuilder;
    private readonly IServiceCollection _services;
    private bool _persistenceEnabled;
    private bool _crossTabSyncEnabled;
    private bool _devToolsEnabled;

    internal BlazorDuckyBuilder(DuckyBuilder innerBuilder, IServiceCollection services)
    {
        _innerBuilder = innerBuilder;
        _services = services;
    }

    /// <summary>
    /// Applies Blazor-specific defaults including DevTools in development.
    /// </summary>
    internal BlazorDuckyBuilder UseBlazorDefaults()
    {
        // Register StoreInitializer service
        _services.AddScoped<DuckyStoreInitializer>();

        // Auto-enable DevTools in development
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (environment == "Development")
        {
            EnableDevTools();
        }

        return this;
    }

    /// <summary>
    /// Enables Redux DevTools integration.
    /// </summary>
    public BlazorDuckyBuilder EnableDevTools(Action<DevToolsOptions>? configure = null)
    {
        if (!_devToolsEnabled)
        {
            _devToolsEnabled = true;

            // Register options first
            _services.AddSingleton<DevToolsOptions>(_ =>
            {
                DevToolsOptions options = new();
                configure?.Invoke(options);
                return options;
            });

            _services.AddScoped<DevToolsStateManager>();
            _services.AddScoped<ReduxDevToolsModule>();

            // Register middleware after dependencies
            _innerBuilder.AddMiddleware<DevToolsMiddleware>();
        }
        else if (configure is not null)
        {
            // If DevTools is already enabled but we have new configuration,
            // we need to update the existing registration
            ServiceDescriptor? existingDescriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(DevToolsOptions));
            if (existingDescriptor is not null)
            {
                _services.Remove(existingDescriptor);
            }

            _services.AddSingleton<DevToolsOptions>(_ =>
            {
                DevToolsOptions options = new();
                configure.Invoke(options);
                return options;
            });
        }

        return this;
    }

    /// <summary>
    /// Enables state persistence to local storage.
    /// </summary>
    public BlazorDuckyBuilder EnablePersistence(Action<PersistenceOptions>? configure = null)
    {
        if (!_persistenceEnabled)
        {
            _persistenceEnabled = true;
            _innerBuilder.AddMiddleware<PersistenceMiddleware>();

            _services.AddSingleton<PersistenceOptions>(_ =>
            {
                PersistenceOptions options = new();
                configure?.Invoke(options);
                return options;
            });

            // Add Blazored.LocalStorage services
            _services.AddBlazoredLocalStorage();

            _services.AddScoped(typeof(IPersistenceProvider<>), typeof(LocalStoragePersistenceProvider<>));
            _services.AddScoped(typeof(IEnhancedPersistenceProvider<>), typeof(LocalStoragePersistenceProvider<>));
            _services.AddScoped<HydrationManager>();
            _services.AddScoped<IPersistenceService, PersistenceService>();
        }

        return this;
    }

    /// <summary>
    /// Enables cross-tab state synchronization.
    /// </summary>
    public BlazorDuckyBuilder EnableCrossTabSync()
    {
        if (!_crossTabSyncEnabled)
        {
            _crossTabSyncEnabled = true;
            _services.AddScoped<CrossTabSyncModule>();
        }

        return this;
    }

    /// <summary>
    /// Enables JavaScript console logging middleware.
    /// </summary>
    public BlazorDuckyBuilder EnableJsLogging()
    {
        // Register the JS module dependency
        _services.AddScoped<JsConsoleLoggerModule>(sp =>
        {
            IJSRuntime jsRuntime = sp.GetRequiredService<IJSRuntime>();
            return new JsConsoleLoggerModule(jsRuntime);
        });

        // Add the middleware
        _innerBuilder.AddMiddleware<JsLoggingMiddleware>();
        return this;
    }

    /// <summary>
    /// Adds a middleware to the pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The middleware type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public BlazorDuckyBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IMiddleware
    {
        _innerBuilder.AddMiddleware<TMiddleware>();
        return this;
    }

    /// <summary>
    /// Adds a state slice to the store.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public BlazorDuckyBuilder AddSlice<TState>() where TState : class, IState, new()
    {
        _innerBuilder.AddSlice<TState>();
        return this;
    }

    /// <summary>
    /// Adds an async effect.
    /// </summary>
    /// <typeparam name="TEffect">The effect type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public BlazorDuckyBuilder AddEffect<TEffect>() where TEffect : class, IAsyncEffect
    {
        _innerBuilder.AddEffect<TEffect>();
        return this;
    }

    /// <summary>
    /// Adds an exception handler.
    /// </summary>
    /// <typeparam name="THandler">The exception handler type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public BlazorDuckyBuilder AddExceptionHandler<THandler>() where THandler : class, IExceptionHandler
    {
        _innerBuilder.AddExceptionHandler<THandler>();
        return this;
    }

    /// <summary>
    /// Scans assemblies for slices and effects.
    /// </summary>
    /// <param name="assemblyNames">The assembly names to scan.</param>
    /// <returns>The builder for chaining.</returns>
    public BlazorDuckyBuilder ScanAssemblies(params string[] assemblyNames)
    {
        _innerBuilder.ScanAssemblies(assemblyNames);
        return this;
    }
}

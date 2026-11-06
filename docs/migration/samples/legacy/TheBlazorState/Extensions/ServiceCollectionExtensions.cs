using TheBlazorState.Configuration;
using TheBlazorState.Services;
using TheBlazorState.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace TheBlazorState.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers TheBlazorState services: StateManager, storage strategies, and options.
    /// </summary>
    public static IServiceCollection AddTheBlazorState(
        this IServiceCollection services,
        Action<TheBlazorStateOptions>? configure = null)
    {
        var options = new TheBlazorStateOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<CrossTabHub>();
        services.AddMemoryCache();
        services.AddScoped<BrowserStorageService>();
        services.AddScoped<CrossTabSyncService>();
        services.AddScoped<StateManager>();
        services.AddScoped<StorageStrategyInitializer>();

        return services;
    }
}

/// <summary>
/// Scoped service that initializes storage strategy singletons with their DI dependencies.
/// Resolved automatically when StateManager is created.
/// </summary>
public sealed class StorageStrategyInitializer
{
    public StorageStrategyInitializer(
        BrowserStorageService browserStorage,
        IMemoryCache cache)
    {
        SessionStorageStrategy.Instance.Initialize(browserStorage);
        LocalStorageStrategy.Instance.Initialize(browserStorage);
        IndexedDbStrategy.Instance.Initialize(browserStorage);
        ServerMemoryCacheStrategy.Instance.Initialize(cache);
    }
}

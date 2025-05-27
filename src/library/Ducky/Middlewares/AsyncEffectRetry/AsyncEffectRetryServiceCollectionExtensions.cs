using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Provides extension methods for registering the AsyncEffectRetry middleware and related services.
/// </summary>
public static class AsyncEffectRetryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AsyncEffectRetry middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncEffectRetryMiddleware(this IServiceCollection services)
    {
        services.AddSingleton<AsyncEffectRetryMiddleware>();
        return services;
    }
}

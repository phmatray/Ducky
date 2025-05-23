using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Provides extension methods for registering the AsyncEffect middleware and related services.
/// </summary>
public static class AsyncEffectServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AsyncEffect middleware for the given state type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncEffectMiddleware(this IServiceCollection services)
    {
        services.AddSingleton<AsyncEffectMiddleware>();
        return services;
    }
}

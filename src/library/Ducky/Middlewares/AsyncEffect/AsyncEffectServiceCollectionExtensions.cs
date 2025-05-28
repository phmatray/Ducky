using Ducky.Pipeline;
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
        services.AddScoped<AsyncEffectMiddleware>(sp =>
        {
            return new AsyncEffectMiddleware(
                sp.GetServices<IAsyncEffect>(),
                () => sp.GetRequiredService<DuckyStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<IStoreEventPublisher>()
            );
        });

        services.AddScoped<IActionMiddleware, AsyncEffectMiddleware>(
            sp => sp.GetRequiredService<AsyncEffectMiddleware>());

        return services;
    }

    /// <summary>
    /// Registers an async effect implementation.
    /// </summary>
    /// <typeparam name="TEffect">The type of the async effect to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncEffect<TEffect>(this IServiceCollection services)
        where TEffect : class, IAsyncEffect
    {
        services.AddScoped<IAsyncEffect, TEffect>();
        return services;
    }
}

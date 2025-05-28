using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.TryAddScoped<AsyncEffectMiddleware>(sp =>
        {
            return new AsyncEffectMiddleware(
                sp.GetServices<IAsyncEffect>(),
                () => sp.GetRequiredService<IStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<IStoreEventPublisher>()
            );
        });

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
        services.TryAddScoped<IAsyncEffect, TEffect>();
        return services;
    }
}

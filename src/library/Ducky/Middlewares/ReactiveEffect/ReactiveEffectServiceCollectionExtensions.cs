using Microsoft.Extensions.DependencyInjection;
using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Provides extension methods for registering the ReactiveEffect middleware and related services.
/// </summary>
public static class ReactiveEffectServiceCollectionExtensions
{
    /// <summary>
    /// Registers the ReactiveEffect middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReactiveEffectMiddleware(this IServiceCollection services)
    {
        services.TryAddScoped<ReactiveEffectMiddleware>(sp =>
        {
            return new ReactiveEffectMiddleware(
                sp.GetServices<IReactiveEffect>(),
                () => sp.GetRequiredService<IStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<IStoreEventPublisher>()
            );
        });

        services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<ReactiveEffectMiddleware>());

        return services;
    }

    /// <summary>
    /// Registers a reactive effect.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReactiveEffect<TEffect>(this IServiceCollection services)
        where TEffect : class, IReactiveEffect
    {
        services.TryAddScoped<IReactiveEffect, TEffect>();
        return services;
    }
}

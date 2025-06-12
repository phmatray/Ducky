using Microsoft.Extensions.DependencyInjection;
using Ducky.Pipeline;
using Ducky.Reactive.Middlewares.ReactiveEffects;

// ReSharper disable once CheckNamespace
namespace Ducky.Reactive;

/// <summary>
/// Extension methods for configuring reactive effects in service collection.
/// </summary>
public static class ReactiveServiceCollectionExtensions
{
    /// <summary>
    /// Adds reactive effect middleware to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReactiveEffectMiddleware(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ReactiveEffectMiddleware>();
        services.AddScoped<IMiddleware>(sp => sp.GetRequiredService<ReactiveEffectMiddleware>());

        return services;
    }

    /// <summary>
    /// Adds a reactive effect to the service collection.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect to add.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReactiveEffect<TEffect>(this IServiceCollection services)
        where TEffect : ReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<TEffect>();
        services.AddScoped<ReactiveEffect>(sp => sp.GetRequiredService<TEffect>());

        return services;
    }
}

// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

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

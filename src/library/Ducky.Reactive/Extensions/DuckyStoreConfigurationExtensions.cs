// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Reactive;

/// <summary>
/// Extension methods for configuring Ducky store with reactive effects.
/// </summary>
public static class DuckyStoreConfigurationExtensions
{
    /// <summary>
    /// Adds Ducky store with reactive effects configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the store builder.</param>
    /// <param name="configureEffects">Action to configure reactive effects.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyStoreWithReactiveEffects(
        this IServiceCollection services,
        Action<DuckyBuilder> configure,
        Action<ReactiveEffectsBuilder> configureEffects)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(configureEffects);

        // First configure reactive effects
        services.AddReactiveEffects(configureEffects);

        // Then configure the Ducky store
        services.AddDuckyStore(builder =>
        {
            // Ensure reactive middleware is added
            builder.AddMiddleware<ReactiveEffectMiddleware>();

            // Apply user configuration
            configure(builder);
        });

        return services;
    }
}

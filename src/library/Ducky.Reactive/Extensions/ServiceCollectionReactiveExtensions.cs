// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Reactive;

/// <summary>
/// Extension methods for configuring reactive effects on IServiceCollection.
/// </summary>
public static class ServiceCollectionReactiveExtensions
{
    /// <summary>
    /// Adds reactive effects to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure reactive effects.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddReactiveEffects(
        this IServiceCollection services,
        Action<ReactiveEffectsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ReactiveEffectsBuilder effectsBuilder = new(services);
        configure(effectsBuilder);

        return services;
    }
}

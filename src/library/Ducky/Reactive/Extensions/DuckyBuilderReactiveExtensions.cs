// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Ducky.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Reactive;

/// <summary>
/// Extension methods for adding reactive functionality to DuckyBuilder.
/// </summary>
public static class DuckyBuilderReactiveExtensions
{
    /// <summary>
    /// Adds reactive effects support to the Ducky store.
    /// </summary>
    /// <param name="builder">The Ducky store builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DuckyBuilder AddReactiveEffects(this DuckyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Add the reactive effect middleware
        builder.AddMiddleware<ReactiveEffectMiddleware>();

        return builder;
    }

    /// <summary>
    /// Adds reactive effects support to the Ducky store with configuration.
    /// </summary>
    /// <param name="builder">The Ducky store builder.</param>
    /// <param name="configure">Action to configure reactive effects.</param>
    /// <returns>The builder for chaining.</returns>
    public static DuckyBuilder AddReactiveEffects(
        this DuckyBuilder builder,
        Action<ReactiveEffectsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Add the reactive effect middleware
        builder.AddMiddleware<ReactiveEffectMiddleware>();

        // Apply the configuration to register effects in DI
        ReactiveEffectsBuilder effectsBuilder = new(builder.Services);
        configure(effectsBuilder);

        return builder;
    }

    /// <summary>
    /// Adds a single reactive effect to the store.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect.</typeparam>
    /// <param name="builder">The Ducky store builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static DuckyBuilder AddReactiveEffect<TEffect>(this DuckyBuilder builder)
        where TEffect : ReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Auto-add ReactiveEffectMiddleware if not already present
        builder.AddMiddleware<ReactiveEffectMiddleware>();

        // Register the effect in DI (mirrors AddEffect<T> pattern)
        builder.Services.AddScoped<TEffect>();
        builder.Services.AddScoped<ReactiveEffect>(sp => sp.GetRequiredService<TEffect>());

        return builder;
    }
}

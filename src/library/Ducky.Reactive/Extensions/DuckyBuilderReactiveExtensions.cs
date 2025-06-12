// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Builder;

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
    /// <remarks>
    /// Important: This method only ensures the middleware is added. To actually register effects,
    /// you must configure them in the IServiceCollection before calling AddDuckyStore.
    /// Consider using AddDuckyStoreWithReactiveEffects extension method instead.
    /// </remarks>
    public static DuckyBuilder AddReactiveEffects(
        this DuckyBuilder builder,
        Action<ReactiveEffectsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Add the reactive effect middleware
        builder.AddMiddleware<ReactiveEffectMiddleware>();

        // WARNING: The configure action cannot be applied here because DuckyBuilder
        // doesn't expose IServiceCollection. Effects must be registered separately.
        // This overload exists for API compatibility but has limitations.

        return builder;
    }

    /// <summary>
    /// Adds a single reactive effect to the store.
    /// </summary>
    /// <typeparam name="TEffect">The type of reactive effect.</typeparam>
    /// <param name="builder">The Ducky store builder.</param>
    /// <returns>The builder for chaining.</returns>
    /// <remarks>
    /// Note: This method only ensures the middleware is added. The actual effect registration
    /// must be done separately in the service collection before calling AddDuckyStore.
    /// </remarks>
    public static DuckyBuilder AddReactiveEffect<TEffect>(this DuckyBuilder builder)
        where TEffect : ReactiveEffect
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Ensure middleware is added
        builder.AddMiddleware<ReactiveEffectMiddleware>();

        return builder;
    }
}

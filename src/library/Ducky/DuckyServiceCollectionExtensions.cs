// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky;

/// <summary>
/// Extension methods for configuring Ducky services.
/// </summary>
public static class DuckyServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ducky services with sensible defaults and optional configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// // Basic setup with defaults
    /// services.AddDucky();
    /// 
    /// // With configuration
    /// services.AddDucky(builder => builder
    ///     .AddEffect&lt;MyEffect&gt;()
    ///     .AddSlice&lt;MyState&gt;()
    ///     .EnableDiagnostics());
    /// </code>
    /// </example>
    public static IServiceCollection AddDucky(
        this IServiceCollection services,
        Action<DuckyBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        DuckyBuilder builder = new(services);

        // Apply defaults
        builder.UseDefaultMiddlewares();

        // Apply user configuration
        configure?.Invoke(builder);

        // Build and return
        return builder.Build();
    }

    /// <summary>
    /// Adds Ducky services with the builder pattern (alias for AddDucky).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the store builder.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDuckyStore(builder => builder
    ///     .UseDefaultMiddlewares()
    ///     .AddSlice&lt;MyState&gt;()
    ///     .AddEffect&lt;MyEffect&gt;());
    /// </code>
    /// </example>
    public static IServiceCollection AddDuckyStore(
        this IServiceCollection services,
        Action<DuckyBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        return services.AddDucky(configure);
    }
}

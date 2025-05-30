// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>
/// Extension methods for configuring Ducky in Blazor applications.
/// </summary>
public static class DuckyBlazorExtensions
{
    /// <summary>
    /// Adds Ducky with the recommended middleware configuration for Blazor applications.
    /// Includes: CorrelationId, JsLogging, AsyncEffect, and ReactiveEffect middlewares.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configureStore">Optional store builder configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyBlazor(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IStoreBuilder>? configureStore = null)
    {
        // Add Blazor-specific time provider
        services.AddBlazorTimeProvider();

        return services.AddDuckyStore(builder =>
        {
            // Add default Blazor middlewares
            builder
                .AddCorrelationIdMiddleware()
                .AddMiddleware<Middlewares.JsLogging.JsLoggingMiddleware>()
                .AddAsyncEffectMiddleware()
                .AddReactiveEffectMiddleware();

            // Apply any additional configuration
            configureStore?.Invoke(builder);
        });
    }
}

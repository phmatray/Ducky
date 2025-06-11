// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor;

/// <summary>
/// Extension methods for adding Ducky services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Adds Ducky with the recommended middleware configuration for Blazor applications.
    /// Includes: CorrelationId, JsLogging, AsyncEffect, and ReactiveEffect middlewares.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureStore">Optional store builder configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyBlazor(
        this IServiceCollection services,
        Action<IStoreBuilder>? configureStore = null)
    {
        return services.AddDuckyStore(builder =>
        {
            // Add default Blazor middlewares
            builder
                .AddCorrelationIdMiddleware()
                .AddMiddleware<JsLoggingMiddleware>()
                .AddAsyncEffectMiddleware();

            // Apply any additional configuration
            configureStore?.Invoke(builder);
        });
    }
}

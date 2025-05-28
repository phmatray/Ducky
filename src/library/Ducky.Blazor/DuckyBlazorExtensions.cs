// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Ducky.Blazor.Middlewares.JsLogging;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;
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
    /// Includes: CorrelationId, JsLogging, AsyncEffect, AsyncEffectRetry, and ReactiveEffect middlewares.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <param name="configurePipeline">Optional additional pipeline configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDuckyBlazor(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ActionPipeline, IServiceProvider>? configurePipeline = null)
    {
        // Register all middleware services first
        services.AddCorrelationIdMiddleware();
        services.AddJsLoggingMiddleware();
        services.AddAsyncEffectMiddleware();
        services.AddReactiveEffectMiddleware();

        return services.AddDuckyWithPipeline(
            configuration,
            (pipeline, serviceProvider) =>
            {
                // Standard middleware pipeline for Blazor
                pipeline.Use(serviceProvider.GetRequiredService<CorrelationIdMiddleware>());
                pipeline.Use(serviceProvider.GetRequiredService<JsLoggingMiddleware>());
                pipeline.Use(serviceProvider.GetRequiredService<AsyncEffectMiddleware>());
                pipeline.Use(serviceProvider.GetRequiredService<ReactiveEffectMiddleware>());

                // Apply any additional configuration
                configurePipeline?.Invoke(pipeline, serviceProvider);
            });
    }
}

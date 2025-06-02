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
    /// Adds Ducky services with a store builder for robust configuration.
    /// This is the recommended way to configure Ducky services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The store builder configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddDuckyStore(builder => builder
    ///     .AddCorrelationIdMiddleware()
    ///     .AddExceptionHandlingMiddleware()
    ///     .AddAsyncEffectMiddleware()
    ///     .AddEffect&lt;MyEffect&gt;()
    ///     .AddSlice&lt;MyState&gt;());
    /// </code>
    /// </example>
    public static IServiceCollection AddDuckyStore(
        this IServiceCollection services,
        Action<IStoreBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        StoreBuilder storeBuilder = new(services);
        configure(storeBuilder);

        // Extract middleware types from the builder
        List<Type> middlewareTypes = storeBuilder.GetMiddlewareTypes();

        DuckyOptions options = new()
        {
            ConfigurePipelineWithServices = (pipeline, _) =>
            {
                foreach (Type middlewareType in middlewareTypes)
                {
                    pipeline.Use(middlewareType);
                }
            }
        };

        return services.AddDuckyCore(options);
    }
}

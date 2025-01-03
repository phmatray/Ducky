// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace Ducky.Blazor;

/// <summary>
/// Extension methods for adding Ducky services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Adds Ducky services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration (optional).</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="DuckyOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddDucky(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<DuckyOptions>? configureOptions = null)
    {
        // Configure options
        DuckyOptions options = new();

        configuration?.GetSection("Ducky").Bind(options); // Bind configuration section to DuckyOptions
        configureOptions?.Invoke(options);

        // Add Reactive Extensions for Blazor if configuration is provided
        if (configuration is not null)
        {
            services.AddScoped<TimeProvider, SynchronizationContextTimeProvider>();
        }

        return services.AddDuckyCore(options);
    }
}

// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using R3;

namespace R3dux.Blazor;

/// <summary>
/// Extension methods for adding R3dux services to the dependency injection container.
/// </summary>
public static class DependencyInjections
{
    /// <summary>
    /// Adds R3dux services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddR3dux(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<R3duxOptions>? configureOptions = null)
    {
        // Configure options
        R3duxOptions options = new();
        configuration.GetSection("R3dux").Bind(options); // Bind configuration section to R3duxOptions
        configureOptions?.Invoke(options);

        // Add Reactive Extensions for Blazor
        services.AddBlazorR3();

        return services.AddR3duxCore(options);
    }

    /// <summary>
    /// Adds R3dux services to the specified <see cref="IServiceCollection"/>. This method registers the BlazorR3 services, dispatcher, slices, and effects.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configureOptions">An optional action to configure the <see cref="R3duxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection AddR3dux(
        this IServiceCollection services,
        Action<R3duxOptions>? configureOptions = null)
    {
        // Configure options
        R3duxOptions options = new();
        configureOptions?.Invoke(options);

        // Add Reactive Extensions for Blazor
        services.AddBlazorR3();

        return services.AddR3duxCore(options);
    }
}

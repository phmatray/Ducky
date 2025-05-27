// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Ducky.Abstractions;

namespace Ducky.Middlewares.ExceptionHandling;

/// <summary>
/// Provides extension methods for registering exception handling middleware and services.
/// </summary>
public static class ExceptionHandlingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the exception handling middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExceptionHandlingMiddleware(this IServiceCollection services)
    {
        return services.AddSingleton<ExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Registers an exception handler.
    /// </summary>
    /// <typeparam name="THandler">The type of exception handler to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExceptionHandler<THandler>(this IServiceCollection services)
        where THandler : class, IExceptionHandler
    {
        return services.AddSingleton<IExceptionHandler, THandler>();
    }

    /// <summary>
    /// Registers an exception handler with a factory function.
    /// </summary>
    /// <typeparam name="THandler">The type of exception handler to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">The factory function to create the handler.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExceptionHandler<THandler>(
        this IServiceCollection services,
        Func<IServiceProvider, THandler> factory)
        where THandler : class, IExceptionHandler
    {
        return services.AddSingleton<IExceptionHandler>(factory);
    }
}

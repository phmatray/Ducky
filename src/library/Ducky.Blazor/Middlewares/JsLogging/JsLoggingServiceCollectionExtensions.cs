using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Provides extension methods for registering the JS logging middleware and related services.
/// </summary>
public static class JsLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JS logging middleware and its dependencies for the given state type.
    /// </summary>
    /// <typeparam name="TState">The type of the state to log.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJsLoggingMiddleware<TState>(this IServiceCollection services)
        where TState : class
    {
        services.AddSingleton<JsConsoleLoggerModule>();
        services.AddSingleton<JsLoggingMiddleware<TState>>();
        return services;
    }
}

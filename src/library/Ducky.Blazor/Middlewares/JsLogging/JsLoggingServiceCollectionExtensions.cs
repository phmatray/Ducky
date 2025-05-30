using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Blazor.Middlewares.JsLogging;

/// <summary>
/// Provides extension methods for registering the JS logging middleware and related services.
/// </summary>
public static class JsLoggingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JS logging middleware and its dependencies for the given state type.
    /// </summary>
    public static IServiceCollection AddJsLoggingMiddleware(this IServiceCollection services)
    {
        services.TryAddScoped<JsConsoleLoggerModule>();
        services.TryAddScoped<JsLoggingMiddleware>(sp =>
            new JsLoggingMiddleware(
                sp.GetRequiredService<JsConsoleLoggerModule>(),
                () => sp.GetRequiredService<IStore>().CurrentState)
        );

        services.AddScoped<IActionMiddleware>(sp => sp.GetRequiredService<JsLoggingMiddleware>());

        return services;
    }
}

using Ducky.Pipeline;
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
    public static IServiceCollection AddJsLoggingMiddleware(this IServiceCollection services)
    {
        services.AddScoped<JsConsoleLoggerModule>();
        services.AddScoped<JsLoggingMiddleware>(sp =>
            new JsLoggingMiddleware(
                sp.GetRequiredService<JsConsoleLoggerModule>(),
                () => sp.GetRequiredService<IStore>().CurrentState)
        );

        services.AddScoped<IActionMiddleware, JsLoggingMiddleware>(
            sp => sp.GetRequiredService<JsLoggingMiddleware>());

        return services;
    }
}

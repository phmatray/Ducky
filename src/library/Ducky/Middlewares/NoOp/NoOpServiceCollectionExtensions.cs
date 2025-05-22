using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Middlewares.NoOp;

/// <summary>
/// Provides extension methods for registering the NoOp middleware and related services.
/// </summary>
public static class NoOpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the NoOp middleware.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNoOpMiddleware(this IServiceCollection services)
    {
        services.AddSingleton<NoOpMiddleware>();
        return services;
    }
}

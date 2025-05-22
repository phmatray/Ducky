using Microsoft.Extensions.DependencyInjection;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Provides extension methods for registering the AsyncEffect middleware and related services.
/// </summary>
public static class AsyncEffectServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AsyncEffect middleware for the given state type.
    /// </summary>
    /// <typeparam name="TState">The type of the state to use with the middleware.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncEffectMiddleware<TState>(this IServiceCollection services)
        where TState : class
    {
        services.AddSingleton<AsyncEffectMiddleware<TState>>();
        return services;
    }
}

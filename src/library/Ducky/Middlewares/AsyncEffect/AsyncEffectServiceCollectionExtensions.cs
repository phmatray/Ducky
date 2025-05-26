using Ducky.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ducky.Middlewares.AsyncEffect;

/// <summary>
/// Provides extension methods for registering the AsyncEffect middleware and related services.
/// </summary>
public static class AsyncEffectServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AsyncEffect middleware for the given state type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncEffectMiddleware(this IServiceCollection services)
    {
        services.AddScoped<AsyncEffectMiddleware>(sp =>
        {
            return new AsyncEffectMiddleware(
                sp,
                () => sp.GetRequiredService<DuckyStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>()
            );
        });

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IActionMiddleware, AsyncEffectMiddleware>());

        return services;
    }
}

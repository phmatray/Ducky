using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Middlewares.NoOp;

namespace Ducky.Builder;

/// <summary>
/// Provides preset configurations for common scenarios.
/// </summary>
public static class StoreBuilderPresets
{
    /// <summary>
    /// Configures the store for production use with essential middlewares.
    /// Includes: CorrelationId, ExceptionHandling, AsyncEffect, and ReactiveEffect.
    /// </summary>
    public static IStoreBuilder UseProductionPreset(this IStoreBuilder builder)
    {
        return builder
            .AddCorrelationIdMiddleware()
            .AddExceptionHandlingMiddleware()
            .AddAsyncEffectMiddleware()
            .AddReactiveEffectMiddleware();
    }

    /// <summary>
    /// Configures the store for development with debugging and diagnostic features.
    /// Includes all production middlewares plus additional development tools.
    /// </summary>
    public static IStoreBuilder UseDevelopmentPreset(this IStoreBuilder builder)
    {
        return builder
            .UseProductionPreset()
            .ConfigureOptions(options =>
            {
                // Could configure development-specific options here
            });
    }

    /// <summary>
    /// Configures the store for unit testing with minimal setup.
    /// Includes only essential middlewares for testing.
    /// </summary>
    public static IStoreBuilder UseTestingPreset(this IStoreBuilder builder)
    {
        return builder
            .AddExceptionHandlingMiddleware()
            .AddAsyncEffectMiddleware();
    }

    /// <summary>
    /// Configures the store with all available middlewares.
    /// Useful for exploring features or comprehensive testing.
    /// </summary>
    public static IStoreBuilder UseAllMiddlewares(this IStoreBuilder builder)
    {
        return builder
            .AddCorrelationIdMiddleware()
            .AddExceptionHandlingMiddleware()
            .AddAsyncEffectMiddleware()
            .AddReactiveEffectMiddleware()
            .AddNoOpMiddleware();
    }

    /// <summary>
    /// Configures the store with minimal overhead for performance-critical scenarios.
    /// Only includes absolutely essential middlewares.
    /// </summary>
    public static IStoreBuilder UseMinimalPreset(this IStoreBuilder builder)
    {
        return builder
            .AddExceptionHandlingMiddleware()
            .ConfigureOptions(options =>
            {
                // Minimal configuration
            });
    }
}


using Microsoft.Extensions.DependencyInjection;
using Ducky.Middlewares.AsyncEffect;
using Ducky.Middlewares.CorrelationId;
using Ducky.Middlewares.ExceptionHandling;
using Ducky.Middlewares.NoOp;
using Ducky.Middlewares.ReactiveEffect;
using Ducky.Pipeline;

namespace Ducky.Builder;

/// <summary>
/// Extension methods for <see cref="IStoreBuilder"/> to provide fluent middleware configuration.
/// </summary>
public static class StoreBuilderExtensions
{
    /// <summary>
    /// Adds the correlation ID middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddCorrelationIdMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Adds the exception handling middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddExceptionHandlingMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMiddleware<ExceptionHandlingMiddleware>();
    }

    /// <summary>
    /// Adds the no-op middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddNoOpMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMiddleware<NoOpMiddleware>();
    }

    /// <summary>
    /// Adds the async effect middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddAsyncEffectMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMiddleware<AsyncEffectMiddleware>(sp =>
            new AsyncEffectMiddleware(
                sp.GetServices<IAsyncEffect>(),
                () => sp.GetRequiredService<IStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<IStoreEventPublisher>()));
    }

    /// <summary>
    /// Adds the reactive effect middleware to the store pipeline.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddReactiveEffectMiddleware(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMiddleware<ReactiveEffectMiddleware>(sp =>
            new ReactiveEffectMiddleware(
                sp.GetServices<IReactiveEffect>(),
                () => sp.GetRequiredService<IStore>().CurrentState,
                sp.GetRequiredService<IDispatcher>(),
                sp.GetRequiredService<IStoreEventPublisher>()));
    }

    /// <summary>
    /// Adds the default set of middlewares to the store pipeline.
    /// Includes: CorrelationId, ExceptionHandling, AsyncEffect, and ReactiveEffect middlewares.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder AddDefaultMiddlewares(this IStoreBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .AddCorrelationIdMiddleware()
            .AddExceptionHandlingMiddleware()
            .AddAsyncEffectMiddleware()
            .AddReactiveEffectMiddleware();
    }

    /// <summary>
    /// Configures the store options.
    /// </summary>
    /// <param name="builder">The store builder.</param>
    /// <param name="configureOptions">The action to configure options.</param>
    /// <returns>The store builder for chaining.</returns>
    public static IStoreBuilder ConfigureStore(this IStoreBuilder builder, Action<DuckyOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureOptions);

        return builder.ConfigureOptions(configureOptions);
    }
}

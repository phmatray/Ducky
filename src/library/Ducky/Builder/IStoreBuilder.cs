using Microsoft.Extensions.DependencyInjection;
using Ducky.Pipeline;

namespace Ducky.Builder;

/// <summary>
/// Provides a fluent interface for configuring a Ducky store with middleware, slices, effects, and exception handlers.
/// </summary>
public interface IStoreBuilder
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Adds a middleware to the store pipeline.
    /// </summary>
    /// <typeparam name="TMiddleware">The type of middleware to add.</typeparam>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddMiddleware<TMiddleware>() where TMiddleware : class, IMiddleware;

    /// <summary>
    /// Adds a middleware to the store pipeline with a factory function.
    /// </summary>
    /// <typeparam name="TMiddleware">The type of middleware to add.</typeparam>
    /// <param name="implementationFactory">The factory function to create the middleware.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddMiddleware<TMiddleware>(Func<IServiceProvider, TMiddleware> implementationFactory)
        where TMiddleware : class, IMiddleware;

    /// <summary>
    /// Adds a middleware to the store pipeline by type.
    /// </summary>
    /// <param name="middlewareType">The type of middleware to add.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddMiddleware(Type middlewareType);

    /// <summary>
    /// Adds a middleware to the store pipeline by type with a factory function.
    /// </summary>
    /// <param name="middlewareType">The type of middleware to add.</param>
    /// <param name="implementationFactory">The factory function to create the middleware.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddMiddleware(Type middlewareType, Func<IServiceProvider, object> implementationFactory);

    /// <summary>
    /// Configures options for the Ducky store.
    /// </summary>
    /// <param name="configureOptions">The action to configure options.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder ConfigureOptions(Action<DuckyOptions> configureOptions);

    /// <summary>
    /// Adds a state slice to the store.
    /// </summary>
    /// <typeparam name="TState">The type of state to add.</typeparam>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddSlice<TState>() where TState : class, IState, new();

    /// <summary>
    /// Adds a state slice to the store with a factory function.
    /// </summary>
    /// <typeparam name="TState">The type of state to add.</typeparam>
    /// <param name="stateFactory">The factory function to create the initial state.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddSlice<TState>(Func<IServiceProvider, TState> stateFactory) where TState : class, IState;

    /// <summary>
    /// Adds an async effect to the store.
    /// </summary>
    /// <typeparam name="TEffect">The type of effect to add.</typeparam>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddEffect<TEffect>() where TEffect : class, IAsyncEffect;

    /// <summary>
    /// Adds an async effect to the store with a factory function.
    /// </summary>
    /// <typeparam name="TEffect">The type of effect to add.</typeparam>
    /// <param name="implementationFactory">The factory function to create the effect.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddEffect<TEffect>(Func<IServiceProvider, TEffect> implementationFactory)
        where TEffect : class, IAsyncEffect;

    /// <summary>
    /// Adds an exception handler to the store.
    /// </summary>
    /// <typeparam name="TExceptionHandler">The type of exception handler to add.</typeparam>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddExceptionHandler<TExceptionHandler>() where TExceptionHandler : class, IExceptionHandler;

    /// <summary>
    /// Adds an exception handler to the store with a factory function.
    /// </summary>
    /// <typeparam name="TExceptionHandler">The type of exception handler to add.</typeparam>
    /// <param name="implementationFactory">The factory function to create the exception handler.</param>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder AddExceptionHandler<TExceptionHandler>(Func<IServiceProvider, TExceptionHandler> implementationFactory)
        where TExceptionHandler : class, IExceptionHandler;

    /// <summary>
    /// Enables middleware diagnostics for performance monitoring.
    /// </summary>
    /// <returns>The store builder for chaining.</returns>
    IStoreBuilder EnableDiagnostics();
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ducky.Pipeline;

/// <summary>
/// Manages the execution of actions through middleware lifecycle events.
/// This replaces the previous reactive pipeline with a simpler lifecycle-based approach.
/// </summary>
public sealed class ActionPipeline : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActionPipeline> _logger;
    private readonly List<IMiddleware> _middlewares = [];
    private readonly List<Type> _middlewareTypes = [];
    private bool _disposed;
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of the ActionPipeline.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="logger">The logger for diagnostics.</param>
    public ActionPipeline(
        IServiceProvider serviceProvider,
        ILogger<ActionPipeline> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }


    /// <summary>
    /// Adds a middleware type to the pipeline.
    /// </summary>
    /// <param name="middlewareType">The type of middleware to add.</param>
    /// <returns>The current pipeline for chaining.</returns>
    public ActionPipeline Use(Type middlewareType)
    {
        if (!typeof(IMiddleware).IsAssignableFrom(middlewareType))
        {
            throw new ArgumentException($"Type {middlewareType.Name} does not implement IMiddleware", nameof(middlewareType));
        }

        _middlewareTypes.Add(middlewareType);
        _logger.LogDebug("Added middleware {MiddlewareType} to pipeline", middlewareType.Name);
        return this;
    }

    /// <summary>
    /// Initializes all middlewares. Should be called by the store after all middlewares are added.
    /// </summary>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="store">The store instance.</param>
    public async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        if (_initialized)
        {
            throw new InvalidOperationException("Pipeline has already been initialized");
        }

        _logger.LogDebug("Initializing action pipeline with {MiddlewareCount} middlewares", _middlewareTypes.Count);

        // Create middleware instances
        foreach (Type middlewareType in _middlewareTypes)
        {
            // First try to get from DI container, fall back to creating new instance
            IMiddleware middleware = _serviceProvider.GetService(middlewareType) as IMiddleware 
                ?? (IMiddleware)ActivatorUtilities.CreateInstance(_serviceProvider, middlewareType);
            _middlewares.Add(middleware);
            await middleware.InitializeAsync(dispatcher, store).ConfigureAwait(false);
        }

        // Call AfterInitializeAllMiddlewares on all middlewares
        foreach (IMiddleware middleware in _middlewares)
        {
            middleware.AfterInitializeAllMiddlewares();
        }

        _initialized = true;
        _logger.LogInformation("Action pipeline initialized successfully");
    }

    /// <summary>
    /// Checks if the action may be dispatched.
    /// </summary>
    /// <param name="action">The action to check.</param>
    /// <returns>True if all middlewares allow the action, false if any middleware prevents it.</returns>
    public bool MayDispatchAction(object action)
    {
        foreach (IMiddleware middleware in _middlewares)
        {
            if (!middleware.MayDispatchAction(action))
            {
                _logger.LogDebug(
                    "Action {ActionType} was prevented by middleware {MiddlewareType}",
                    action.GetType().Name,
                    middleware.GetType().Name);
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Calls BeforeReduce on all middlewares.
    /// </summary>
    /// <param name="action">The action being reduced.</param>
    public void BeforeReduce(object action)
    {
        foreach (IMiddleware middleware in _middlewares)
        {
            middleware.BeforeReduce(action);
        }
    }

    /// <summary>
    /// Calls AfterReduce on all middlewares.
    /// </summary>
    /// <param name="action">The action that has been reduced.</param>
    public void AfterReduce(object action)
    {
        foreach (IMiddleware middleware in _middlewares)
        {
            middleware.AfterReduce(action);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Note: If middlewares need disposal, they should implement IDisposable
        // and be registered with the DI container for proper disposal

        _logger.LogDebug("Action pipeline disposed");
    }
}

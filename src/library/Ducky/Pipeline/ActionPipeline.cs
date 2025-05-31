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
    /// <typeparam name="TMiddleware">The type of middleware to add.</typeparam>
    /// <returns>The current pipeline for chaining.</returns>
    public ActionPipeline Use<TMiddleware>() where TMiddleware : class, IMiddleware
    {
        return Use(typeof(TMiddleware));
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
            var middleware = (IMiddleware)ActivatorUtilities.CreateInstance(_serviceProvider, middlewareType);
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
    /// Processes an action through all middleware lifecycle events.
    /// </summary>
    /// <param name="action">The action to process.</param>
    /// <returns>True if the action was processed, false if it was prevented.</returns>
    public bool ProcessAction(object action)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ActionPipeline));
        }

        if (!_initialized)
        {
            throw new InvalidOperationException("Pipeline must be initialized before processing actions");
        }

        _logger.LogTrace("Processing action {ActionType} through pipeline", action.GetType().Name);

        try
        {
            // Check if any middleware wants to prevent this action
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

            // Call BeforeDispatch on all middlewares
            foreach (IMiddleware middleware in _middlewares)
            {
                middleware.BeforeDispatch(action);
            }

            // Action processing happens here (handled by DuckyStore)
            // This is where slice reducers are executed

            // Call AfterDispatch on all middlewares
            foreach (IMiddleware middleware in _middlewares)
            {
                middleware.AfterDispatch(action);
            }

            _logger.LogTrace("Completed processing action {ActionType}", action.GetType().Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing action {ActionType}", action.GetType().Name);
            throw;
        }
    }

    /// <summary>
    /// Begins an internal middleware change for all middlewares.
    /// </summary>
    /// <returns>A disposable that ends the change when disposed.</returns>
    public IDisposable BeginInternalMiddlewareChange()
    {
        List<IDisposable> disposables = _middlewares.Select(m => m.BeginInternalMiddlewareChange()).ToList();
        return new DisposableCallback(() =>
        {
            foreach (IDisposable disposable in disposables)
            {
                disposable.Dispose();
            }
        });
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

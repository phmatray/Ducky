using Ducky.Pipeline;

namespace Ducky.Middlewares;

/// <summary>
/// Base class for middleware providing default sync/async bridging.
/// </summary>
public abstract class StoreMiddleware : IStoreMiddleware
{
    private IDispatcher? _dispatcher;
    private IStore? _store;
    private IPipelineEventPublisher? _events;

    /// <summary>
    /// Gets the dispatcher.
    /// </summary>
    /// <exception cref="DuckyException">Thrown when the middleware is not initialized.</exception>
    protected IDispatcher Dispatcher
        => _dispatcher ?? throw new DuckyException("Middleware not initialized.");

    /// <summary>
    /// Gets the store.
    /// </summary>
    /// <exception cref="DuckyException">Thrown when the middleware is not initialized.</exception>
    protected IStore Store
        => _store ?? throw new DuckyException("Middleware not initialized.");

    /// <summary>
    /// Gets the event publisher.
    /// </summary>
    /// <exception cref="DuckyException">Thrown when the middleware is not initialized.</exception>
    protected IPipelineEventPublisher Events
        => _events ?? throw new DuckyException("Middleware not initialized.");

    /// <inheritdoc />
    public virtual StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.Await;

    /// <inheritdoc />
    public virtual async Task InitializeAsync(
        IDispatcher dispatcher,
        IStore store,
        IPipelineEventPublisher eventPublisher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _events = eventPublisher ?? throw new ArgumentNullException(nameof(eventPublisher));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual bool CanHandle(Type actionType)
        => true;

    /// <inheritdoc />
    public virtual async Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        Before(context);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public virtual async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        After(context);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Executes before the action is processed (sync version).
    /// </summary>
    public virtual void Before<TAction>(ActionContext<TAction> context)
    {
    }

    /// <summary>
    /// Executes after the action is processed (sync version).
    /// </summary>
    public virtual void After<TAction>(ActionContext<TAction> context)
    {
    }
}

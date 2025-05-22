using Ducky.Pipeline;

namespace Ducky.Middlewares;

/// <summary>
/// Base class for middleware providing default sync/async bridging.
/// </summary>
public abstract class StoreMiddleware : IStoreMiddleware
{
    /// <inheritdoc />
    public virtual StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.Await;

    /// <inheritdoc />
    public virtual Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        // No-op
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual bool CanHandle(Type actionType)
        => true;

    /// <inheritdoc />
    public virtual Task BeforeDispatchAsync<TAction>(ActionContext<TAction> context, CancellationToken cancellationToken = default)
    {
        Before(context);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task AfterDispatchAsync<TAction>(ActionContext<TAction> context, CancellationToken cancellationToken = default)
    {
        After(context);
        return Task.CompletedTask;
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

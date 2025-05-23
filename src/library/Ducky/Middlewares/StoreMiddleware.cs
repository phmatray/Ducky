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
    public virtual async Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        // No-op
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

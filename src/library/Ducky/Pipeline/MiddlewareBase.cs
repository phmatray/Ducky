namespace Ducky.Pipeline;

/// <summary>
/// Base class for middleware implementations that provides common functionality.
/// </summary>
public abstract class MiddlewareBase : IMiddleware
{
    /// <inheritdoc />
    public virtual Task InitializeAsync(IDispatcher dispatcher, IStore store)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual void AfterInitializeAllMiddlewares()
    {
        // Default implementation does nothing
    }

    /// <inheritdoc />
    public virtual bool MayDispatchAction(object action)
    {
        // Default implementation allows all actions
        return true;
    }

    /// <inheritdoc />
    public virtual void BeforeDispatch(object action)
    {
        // Default implementation does nothing
    }

    /// <inheritdoc />
    public virtual void AfterDispatch(object action)
    {
        // Default implementation does nothing
    }

    /// <inheritdoc />
    public virtual IDisposable BeginInternalMiddlewareChange()
    {
        // Default implementation returns a no-op disposable
        return new DisposableCallback(() => { });
    }
}

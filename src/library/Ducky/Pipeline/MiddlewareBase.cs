namespace Ducky.Pipeline;

/// <summary>
/// Base class for middleware implementations that provides common functionality.
/// </summary>
public abstract class MiddlewareBase : IMiddleware
{
    /// <inheritdoc />
    public abstract Task InitializeAsync(IDispatcher dispatcher, IStore store);

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
    public abstract void BeforeDispatch(object action);

    /// <inheritdoc />
    public abstract void AfterDispatch(object action);

    /// <inheritdoc />
    public virtual IDisposable BeginInternalMiddlewareChange()
    {
        // Default implementation returns a no-op disposable
        return new DisposableCallback(() => { });
    }
}

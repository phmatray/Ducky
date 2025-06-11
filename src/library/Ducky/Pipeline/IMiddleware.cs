namespace Ducky.Pipeline;

/// <summary>
/// An interface for implementing middleware
/// </summary>
public interface IMiddleware
{
    /// <summary>
    /// Called exactly once by the store when the store initialises, or when
    /// the middleware is added to the store (if the store has already been initialised)
    /// </summary>
    /// <param name="dispatcher">A reference to the dispatcher</param>
    /// <param name="store">A reference to the store</param>
    Task InitializeAsync(IDispatcher dispatcher, IStore store);

    /// <summary>
    /// Called exactly once by the store after <see cref="InitializeAsync(IDispatcher, IStore)"/> has been
    /// called on all registered Middlewares
    /// </summary>
    void AfterInitializeAllMiddlewares();

    /// <summary>
    /// Called before each action is dispatched
    /// </summary>
    /// <param name="action">The action to be dispatched</param>
    /// <returns>True if the action may proceed, False if it should be prevented</returns>
    bool MayDispatchAction(object action);

    /// <summary>
    /// Called before each action is reduced
    /// </summary>
    /// <param name="action">The action being reduced</param>
    void BeforeReduce(object action);

    /// <summary>
    /// Called after each action has been reduced
    /// </summary>
    /// <param name="action">The action that has just been reduced</param>
    void AfterReduce(object action);

    /// <summary>
    /// This should only be called via IStore.BeginInternalMiddlewareChange.
    /// </summary>
    /// <returns>An IDisposable that should be executed when the internal change ends</returns>
    IDisposable BeginInternalMiddlewareChange();
}

/// <summary>
/// A simple implementation of IDisposable that executes a callback when disposed.
/// </summary>
public class DisposableCallback : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisposableCallback"/> class.
    /// </summary>
    /// <param name="onDispose">The action to execute when disposed.</param>
    public DisposableCallback(Action onDispose)
    {
        _onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _onDispose();
    }
}

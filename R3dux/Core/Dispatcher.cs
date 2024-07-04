using R3;
using R3dux.Exceptions;
using R3dux.Temp;

namespace R3dux;

/// <summary>
/// A dispatcher that queues and dispatches actions, providing an observable stream of dispatched actions.
/// </summary>
public class Dispatcher
    : IDispatcher, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Queue<IAction> _queuedActions = new();
    private readonly Subject<IAction> _actionSubject = new();
    private volatile bool _isDequeuing;
    private bool _disposed;
    
    /// <summary>
    /// Gets an observable stream of dispatched actions.
    /// </summary>
    public Observable<IAction> ActionStream
        => _actionSubject.AsObservable();

    /// <summary>
    /// Dispatches the specified action.
    /// </summary>
    /// <param name="action">The action to dispatch.</param>
    /// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when the dispatcher has been disposed.</exception>
    public void Dispatch(IAction action)
    {
        if (_disposed)
        {
            throw new R3duxException(
                "The dispatcher has been disposed.",
                new ObjectDisposedException(nameof(Dispatcher)));
        }

        ArgumentNullException.ThrowIfNull(action);

        lock (_syncRoot)
        {
            _queuedActions.Enqueue(action);
        }
        
        DequeueActions();
    }

    /// <summary>
    /// Dequeues and dispatches actions to the observable stream.
    /// </summary>
    private void DequeueActions()
    {
        lock (_syncRoot)
        {
            if (_isDequeuing || _actionSubject == null!)
            {
                return;
            }

            _isDequeuing = true;
        }

        while (true)
        {
            IAction dequeuedAction;

            lock (_syncRoot)
            {
                if (_queuedActions.Count == 0)
                {
                    _isDequeuing = false;
                    return;
                }

                dequeuedAction = _queuedActions.Dequeue();
            }

            _actionSubject.OnNext(dequeuedAction);
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="Dispatcher"/> class.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _actionSubject.OnCompleted();
        _actionSubject.Dispose();
    }
}
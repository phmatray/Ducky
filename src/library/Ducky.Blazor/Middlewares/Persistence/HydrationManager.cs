namespace Ducky.Blazor.Middlewares.Persistence;

/// <summary>
/// Manages the hydration process and queues actions that occur during hydration.
/// </summary>
public class HydrationManager
{
    private bool _isHydrating = true;
    private readonly Queue<object> _pendingActions = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Gets a value indicating whether the store is currently hydrating.
    /// </summary>
    public bool IsHydrating
    {
        get
        {
            lock (_lock)
            {
                return _isHydrating;
            }
        }
    }

    /// <summary>
    /// Marks the start of the hydration process.
    /// </summary>
    public void StartHydrating()
    {
        lock (_lock)
        {
            _isHydrating = true;
        }
    }

    /// <summary>
    /// Marks the end of the hydration process.
    /// </summary>
    public void FinishHydrating()
    {
        lock (_lock)
        {
            _isHydrating = false;
        }
    }

    /// <summary>
    /// Enqueues an action to be processed after hydration completes.
    /// </summary>
    /// <param name="action">The action to enqueue.</param>
    public void EnqueueAction(object action)
    {
        lock (_lock)
        {
            _pendingActions.Enqueue(action);
        }
    }

    /// <summary>
    /// Dequeues and returns all actions that were queued during hydration.
    /// </summary>
    /// <returns>An enumerable of all pending actions.</returns>
    public IEnumerable<object> DequeueAll()
    {
        List<object> actions = [];

        lock (_lock)
        {
            while (_pendingActions.Count > 0)
            {
                actions.Add(_pendingActions.Dequeue());
            }
        }

        return actions;
    }
}

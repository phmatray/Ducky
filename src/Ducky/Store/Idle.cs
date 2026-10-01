namespace Ducky;

// SPEC §6.3, §6.7, §7 rule 3: WhenIdleAsync. Idle is Ready, queue empty and not draining (running effects join with
// M2-03). Every waiter shares one TCS, detached under _gate and completed after the lock is released (INV-05); a caller's
// token cancels only its own WaitAsync. Dispose step 1 completes the waiters too (§6.11).
internal sealed partial class Dispatcher
{
    private TaskCompletionSource? _idleWaiters;

    // Starts init first, never under _gate (INV-13), so a store whose inits all complete synchronously is already idle.
    internal Task WhenIdleAsync(CancellationToken cancellationToken)
    {
        StartInit();
        TaskCompletionSource waiters;
        lock (_gate)
        {
            if (_state == StoreState.Disposed || IsIdleLocked())
            {
                return Task.CompletedTask;
            }

            waiters = _idleWaiters ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        return waiters.Task.WaitAsync(cancellationToken);
    }

    // Not draining implies an empty queue: every enqueue on _queue starts a drain under _gate, and a drain releases only
    // once the queue is empty, in the same critical section (INV-03).
    private bool IsIdleLocked() => _state == StoreState.Ready && !_draining;

    // Detaches the waiters; completes nothing (the caller does, outside the lock).
    private TaskCompletionSource? TakeIdleWaitersIfIdleLocked()
    {
        if (!IsIdleLocked())
        {
            return null;
        }

        var waiters = _idleWaiters;
        _idleWaiters = null;
        return waiters;
    }
}

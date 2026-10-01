using System.Diagnostics.CodeAnalysis;

namespace Ducky;

// SPEC §6.11 steps 1-3 (INV-29). Every caller gets the one disposal task, published before any step runs, so a call
// made re-entrantly from step 2's callbacks starts nothing. Steps 4-6 (materialization, middleware, effect runs,
// subscribers) come with those features; when step 3 times out they chain on the drain's exit.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The lifetime CTS is never disposed: tokens taken from it may be read after disposal, and it owns no timer or linked registration.")]
internal sealed partial class Dispatcher
{
    private const long MaxDisposeTimeoutTicks = (uint.MaxValue - 1L) * TimeSpan.TicksPerMillisecond;

    private readonly CancellationTokenSource _lifetime = new();

    // Task.WaitAsync accepts only Timeout.InfiniteTimeSpan or [0, uint.MaxValue - 1 ms]: a negative DisposeTimeout waits
    // not at all and a larger one (TimeSpan.MaxValue meaning "forever") waits the longest bound, so step 3 never throws.
    private readonly TimeSpan _disposeTimeout = disposeTimeout == Timeout.InfiniteTimeSpan
        ? disposeTimeout
        : TimeSpan.FromTicks(Math.Clamp(disposeTimeout.Ticks, 0, MaxDisposeTimeoutTicks));

    private TaskCompletionSource? _disposal;

    // The store lifetime, cancelled at step 2: effect runs and init link their tokens to it.
    internal CancellationToken Lifetime => _lifetime.Token;

    // Never faults: nothing below throws but a fatal exception (SafeLogger rethrows only OutOfMemoryException).
    internal Task DisposeAsync()
    {
        var disposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = Interlocked.CompareExchange(ref _disposal, disposal, null);
        if (published is not null)
        {
            return published.Task;
        }

        _ = RunDisposalAsync(disposal);
        return disposal.Task;
    }

    // Sync containers call this; every ordering rule comes from DisposeAsync.
    internal void Dispose()
    {
        _ = DisposeAsync();
        Log.SyncDispose(logger);
    }

    private async Task RunDisposalAsync(TaskCompletionSource disposal)
    {
        // The finally completes the shared task on every path, so no exception can leave a DisposeAsync caller waiting.
        try
        {
            var exited = Detach();
            CancelLifetime();

            // Step 3, also when the caller is the drainer: it gets an incomplete task, and the drainer, finding the queue
            // empty, exits. A drain that outlasts DisposeTimeout no longer holds DisposeAsync.
            if (exited is not null)
            {
                var wait = exited.Task.WaitAsync(_disposeTimeout, timeProvider);
                await wait.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
                if (!wait.IsCompletedSuccessfully)
                {
                    Log.DrainExitTimedOut(logger, _disposeTimeout);
                }
            }
        }
        finally
        {
            disposal.TrySetResult();
        }
    }

    // Step 1: Disposed closes Enqueue and MarkReady, and Retire closes init start; the detached actions complete outside
    // the lock. The init buffer is copied, not cleared: nothing reads it once Disposed. Returns the last drain's exit
    // (complete when no drain runs).
    private TaskCompletionSource? Detach()
    {
        Pending[] detached;
        TaskCompletionSource? exited;
        lock (_gate)
        {
            _state = StoreState.Disposed;
            detached = [.. _queue, .. _initBuffer];
            _queue.Clear();
            exited = _drainExited;
        }

        foreach (var p in detached)
        {
            p.Complete(DispatchResult.Disposed);
        }

        _initializer.Retire();

        // A no-op when StoreInitialized completed it (processed or detached above); otherwise Ready was never reached.
        _sharedInit.TrySetResult(DispatchResult.Disposed);
        return exited;
    }

    // Step 2: callbacks run inline, possibly while a drain is in flight; Cancel runs them all even when one throws.
    private void CancelLifetime()
    {
        try
        {
            _lifetime.Cancel();
        }
        catch (AggregateException ex)
        {
            Log.CancellationCallbackThrew(logger, ex);
        }
    }
}

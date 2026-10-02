using System.Diagnostics.CodeAnalysis;

namespace Ducky;

// SPEC §6.11 steps 1-3, phases 5a-5b and step 6 (INV-29). Every caller gets the one disposal task, published before any
// step runs, so a call made re-entrantly from step 2's callbacks starts nothing. The other waits and phases
// (init, effect runs) come with those features; when step 3 times out they chain on the drain's exit.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The lifetime CTS is never disposed: tokens taken from it may be read after disposal, and it owns no timer or linked registration.")]
internal sealed partial class Dispatcher
{
    private const long MaxTimeoutTicks = (uint.MaxValue - 1L) * TimeSpan.TicksPerMillisecond;

    private readonly CancellationTokenSource _lifetime = lifetime;

    // Task.WaitAsync and ITimer.Change accept only Timeout.InfiniteTimeSpan or [0, uint.MaxValue - 1 ms]: a negative
    // timeout waits not at all and a larger one (TimeSpan.MaxValue meaning "forever") waits the longest bound, so neither
    // dispose step 3 nor arming InitTimeout ever throws.
    private static TimeSpan ClampTimeout(TimeSpan timeout) => timeout == Timeout.InfiniteTimeSpan
        ? timeout
        : TimeSpan.FromTicks(Math.Clamp(timeout.Ticks, 0, MaxTimeoutTicks));

    private readonly TimeSpan _disposeTimeout = ClampTimeout(disposeTimeout);

    private TaskCompletionSource? _disposal;

    // The bound step 3 actually waits with, which middleware read as DisposeTimeout (§5.6).
    internal TimeSpan DisposeTimeout => _disposeTimeout;

    private Task? _disposalSteps;

    // For tests: every disposal step, those chained past a step-3 timeout included; null before disposal.
    internal Task? DisposalSteps => Volatile.Read(ref _disposalSteps);

    // The store lifetime, cancelled at step 2: effect runs and init link their tokens to it.
    internal CancellationToken Lifetime => _lifetime.Token;

    // One volatile read: the disposal task is published before step 1 runs.
    internal bool DisposalBegan => Volatile.Read(ref _disposal) is not null;

    // Never faults: nothing below throws but a fatal exception (SafeLogger rethrows only OutOfMemoryException).
    internal Task DisposeAsync()
    {
        var disposal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var published = Interlocked.CompareExchange(ref _disposal, disposal, null);
        if (published is not null)
        {
            return published.Task;
        }

        Volatile.Write(ref _disposalSteps, RunDisposalAsync(disposal));
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
            var drainExited = exited?.Task ?? Task.CompletedTask;
            var wait = drainExited.WaitAsync(_disposeTimeout, timeProvider);
            await wait.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (!wait.IsCompletedSuccessfully)
            {
                // DisposeAsync completes at the bound; the later steps still run, chained on the drain's exit.
                Log.DrainExitTimedOut(logger, _disposeTimeout);
                disposal.TrySetResult();
            }

            await DisposeMaterializedAsync(drainExited).ConfigureAwait(false);
            ClearSubscribers();
        }
        finally
        {
            disposal.TrySetResult();
        }
    }

    // Phases 5a and 5b, after the drain exited (never inline while one may be in flight): middleware in reverse registration
    // order, then the store-owned effects in reverse (the run wait before 5b comes with M2-03b). Nothing materialized means
    // nothing to dispose; a faulted materialization built nothing it kept (disposing its partial work is M4-05b's).
    private async Task DisposeMaterializedAsync(Task drainExited)
    {
#pragma warning disable VSTHRD003 // justification: the drain's exit is our own RunContinuationsAsynchronously TCS, never faulted
        // Stryker disable once Boolean : either context only resumes after the drain exited, which is all 5a needs
        await drainExited.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        if (!_materialized.IsValueCreated)
        {
            return;
        }

        var materialized = _materialized.Value;
        await DisposeReversedAsync(materialized.Middleware).ConfigureAwait(ConfigureAwaitOptions.None);
        await DisposeReversedAsync(materialized.OwnedEffects).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // Each disposal in its own try/catch (Error 1015), so one throw never skips the others.
    private async Task DisposeReversedAsync(object[] owned)
    {
        for (var i = owned.Length - 1; i >= 0; i--)
        {
            try
            {
                switch (owned[i])
                {
                    case IAsyncDisposable disposable:
                        await disposable.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.None);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
#pragma warning disable CA1031 // justification: disposal is user code; its throw is logged and never faults disposal (§6.11)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Log.DisposeThrew(logger, ex, owned[i].GetType());
            }
        }
    }

    // Step 1: Disposed closes Enqueue and MarkReady, and Retire closes init start; the detached actions complete outside
    // the lock. The init buffer is copied, not cleared: nothing reads it once Disposed. Returns the last drain's exit
    // (complete when no drain runs).
    private TaskCompletionSource? Detach()
    {
        Pending[] detached;
        TaskCompletionSource? exited;
        TaskCompletionSource? idle;
        lock (_gate)
        {
            _state = StoreState.Disposed;
            detached = [.. _queue, .. _initBuffer];
            _queue.Clear();
            exited = _drainExited;
            idle = _idleWaiters;
        }

        foreach (var p in detached)
        {
            p.Complete(DispatchResult.Disposed);
        }

        _initializer.Retire();

        // A no-op when StoreInitialized completed it (processed or detached above); otherwise Ready was never reached.
        _sharedInit.TrySetResult(DispatchResult.Disposed);

        // Now, not at the end: a wait registered before disposal ends only here, and Disposed registers no new one.
        idle?.TrySetResult();
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

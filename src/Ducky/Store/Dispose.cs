using System.Diagnostics.CodeAnalysis;

namespace Ducky;

// SPEC §6.11 steps 1-3, the step-4 run and init waits, phases 5a-5b and step 6 (INV-29). Every caller gets the one
// disposal task, published before any step runs, so a call made re-entrantly from step 2's callbacks starts nothing. The
// materialization wait comes with M4-05b; when step 3 times out the rest chains on the drain's exit.
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
        // Before anything else, so a run that calls this, first or later, is never waited for by step 4.
        _effectRun.Value?.DisposeCalled.TrySetResult();
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
            var (exited, retired) = Detach();
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

            await DisposeMaterializedAsync(drainExited, retired, disposal).ConfigureAwait(false);
            ClearSubscribers();
        }
        finally
        {
            disposal.TrySetResult();
        }
    }

    // Phases 5a and 5b, after the drain exited (never inline while one may be in flight): middleware in reverse registration
    // order, then, after the run wait, the store-owned effects in reverse and the store scope. Nothing materialized (or a
    // faulted materialization, which kept nothing; disposing its partial work is M4-05b's) leaves only the store scope.
    private async Task DisposeMaterializedAsync(Task drainExited, Task retired, TaskCompletionSource disposal)
    {
#pragma warning disable VSTHRD003 // justification: the drain's exit is our own RunContinuationsAsynchronously TCS, never faulted
        // Stryker disable once Boolean : either context only resumes after the drain exited, which is all 5a needs
        await drainExited.ConfigureAwait(false);
#pragma warning restore VSTHRD003
        if (!_materialized.IsValueCreated)
        {
            // The scope exists from DuckyStore.Create on; disposing one that resolved nothing constructs nothing.
            await DisposeReversedAsync([storeScope]).ConfigureAwait(ConfigureAwaitOptions.None);
            return;
        }

        // Step 4's waits start with the drain's exit, so the run wait sees every run step 11 registered; 5a does not wait
        // for it, and starts the init waits itself.
        var runs = RunWaitAsync();
        Task bounded = runs.WaitAsync(_disposeTimeout, timeProvider);
        var materialized = _materialized.Value;
#pragma warning disable VSTHRD003 // justification: our own phase; the Retire task it is handed is only observed
        await DisposeMiddlewareAsync(materialized.Middleware, retired).ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        await bounded.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!bounded.IsCompletedSuccessfully)
        {
            // A hung run holds only 5b: step 6 runs now (RunDisposalAsync's later call is a no-op) and DisposeAsync
            // completes at the bound, so no subscriber closure stays reachable; 5b stays chained on the runs.
            ClearSubscribers();
            disposal.TrySetResult();
            await runs.ConfigureAwait(false);
        }

        // 5b: owned effects in reverse construction order, then the store scope (browser) last; a null scope is skipped.
        await DisposeReversedAsync([storeScope, .. materialized.OwnedEffects]).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // Phase 5a, in reverse registration order, with step 4's init waits started together: each middleware waits only for
    // its own init task, after the prefix Retire returned, bounded by DisposeTimeout. One whose wait expired gets its
    // DisposeAsync chained on that init alone (never run early; it may land after 5b), and 5a moves on at once.
    private async Task DisposeMiddlewareAsync(Middleware[] middleware, Task retired)
    {
        var inits = new Task[middleware.Length];
        var waits = new Task[middleware.Length];
        for (var i = 0; i < middleware.Length; i++)
        {
            inits[i] = InitEndedAsync(retired, i);
            waits[i] = inits[i].WaitAsync(_disposeTimeout, timeProvider);
        }

        for (var i = middleware.Length - 1; i >= 0; i--)
        {
            await waits[i].ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (waits[i].IsCompletedSuccessfully)
            {
                await DisposeBoundedAsync(middleware[i]).ConfigureAwait(ConfigureAwaitOptions.None);
            }
            else
            {
                _ = DisposeAfterAsync(inits[i], middleware[i]);
            }
        }
    }

    // Ends once this middleware's own init ended, or at once when it never started (InitTasks is published when the
    // prefix completes, and is empty when init never ran). Never faults: an init fault is init's own Error 1032.
    private async Task InitEndedAsync(Task retired, int index)
    {
#pragma warning disable VSTHRD003 // justification: _prefixDone and the init tasks are only observed here, never completed on our context
        await retired.ConfigureAwait(ConfigureAwaitOptions.None);
        var tasks = _initializer.InitTasks;
        if (index < tasks.Length && tasks[index] is { } init)
        {
            await init.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }
#pragma warning restore VSTHRD003
    }

    private async Task DisposeAfterAsync(Task init, Middleware middleware)
    {
#pragma warning disable VSTHRD003 // justification: InitEndedAsync's own task, which never faults
        await init.ConfigureAwait(ConfigureAwaitOptions.None);
#pragma warning restore VSTHRD003
        await DisposeBoundedAsync(middleware).ConfigureAwait(ConfigureAwaitOptions.None);
    }

    // Awaited with WaitAsync(DisposeTimeout): past it, Warning 1016 and the store moves on, leaving that DisposeAsync
    // running but observed, so a throw, before or after the bound, is Error 1015 as in DisposeReversedAsync.
    private async Task DisposeBoundedAsync(Middleware middleware)
    {
        var dispose = DisposeLoggedAsync(middleware);
        await dispose.WaitAsync(_disposeTimeout, timeProvider).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!dispose.IsCompleted)
        {
            Log.MiddlewareDisposeTimedOut(logger, middleware.GetType(), _disposeTimeout);
        }
    }

    // Never faults: a throw is logged.
    private async Task DisposeLoggedAsync(Middleware middleware)
    {
        try
        {
            await middleware.DisposeAsync().AsTask().ConfigureAwait(ConfigureAwaitOptions.None);
        }
#pragma warning disable CA1031 // justification: disposal is user code; its throw is logged and never faults disposal (§6.11)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.DisposeThrew(logger, ex, middleware.GetType());
        }
    }

    // Each disposal in its own try/catch (Error 1015), so one throw never skips the others.
    private async Task DisposeReversedAsync(object?[] owned)
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
                Log.DisposeThrew(logger, ex, owned[i]!.GetType());
            }
        }
    }

    // Step 1: Disposed closes Enqueue and MarkReady, and Retire closes init start; the detached actions complete outside
    // the lock. The init buffer is copied, not cleared: nothing reads it once Disposed. Returns the last drain's exit
    // (complete when no drain runs) and the task Retire returned, which step 4's init waits start with.
    private (TaskCompletionSource? Exited, Task Retired) Detach()
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

        var retired = _initializer.Retire();

        // A no-op when StoreInitialized completed it (processed or detached above); otherwise Ready was never reached.
        _sharedInit.TrySetResult(DispatchResult.Disposed);

        // Now, not at the end: a wait registered before disposal ends only here, and Disposed registers no new one.
        idle?.TrySetResult();
        return (exited, retired);
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

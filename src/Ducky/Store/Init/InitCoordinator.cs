using System.Diagnostics.CodeAnalysis;

namespace Ducky;

// SPEC §6.7 (INV-13): the init state machine NotStarted -> Starting -> Running -> Completed. Start runs every middleware's
// InitializeAsync up to its first await, publishes InitTasks and completes _prefixDone, then arms InitTimeout; Complete
// (every init finished) or Abort (the timer) makes the store Ready, whichever wins the CAS from Running. Init never starts
// under _gate (INV-05): the triggers call Start after releasing it. The overflow abort comes with M4-03b; Retire returning
// _prefixDone, the timer disposal in Retire and the per-middleware dispose waits on InitTasks with M4-03c.
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The timer is disposed by Complete and Abort; the init CTS is never disposed, because middleware may read its token after init.")]
internal sealed class InitCoordinator(Lazy<Materialized> materialized, SafeLogger logger, TimeProvider timeProvider, TimeSpan initTimeout, CancellationToken lifetime)
{
    private const int NotStarted = 0;
    private const int Starting = 1;
    private const int Running = 2;
    private const int Completed = 3;
    private int _state;

    // Created with the coordinator, linked to the store lifetime, so it exists before any transition.
    private readonly CancellationTokenSource _initCts = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
    private readonly TaskCompletionSource _prefixDone = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ITimer? _timer;

    // Step 2, for dispose (§6.11 step 4): each middleware's own init task by registration index, null when it never
    // started. Read it once PrefixDone completed.
    internal Task?[] InitTasks { get; private set; } = [];

    internal Task PrefixDone => _prefixDone.Task;

    // Idempotent; once started, one volatile read.
    internal void Start(Dispatcher dispatcher)
    {
        if (Volatile.Read(ref _state) == NotStarted
            && Interlocked.CompareExchange(ref _state, Starting, NotStarted) == NotStarted)
        {
            _ = RunAsync(dispatcher);
        }
    }

    // Steps 1, 2, 4 and 5. Every synchronous part runs before this returns its task. When every init task has already
    // completed, the rest runs synchronously too, so the trigger drains inline. A faulted init counts as finished.
    private async Task RunAsync(Dispatcher dispatcher)
    {
        // Every entry point materialized before Start (§6.6), so a constructor's DUCKY353 has already surfaced there. Once
        // disposal began nothing is built, as in Dispatcher.Materialize: init starts no middleware.
        Middleware[] all = dispatcher.DisposalBegan ? [] : materialized.Value.Middleware;

        // Created unarmed, so it exists before anything can read it; Change arms it only once Running.
        _timer = timeProvider.CreateTimer(_ => Abort(dispatcher, InitAbortReason.InitTimeout), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        var tasks = new Task?[all.Length];
        List<Task> observed = new(all.Length);
        for (var i = 0; i < all.Length; i++)
        {
            // Retire (a dispose, possibly from an earlier synchronous part) stops the loop: nothing starts after it.
            if (Volatile.Read(ref _state) != Starting)
            {
                break;
            }

            Task init;
            try
            {
                init = all[i].InitializeAsync(_initCts.Token).AsTask();
            }
#pragma warning disable CA1031 // justification: user code; a synchronous throw is a faulted init (§6.7 step 1)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                init = Task.FromException(ex);
            }

            tasks[i] = init;
            observed.Add(ObserveAsync(init, all[i].GetType()));
        }

        // Step 2: published whatever the CAS below decides, so dispose sees every init that really started (§6.11 step 4).
        InitTasks = tasks;
        _prefixDone.TrySetResult();

        // Retire won while Starting: nothing is armed and init never reaches Ready.
        if (Interlocked.CompareExchange(ref _state, Running, Starting) == Starting)
        {
            // Armed only while an init is pending: with every init complete there is nothing to bound, and a zero timeout
            // could otherwise abort before Complete.
            var inits = Task.WhenAll(observed);
            if (!inits.IsCompleted)
            {
                _timer.Change(initTimeout, Timeout.InfiniteTimeSpan);
            }

            await inits.ConfigureAwait(ConfigureAwaitOptions.None);
            Complete(dispatcher);
        }
    }

    // Each init observed on its own, so one that faults is logged even when another never ends. Never faults. Only a fault
    // is a failure (§6.7 step 4): a cancelled init honoured its token (an abort or a dispose).
    private async Task ObserveAsync(Task init, Type type)
    {
#pragma warning disable VSTHRD003 // justification: observing the middleware's own init task is the point; it never needs our context
        await init.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#pragma warning restore VSTHRD003
        if (init.Exception is { } failure)
        {
            Log.MiddlewareInitFailed(logger, failure.InnerException!, type);
        }
    }

    private void Complete(Dispatcher dispatcher)
    {
        if (Interlocked.CompareExchange(ref _state, Completed, Running) != Running)
        {
            return;
        }

        // Stryker disable once Statement : equivalent, a timer that fires after Complete finds Abort's CAS failing
        _timer!.Dispose();
        dispatcher.MarkReady();
    }

    // One CAS from Running; the loser does nothing. The init token is cancelled outside any lock, and its callbacks all
    // run even when one throws. Middleware inits that end later are still observed and logged, never awaited.
    private void Abort(Dispatcher dispatcher, InitAbortReason reason)
    {
        if (Interlocked.CompareExchange(ref _state, Completed, Running) != Running)
        {
            return;
        }

        // Stryker disable once Statement : equivalent, the one-shot timer is the caller and never fires again
        _timer!.Dispose();
        try
        {
            _initCts.Cancel();
        }
        catch (AggregateException ex)
        {
            Log.CancellationCallbackThrew(logger, ex);
        }

        Log.StoreInitAborted(logger, reason);
        dispatcher.MarkReady();
    }

    // Dispose step 1 (§6.7): a Start that has not yet won NotStarted -> Starting loses its CAS and starts nothing; a Start
    // inside its synchronous part starts no further init and arms nothing; a running init ends in a CAS that fails.
    internal void Retire() => Volatile.Write(ref _state, Completed);
}

// Why init ended before every middleware init finished (StoreInitAborted, §6.7). BufferOverflow comes with M4-03b.
internal enum InitAbortReason
{
    InitTimeout,
}

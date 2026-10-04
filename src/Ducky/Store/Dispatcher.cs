using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Ducky;

// The single drainer (SPEC §6.3): whoever enqueues while no drain is active drains the whole queue inline, so reducers
// never run concurrently (INV-01) and a dispatch from a reducer is queued, never nested. No user code, log call or
// completion runs under _gate (INV-05).
internal sealed partial class Dispatcher(
    Registry registry,
    StateSnapshot initial,
    SafeLogger logger,
    SafeTelemetry telemetry,
    int maxDispatchDepth,
    bool throwOnUnhandledAction,
    int initBufferCapacity,
    TimeSpan initTimeout,
    TimeSpan disposeTimeout,
    TimeProvider timeProvider,
    IAsyncDisposable? storeScope,
    Lazy<Materialized> materialized,
    CancellationTokenSource lifetime,
    DuckyJson json)
{
    private readonly Lock _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly ActionTypes _actionTypes = new();
    private readonly List<(int Ordinal, object State)> _scratch = [];
    private StateSnapshot _snapshot = initial;
    private bool _draining;
    private long _lastId;
    private TaskCompletionSource? _drainExited;

    // Middleware and effects, built on the store's first use by Build (Materialization.cs, §6.6), outside _gate. A
    // constructor's DUCKY353 is cached by the Lazy, which rethrows that same instance at every later use.
    private readonly Lazy<Materialized> _materialized = materialized;

    internal StateSnapshot State => Volatile.Read(ref _snapshot);

    // The last drain's exit: created when a drain starts, completed when it releases (null before the first drain).
    internal Task? DrainExited
    {
        get
        {
            lock (_gate)
            {
                return _drainExited?.Task;
            }
        }
    }

    // IVT-only seam (§6.3, §17.1): null in production, invoked with the action at the top of Process.
    internal Action<object>? BeforeProcessHook { get; set; }

    internal void Dispatch(object action, Origin origin)
    {
        ArgumentNullException.ThrowIfNull(action);
        Enqueue(NewPending(action, origin, null));
    }

    internal Task<DispatchResult> DispatchAsync(object action, Origin origin)
    {
        ArgumentNullException.ThrowIfNull(action);
        var completion = new TaskCompletionSource<DispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Enqueue(NewPending(action, origin, completion));
        return completion.Task;
    }

    // Before Ready a user action (Local, Effect) goes to the init buffer and starts init; every other action goes to the
    // main queue (§6.3). Once Disposed the action completes Disposed and is logged at Debug. Init starts, the drain runs
    // and the after-disposal path completes outside the lock, on the caller's context (INV-05).
    internal void Enqueue(Pending p)
    {
        var disposed = false;
        var startInit = false;
        var abort = false;
        var drain = false;
        lock (_gate)
        {
            if (_state == StoreState.Disposed)
            {
                disposed = true;
            }
            else
            {
                p.Id = ++_lastId;
                if (p.CorrelationId == 0)
                {
                    p.CorrelationId = p.Id;
                }

                if (_state != StoreState.Ready && p.Origin is Origin.Local or Origin.Effect)
                {
                    _initBuffer.Enqueue(p);
                    if (_state == StoreState.Created)
                    {
                        _state = StoreState.Initializing;
                        startInit = true;
                    }

                    abort = _initBuffer.Count > initBufferCapacity; // soft bound: nothing is dropped (INV-13)
                }
                else
                {
                    _queue.Enqueue(p);
                    drain = BeginDrainLocked();
                }
            }
        }

        if (disposed)
        {
            CompleteDisposed(p);
            return;
        }

        if (startInit)
        {
            StartInit();
        }

        if (abort)
        {
            _initializer.RequestOverflowAbort(this); // queues the abort, never runs it here; a no-op until Running (§6.7)
        }

        if (drain)
        {
            Drain();
        }
    }

    // A dispatch after disposal began: Disposed, never Dropped, and not counted (INV-02).
    private void CompleteDisposed(Pending p)
    {
        p.Complete(DispatchResult.Disposed);
        Log.DispatchAfterDispose(logger, p.Action.GetType());
    }

    // Under _gate: true when the caller became the drainer.
    private bool BeginDrainLocked()
    {
        if (_draining)
        {
            return false;
        }

        _draining = true;
        _drainExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return true;
    }

    private void Drain()
    {
        while (true)
        {
            Pending? p;
            TaskCompletionSource? exited = null;
            TaskCompletionSource? idle = null;
            lock (_gate)
            {
                // The empty check and the release share one critical section (INV-03).
                if (!_queue.TryDequeue(out p))
                {
                    _draining = false;
                    exited = _drainExited;
                    idle = TakeIdleWaitersIfIdleLocked();
                }
            }

            if (p is null)
            {
                exited!.TrySetResult();
                idle?.TrySetResult();
                return;
            }

            try
            {
                // An effect continuation that became the drainer usually carries its trigger's span, stopped by Process since.
                // No API sets a stopped activity current again, so ExitScope can't restore it: ProcessIsolatedAsync gives the
                // drainer's ExecutionContext, and so its Activity.Current, back untouched (§6.4 step 2).
                if (Activity.Current is { IsStopped: true })
                {
#pragma warning disable VSTHRD002 // justification: ProcessIsolatedAsync never awaits, the task is already complete
                    ProcessIsolatedAsync(p).GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
                }
                else
                {
                    Process(p);
                }
            }
#pragma warning disable CA1031 // justification: the last line of defence, nothing may escape Process (INV-03)
            catch (Exception ex)
            {
                // Complete first, so even a fatal rethrow from the log call can't strand the action or hold the drain.
                p.Complete(DispatchResult.Failed);
                try
                {
                    Log.ProcessEscaped(logger, ex, p.Action.GetType());
                }
                catch
                {
                }
            }
#pragma warning restore CA1031
        }
    }

    // An async method that never awaits: its builder puts the caller's ExecutionContext back when it returns, whatever
    // Process set in it, even while flow is suppressed (ExecutionContext.Capture is null then, so a captured copy can't do
    // it). A throw comes back through GetResult.
#pragma warning disable CS1998 // justification: never awaits, the builder's context restore is the point
    private async Task ProcessIsolatedAsync(Pending p) => Process(p);
#pragma warning restore CS1998

    // SPEC §6.4, one method per step: steps 1-2 (CausalScope.cs; the span through SafeTelemetry, Telemetry.cs), 3 and 11
    // (EffectRuns.cs), 4, 5 and 9 (MiddlewarePipeline.cs), 6-8, 10 (Notify.cs), 12 (Unhandled.cs) so far.
    private void Process(Pending p)
    {
        BeforeProcessHook?.Invoke(p.Action);
        if (DepthExceeded(p))
        {
            return;
        }

        var outer = EnterScope(p);
        Activity? span = null;
        try
        {
            var type = TypeName(p.Action);
            span = telemetry.StartActivity(p, type);
            if (RunCancelled(p))
            {
                return;
            }

            var materialized = _materialized.Value;
            var middleware = materialized.Middleware;
            var previous = State;
            var before = new ActionContext(p, type, previous, previous, []);
            if (!Admitted(p, before, middleware))
            {
                return;
            }

            // A failure in steps 5-6 commits nothing and skips steps 7, 10 and 12, but AfterReduce still sees the action, with
            // before's State == PreviousState and no changed key, and its effects still start: they react to the action, not
            // to the commit, so a throwing StoreInitialized reducer can't disable the load effects (§6.4, INV-08, INV-13).
            var after = before;
            var changed = false;
            var reduced = BeforeReduce(before, middleware) && Reduce(p.Action);
            if (reduced)
            {
                changed = Commit(p.Origin);
                p.Complete(DispatchResult.Reduced);
                var state = State;
                after = new ActionContext(p, before.ActionType, previous, state, ChangedKeys(previous, state));
            }
            else
            {
                p.Complete(DispatchResult.Failed);
            }

            AfterReduce(after, middleware);
            if (changed)
            {
                Notify(p.Action.GetType());
            }

            StartEffects(after, materialized.Effects);
            if (reduced)
            {
                CheckHandled(p, materialized.Effects);
            }
        }
        finally
        {
            ExitScope(outer, span);
        }
    }

    // Step 6: every reducer writes into the scratch list; one throw discards it all (INV-08). A restore never fails.
    private bool Reduce(object action)
    {
        if (action is HydrateSlices restore)
        {
            Hydrate(restore);
            return true;
        }

        _scratch.Clear();
        var current = State;
        var slices = registry.Slices;
        for (var ordinal = 0; ordinal < slices.Length; ordinal++)
        {
            try
            {
                if (slices[ordinal].TryReduce(current[ordinal], action, out var next))
                {
                    _scratch.Add((ordinal, next));
                }
            }
#pragma warning disable CA1031 // justification: a reducer is user code; any throw fails the action, never the drain (§6.4)
            catch (Exception ex)
#pragma warning restore CA1031
            {
                var sliceKey = registry.Keys[ordinal];
                Log.ReducerThrew(logger, ex, sliceKey, action.GetType());
                RouteScopedFailure(action, sliceKey, ex);
                return false;
            }
        }

        return true;
    }

    // Step 7: Commit returns the same snapshot when nothing changed, so republishing it is a no-op (INV-07). Returns
    // whether it changed, which gates step 10.
    private bool Commit(Origin origin)
    {
        var previous = State;
        var next = previous.Commit(CollectionsMarshal.AsSpan(_scratch), origin);
        Volatile.Write(ref _snapshot, next);
        return !ReferenceEquals(previous, next);
    }
}

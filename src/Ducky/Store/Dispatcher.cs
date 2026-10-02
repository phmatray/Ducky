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
    int maxDispatchDepth,
    TimeSpan initTimeout,
    TimeSpan disposeTimeout,
    TimeProvider timeProvider,
    Lazy<Materialized> materialized,
    CancellationTokenSource lifetime)
{
    private readonly Lock _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly List<(int Ordinal, object State)> _scratch = [];
    private StateSnapshot _snapshot = initial;
    private bool _draining;
    private long _lastId;
    private TaskCompletionSource? _drainExited;

    // Middleware and effects, built on the store's first use (§6.6), outside _gate. A constructor's DUCKY353 is cached by
    // the Lazy, which rethrows that same instance at every later use.
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

    // Called first by every materializing entry point (§6.6). Once disposal began nothing is built and nothing rethrown:
    // the caller takes its after-disposal path (the race with a concurrent first use is closed by M4-05b).
    internal void Materialize()
    {
        if (!DisposalBegan)
        {
            _ = _materialized.Value;
        }
    }

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
            p.Complete(DispatchResult.Disposed);
            Log.DispatchAfterDispose(logger, p.Action.GetType());
            return;
        }

        if (startInit)
        {
            StartInit();
        }

        if (drain)
        {
            Drain();
        }
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
                Process(p);
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

    // SPEC §6.4, one method per step: steps 1-2 (CausalScope.cs), 4, 5 and 9 (MiddlewarePipeline.cs), 6-8, 10
    // (Notify.cs) and 11 (EffectRuns.cs) so far.
    private void Process(Pending p)
    {
        BeforeProcessHook?.Invoke(p.Action);
        if (DepthExceeded(p))
        {
            return;
        }

        var outer = EnterScope(p);
        try
        {
            var materialized = _materialized.Value;
            var middleware = materialized.Middleware;
            var previous = State;
            var before = new ActionContext(p, TypeName(p.Action), previous, previous, []);
            if (!Admitted(p, before, middleware))
            {
                return;
            }

            if (!BeforeReduce(before, middleware) || !Reduce(p.Action))
            {
                p.Complete(DispatchResult.Failed);
                return;
            }

            var changed = Commit(p.Origin);
            p.Complete(DispatchResult.Reduced);
            var state = State;
            var after = new ActionContext(p, before.ActionType, previous, state, ChangedKeys(previous, state));
            AfterReduce(after, middleware);
            if (changed)
            {
                Notify();
            }

            StartEffects(after, materialized.Effects);
        }
        finally
        {
            ExitScope(outer);
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

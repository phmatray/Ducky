using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Ducky;

// The single drainer (SPEC §6.3): whoever enqueues while no drain is active drains the whole queue inline, so reducers
// never run concurrently (INV-01) and a dispatch from a reducer is queued, never nested. No user code, log call or
// completion runs under _gate (INV-05).
internal sealed class Dispatcher(Registry registry, StateSnapshot initial, ILogger logger)
{
    private readonly Lock _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly List<(int Ordinal, object State)> _scratch = [];
    private StateSnapshot _snapshot = initial;
    private bool _draining;
    private long _lastId;
    private TaskCompletionSource? _drainExited;

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

    internal void Enqueue(Pending p)
    {
        var drain = false;
        lock (_gate)
        {
            p.Id = ++_lastId;
            _queue.Enqueue(p);
            if (!_draining)
            {
                _draining = true;
                _drainExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
                drain = true;
            }
        }

        // On the caller's context, outside the lock.
        if (drain)
        {
            Drain();
        }
    }

    private void Drain()
    {
        while (true)
        {
            Pending? p;
            TaskCompletionSource? exited = null;
            lock (_gate)
            {
                // The empty check and the release share one critical section (INV-03).
                if (!_queue.TryDequeue(out p))
                {
                    _draining = false;
                    exited = _drainExited;
                }
            }

            if (p is null)
            {
                exited!.TrySetResult();
                return;
            }

            Process(p);
        }
    }

    // SPEC §6.4, one method per step; this story adds steps 6-8.
    private void Process(Pending p)
    {
        if (!Reduce(p.Action))
        {
            p.Complete(DispatchResult.Failed);
            return;
        }

        Commit(p.Origin);
        p.Complete(DispatchResult.Reduced);
    }

    // Step 6: every reducer writes into the scratch list; one throw discards it all (INV-08).
    private bool Reduce(object action)
    {
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
                Log.ReducerThrew(logger, ex, registry.Keys[ordinal], action.GetType());
                return false;
            }
        }

        return true;
    }

    // Step 7: Commit returns the same snapshot when nothing changed, so republishing it is a no-op (INV-07).
    private void Commit(Origin origin) =>
        Volatile.Write(ref _snapshot, State.Commit(CollectionsMarshal.AsSpan(_scratch), origin));
}

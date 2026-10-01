using System.Collections.Immutable;

namespace Dispatcher.Tests;

// The S-4 stub of the §7 dispatcher: a lock-protected queue with one drainer. The dispatching thread that finds no drainer
// becomes it and reduces outside the lock until the queue is empty; DispatchAsync completes after its action is reduced
// and committed, with continuations kept off the drainer.
// S4_STUB selects the negative controls behind spikes.md's detection column: "stranded" clears the drainer flag after
// releasing the lock (the INV-03 bug), "naive" reduces in place with no lock and no drainer. Default "correct".
internal sealed class DispatcherStub
{
    private static readonly string _mode = Environment.GetEnvironmentVariable("S4_STUB") ?? "correct";

    private readonly Lock _gate = new();
    private readonly Queue<(int Action, TaskCompletionSource Reduced)> _queue = new();
    private bool _draining;
    private ImmutableList<int> _state = [];

    public ImmutableList<int> State => Volatile.Read(ref _state);

    public Task DispatchAsync(int action)
    {
        if (_mode == "naive")
        {
            _state = _state.Add(action);
            return Task.CompletedTask;
        }

        var reduced = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _queue.Enqueue((action, reduced));
            if (_draining)
            {
                return reduced.Task;
            }

            _draining = true;
        }

        Drain();
        return reduced.Task;
    }

    private void Drain()
    {
        while (true)
        {
            (int Action, TaskCompletionSource Reduced) next;
            bool empty;
            lock (_gate)
            {
                empty = !_queue.TryDequeue(out next);
                if (empty && _mode != "stranded")
                {
                    _draining = false;
                    return;
                }
            }

            if (empty)
            {
                // stranded: a dispatch enqueued between the unlock and this write sees a drainer and is never reduced.
                _draining = false;
                return;
            }

            Volatile.Write(ref _state, _state.Add(next.Action));
            next.Reduced.SetResult();
        }
    }
}

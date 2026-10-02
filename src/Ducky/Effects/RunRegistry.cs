using System.Collections.Concurrent;

namespace Ducky;

// SPEC §6.6 run registry (INV-11, INV-29): every started run, whatever its policy, LongRunning included, stays in _runs
// until it ends. _runningEffects stays the idle counter; _runs is what dispose step 4 waits for (§6.11).
internal sealed partial class Dispatcher
{
    private readonly ConcurrentDictionary<long, (Task Run, EffectRunToken Token)> _runs = new();
    private long _lastRunId;

    // For tests: the runs still registered.
    internal int RunCount => _runs.Count;

    // Step 11, on the drainer. A run that ends between the check and the add is removed by its continuation at once.
    private void Register(Task run, EffectRunToken token)
    {
        if (!run.IsCompleted)
        {
            _runs[token.Id] = (run, token);
            _ = run.ContinueWith(
                ended => _runs.TryRemove(token.Id, out _),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    // Step 4: each registered run ends or calls DisposeAsync (this call or any other), whichever comes first. Never faults.
#pragma warning disable VSTHRD003 // justification: WhenAny only observes the runs, which never block on the caller's context
    private Task<Task[]> RunWaitAsync() =>
        Task.WhenAll(_runs.Values.Select(r => Task.WhenAny(r.Run, r.Token.DisposeCalled.Task)));
#pragma warning restore VSTHRD003
}

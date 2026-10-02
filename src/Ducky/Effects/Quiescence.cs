namespace Ducky;

// A new object per run (SPEC §6.6). Token is the store lifetime for Merge, the slot's linked token for Switch; several runs
// may share one, so a run's identity is this object or its Id, never the token. CountedForIdle is true for a
// non-LongRunning run until its idle decrement; it is written and read under the store's _gate. DisposeCalled completes
// when the run calls its store's DisposeAsync (§6.11).
internal sealed class EffectRunToken(long id, CancellationToken token)
{
    public long Id => id;

    public CancellationToken Token => token;

    public TaskCompletionSource DisposeCalled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool CountedForIdle { get; set; }
}

// Quiescence (SPEC §6.5, §6.6, §7.4): _runningEffects counts the non-LongRunning runs, and idle requires it to be zero.
// The effect-run scope _effectRun is per store, never static: only RunAsync sets it, for its own run, and Process clears
// it for every action (§6.4 step 2).
internal sealed partial class Dispatcher
{
    private readonly AsyncLocal<EffectRunToken?> _effectRun = new();
    private int _runningEffects;

    // Called first in RunAsync, inside its try, so a throwing LongRunning getter is that run's EffectFailed.
    private void BeginRun(Effect effect, EffectRunToken run)
    {
        if (!effect.LongRunning)
        {
            lock (_gate)
            {
                _runningEffects++;
                run.CountedForIdle = true;
            }
        }

        _effectRun.Value = run;
    }

    // The last action of the run's finally (after the slot remove, Done and Cts disposal, §6.6), so an idle waiter's
    // continuation never observes the run's slot. The waiters are completed outside the lock (INV-05).
    private void EndRun(EffectRunToken run)
    {
        TaskCompletionSource? idle;
        lock (_gate)
        {
            if (!run.CountedForIdle)
            {
                return;
            }

            run.CountedForIdle = false;
            _runningEffects--;
            idle = TakeIdleWaitersIfIdleLocked();
        }

        idle?.TrySetResult();
    }

    // §7.4: a non-LongRunning run of this store that awaited idle would wait for itself forever.
    private void ThrowIfCountedRunLocked()
    {
        if (_effectRun.Value?.CountedForIdle == true)
        {
            throw new InvalidOperationException(
                "WhenIdleAsync was called from a non-LongRunning effect run of this store, which would wait for itself " +
                "forever. Do not wait for idle inside an effect, or mark the effect LongRunning.");
        }
    }
}

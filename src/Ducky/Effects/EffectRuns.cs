namespace Ducky;

// SPEC §6.4 steps 3 and 11 and §6.6 (EFF-01, INV-11): starting runs through each effect's EffectRunner, and the run check
// of EffectContext dispatches; idle counting and the effect-run scope are in Quiescence.cs (M2-03), the Switch slots in
// Policies/Switch.cs, the run registry in RunRegistry.cs (M2-03b). Exhaust and Queue come with M2-05.
internal sealed partial class Dispatcher
{
    internal TimeProvider Time => timeProvider;

    // For tests: the slots every runner of this store holds (§6.6).
    internal int SlotCount => _materialized.Value.Effects.Values.Sum(runners => runners.Sum(r => r.SlotCount));

    // EffectContext.Dispatch/DispatchAsync (§6.6). Disposal takes precedence (one volatile read): once it began, the call
    // completes Disposed, uncounted, whatever the run's token. Only on a live store is a cancelled token a superseded
    // Switch run: Dropped (Debug 1050). The token is read first, as in step 3: DisposeAsync publishes _disposal before it
    // cancels the lifetime, so a token its disposal cancelled always meets DisposalBegan and completes Disposed, never a
    // drop. Otherwise the action carries its run, so step 3 checks it again at process time.
    internal void DispatchFromRun(object action, EffectRunToken run, TaskCompletionSource<DispatchResult>? completion)
    {
        ArgumentNullException.ThrowIfNull(action);
        var p = NewPending(action, Origin.Effect, completion);
        var cancelled = run.Token.IsCancellationRequested;
        if (DisposalBegan)
        {
            CompleteDisposed(p);
            return;
        }

        if (cancelled)
        {
            Drop(p);
            return;
        }

        p.Run = run;
        Enqueue(p);
    }

    // Step 3, with the same precedence: a run-tagged action whose token was cancelled after it was enqueued completes
    // Disposed if the store's disposal began, and is otherwise a stale result of a superseded Switch run, dropped here,
    // at process time, so FIFO can't let a result queued before the supersession through.
    private bool RunCancelled(Pending p)
    {
        if (p.Run?.Token.IsCancellationRequested != true)
        {
            return false;
        }

        if (DisposalBegan)
        {
            p.Complete(DispatchResult.Disposed);
        }
        else
        {
            Drop(p);
        }

        return true;
    }

    private void Drop(Pending p)
    {
        p.Complete(DispatchResult.Dropped);
        Log.RunDropped(logger, p.Action.GetType());
    }

    // Step 11: each effect registered for the action's exact type starts inline, on the drainer, up to its handler's first
    // await. Runs after a commit and after a BeforeReduce or reducer failure alike: effects react to the action, not to the
    // commit (§6.4, M4-01b).
    private void StartEffects(ActionContext trigger, Dictionary<Type, EffectRunner[]> index)
    {
        if (!index.TryGetValue(trigger.Action.GetType(), out var runners))
        {
            return;
        }

        // Checked once, before any run: a disposal that begins after it gives the remaining runs a cancelled token, and
        // they are registered before the drain exits, so dispose step 4 still waits for them.
        if (Lifetime.IsCancellationRequested)
        {
            Log.EffectsNotStarted(logger, trigger.Action.GetType());
            return;
        }

        var inFailure = _causal.Value!.InFailure;
        foreach (var runner in runners)
        {
            var slot = runner.NewSlot(Lifetime);
            var run = new EffectRunToken(Interlocked.Increment(ref _lastRunId), slot?.Cts.Token ?? Lifetime);
            Register(RunAsync(runner, slot, run, trigger, inFailure), run);
        }
    }

    // Never faults. Step 11 builds the run's slot and token and registers every run, whatever its policy (§6.6 run
    // registry). A Merge run takes the store-lifetime token itself: no per-run CTS, so nothing to dispose. A Switch run
    // takes its slot's linked token; the key is computed and the slot installed inside the try, so a throwing key function
    // is this effect's EffectFailed only, and the finally compare-and-removes the run's own slot (or disposes a slot never
    // installed) before the idle decrement (§6.6 finally order); the removal runs no user code (SlotKey), so the decrement
    // always runs. An OCE for the run's token, or any OCE once it is cancelled (supersession or store disposal), is ours,
    // never a failure (§6.6); anything else, a synchronous throw included (this is a plain async method), is logged (Error
    // 1003) and becomes EffectFailed at depth 0 on the trigger's chain, or only a log line under a failure action
    // (INV-12): inFailure is captured by the drainer at start, so a run resumed on any thread keeps it. Nothing here waits
    // on the cancellable token, so a fault raised after cancellation is still observed (the 1.x lost fault).
    private async Task RunAsync(EffectRunner runner, Slot? slot, EffectRunToken run, ActionContext trigger, bool inFailure)
    {
        var effect = runner.Effect;
        SlotKey? key = null;
        try
        {
            BeginRun(effect, run);
            if (slot is not null)
            {
                key = runner.Install(trigger.Action, slot, logger);
            }

            await effect.RunAsync(trigger.Action, new EffectContext(this, trigger, run), run.Token).ConfigureAwait(ConfigureAwaitOptions.None);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == run.Token || run.Token.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // justification: an effect is user code; its throw becomes EffectFailed, never a fault (§6.6)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.EffectThrew(logger, ex, effect.GetType(), trigger.Action.GetType());
            RouteFailure(new EffectFailed(effect.GetType().ToString(), trigger.ActionType, ex), ex, trigger.ActionType, trigger.CorrelationId, inFailure);
        }
        finally
        {
            if (slot is not null)
            {
                runner.Release(key, slot);
            }

            EndRun(run);
        }
    }
}

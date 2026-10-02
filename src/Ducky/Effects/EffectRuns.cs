namespace Ducky;

// SPEC §6.4 step 11 and §6.6 (EFF-01, INV-11): the Merge runner. Switch, Exhaust, Queue, idle counting and the run
// registry come with M2-03, M2-03b, M2-04 and M2-05.
internal sealed partial class Dispatcher
{
    internal TimeProvider Time => timeProvider;

    // Step 11: each effect registered for the action's exact type starts inline, on the drainer, up to its handler's first
    // await. Runs only after a successful reduce for now (the failure rule is M4-01b).
    private void StartEffects(ActionContext trigger, Dictionary<Type, Effect[]> index)
    {
        if (!index.TryGetValue(trigger.Action.GetType(), out var effects))
        {
            return;
        }

        var inFailure = _causal.Value!.InFailure;
        foreach (var effect in effects)
        {
            _ = RunAsync(effect, trigger, inFailure);
        }
    }

    // Never faults. A Merge run takes the store-lifetime token itself: no per-run CTS, so nothing to dispose. An OCE for
    // that token, or any OCE once it is cancelled (store disposal), is ours, never a failure (§6.6); anything else, a
    // synchronous throw included, is EffectFailed at depth 0 on the trigger's chain, or a log line under a failure action
    // (INV-12).
    private async Task RunAsync(Effect effect, ActionContext trigger, bool inFailure)
    {
        var token = Lifetime;
        try
        {
            await effect.RunAsync(trigger.Action, new EffectContext(this, trigger), token).ConfigureAwait(ConfigureAwaitOptions.None);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == token || token.IsCancellationRequested)
        {
        }
#pragma warning disable CA1031 // justification: an effect is user code; its throw becomes EffectFailed, never a fault (§6.6)
        catch (Exception ex)
#pragma warning restore CA1031
        {
            RouteFailure(new EffectFailed(effect.GetType().ToString(), trigger.ActionType, ex), ex, trigger.ActionType, trigger.CorrelationId, inFailure);
        }
    }
}

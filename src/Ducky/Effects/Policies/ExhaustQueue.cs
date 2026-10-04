namespace Ducky;

// The Exhaust and Queue policies (SPEC §6.6, INV-11). Exhaust: the slot is a running marker taken with TryAdd; while it is
// held, a new action for the key starts no run, and its run token is the store lifetime, like Merge. Queue: each start
// installs a slot with its own Done and waits for the replaced slot's Done, so runs for one key never overlap and run in
// dispatch order; the run's finally compare-and-removes its slot, then completes its Done (EffectRunner.Release).
internal sealed partial class Dispatcher
{
    // The Exhaust drop: Debug 1051 and ducky.effect.dropped.
    private void DropEffect(Effect effect, object action)
    {
        Log.EffectDropped(logger, effect.GetType(), action.GetType());
        telemetry.Add(telemetry.EffectDropped);
    }
}

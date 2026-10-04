namespace Ducky;

// SPEC §6.4 step 12 (CORE-08, ADR-0018): a Local or Effect action that no slice can handle and no async effect matched is
// logged at Debug or, with ThrowOnUnhandledAction, routed as ReducerFailed(UnhandledActionException). Library traffic
// (System, Hydration, CrossTab, DevTools) is never checked. Reactive effects and middleware are not inspected.
internal sealed partial class Dispatcher
{
    private void CheckHandled(Pending p, Dictionary<Type, EffectRunner[]> effects)
    {
        if (p.Origin is not (Origin.Local or Origin.Effect))
        {
            return;
        }

        var type = p.Action.GetType();
        if (effects.ContainsKey(type))
        {
            return;
        }

        foreach (var slice in registry.Slices) // a loop, not Array.Exists: no closure allocation on the drainer hot path
        {
            if (slice.CanHandle(type))
            {
                return;
            }
        }

        if (throwOnUnhandledAction)
        {
            RouteScopedFailure(p.Action, null, new UnhandledActionException(TypeName(p.Action)));
        }
        else
        {
            Log.UnhandledAction(logger, type); // the generated method checks IsEnabled(Debug) first
        }
    }
}

namespace Ducky;

// FailureRouter (SPEC §6.4 steps 1 and 4-6, INV-12): every failure becomes exactly one core failure action, or a log
// line when it was raised while handling a failure action.
internal sealed partial class Dispatcher
{
    // A core failure action is a System action at depth 0 on the failed action's chain (§6.5), marked IsFailure so that
    // anything failing under it is logged only. Called on the drainer, so Enqueue only queues it.
    private void RouteFailure(ReducerFailed failure, long correlationId, bool inFailure)
    {
        if (inFailure)
        {
            Log.FailureNotDispatched(logger, failure.Exception, failure.GetType(), failure.ActionType);
            return;
        }

        Enqueue(new Pending(failure, Origin.System, 0, correlationId, false, true, null));
    }

    // ponytail: Type.ToString() is Namespace.Name; the per-store cached action type names of §9 replace it (M4-07).
    private static string TypeName(object action) => action.GetType().ToString();
}

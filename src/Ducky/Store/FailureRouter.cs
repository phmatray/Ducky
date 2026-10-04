namespace Ducky;

// FailureRouter (SPEC §6.4 steps 1 and 4-6, INV-12): every failure becomes exactly one core failure action, or a log
// line when it was raised while handling a failure action.
internal sealed partial class Dispatcher
{
    // A core failure action (ReducerFailed, EffectFailed) is a System action at depth 0 on the failed action's chain (§6.5),
    // marked IsFailure so that anything failing under it is logged only.
    private void RouteFailure(object failure, Exception exception, string actionType, long correlationId, bool inFailure)
    {
        if (inFailure)
        {
            Log.FailureNotDispatched(logger, exception, failure.GetType(), actionType);
            return;
        }

        Enqueue(new Pending(failure, Origin.System, 0, correlationId, false, true, null));
    }

    // Steps 4-6 run under the failed action's own scope, installed by step 2.
    private void RouteScopedFailure(object action, string? sliceKey, Exception ex)
    {
        var scope = _causal.Value!;
        var type = TypeName(action);
        RouteFailure(new ReducerFailed(type, sliceKey, ex), ex, type, scope.CorrelationId, scope.InFailure);
    }

    // The per-store cached action type name (§9).
    private string TypeName(object action) => _actionTypes.Of(action);
}

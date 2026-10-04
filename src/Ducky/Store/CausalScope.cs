using System.Diagnostics;

namespace Ducky;

// The causal scope (SPEC §6.5, INV-06): Seq is the Id of the action being processed.
internal sealed record Cause(long Seq, int Depth, long CorrelationId, bool InFailure);

internal sealed partial class Dispatcher
{
    // Default synchronous causal depth above which an action is dropped (§5.1); DuckyBuilder.MaxDispatchDepth sets it.
    internal const int DefaultMaxDispatchDepth = 64;

    // Per store, never static: a scope never crosses stores (§6.5).
    private readonly AsyncLocal<Cause?> _causal = new();

    // Id of the action being processed; 0 between actions and while idle, and Ids start at 1.
    private long _processingSeq;

    internal Cause? Causal => _causal.Value;

    // Called on the producer's flow to create the Pending, for every origin, before Enqueue (§6.3). No scope: depth 0
    // and a new chain (Enqueue sets CorrelationId to the Id). The scope of the action being processed: a synchronous
    // child. Any other scope: the parent has finished, so this is an asynchronous continuation that keeps the
    // correlation and restarts the depth budget. Core failure actions bypass this with an explicit cause (§6.5, INV-06);
    // a DispatchSystem failure action (isFailure) follows it.
    internal Pending NewPending(object action, Origin origin, TaskCompletionSource<DispatchResult>? completion, bool isFailure = false)
    {
        var c = _causal.Value;
        if (c is null)
        {
            return new(action, origin, 0, 0, false, isFailure, completion);
        }

        var child = c.Seq == Volatile.Read(ref _processingSeq);
        return new(action, origin, child ? c.Depth + 1 : 0, c.CorrelationId, child && c.InFailure, isFailure, completion);
    }

    // Step 1. Runs before step 2 installs p's scope.
    private bool DepthExceeded(Pending p)
    {
        if (p.Depth <= maxDispatchDepth)
        {
            return false;
        }

        p.Complete(DispatchResult.Dropped);
        telemetry.Add(telemetry.DispatchDropped, new KeyValuePair<string, object?>("ducky.drop.reason", "depth"));

        // _causal and Activity.Current are still the drainer's here, so the failure takes p's chain, InFailure and
        // producer explicitly.
        var type = TypeName(p.Action);
        var loop = new DispatchLoopException(type, p.Depth);
        RouteFailure(new ReducerFailed(type, null, loop), loop, type, p.CorrelationId, p.InFailure, p.Producer);
        return true;
    }

    // Step 2. Returns the drainer's previous scopes and Activity.Current, which ExitScope restores, and the start of the
    // dispatch duration. The effect-run scope is cleared: a drain owned by an effect continuation processes other
    // producers' actions, which are not that run's (§6.4, §6.6). The producer's activity becomes current (§6.3), so with no
    // span (nobody listens or samples) the reducers and the effects' synchronous prefixes run under it whichever flow
    // drains, never under the drainer's, and the BCL parents a span with a default ParentActivity (no activity, or a
    // hierarchical id) on it. Inline nothing changes, which keeps a stopped activity an effect continuation dispatched
    // under; a stopped one from another flow can't be set (the setter rejects it), so none is. The ducky.dispatch span
    // starts in Process, inside the try whose finally calls ExitScope.
    private Outer EnterScope(Pending p)
    {
        var outer = new Outer(_causal.Value, _effectRun.Value, Activity.Current, timeProvider.GetTimestamp());
        Volatile.Write(ref _processingSeq, p.Id);
        _causal.Value = new(p.Id, p.Depth, p.CorrelationId, p.InFailure || p.IsFailure);
        _effectRun.Value = null;
        if (!ReferenceEquals(outer.Activity, p.Producer))
        {
            Activity.Current = p.Producer is { IsStopped: false } ? p.Producer : null;
        }

        return outer;
    }

    // Restores the scopes first and Activity.Current in a finally, so a fatal listener throw from the telemetry calls can't
    // leave one behind. Activity.Current is restored after Stop, which sets it to what was current when the span started
    // (EnterScope may have cleared it), and explicitly, since a reducer or listener may have started an activity it never
    // stopped. The setter rejects a stopped activity (and throws a first-chance exception doing so), so a stopped one is not
    // set: Drain processes under a stopped ambient inside ProcessIsolatedAsync, which gives it back.
    private void ExitScope(Outer outer, Activity? span)
    {
        _causal.Value = outer.Causal;
        _effectRun.Value = outer.Run;
        Volatile.Write(ref _processingSeq, 0);
        try
        {
            telemetry.Record(telemetry.DispatchDuration, timeProvider.GetElapsedTime(outer.Started).TotalMilliseconds);
            telemetry.Stop(span);
        }
        finally
        {
            if (outer.Activity is not { IsStopped: true })
            {
                Activity.Current = outer.Activity;
            }
        }
    }

    private readonly record struct Outer(Cause? Causal, EffectRunToken? Run, Activity? Activity, long Started);
}

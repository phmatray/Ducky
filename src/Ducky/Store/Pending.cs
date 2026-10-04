using System.Diagnostics;

namespace Ducky;

// One queued action (SPEC §6.3). Only DispatchAsync creates a completion, always with RunContinuationsAsynchronously
// (INV-10), and the dispatcher completes it only after releasing _gate. Depth, CorrelationId and InFailure are
// computed on the producer's flow before Enqueue (Dispatcher.NewPending, §6.5); a CorrelationId of 0 means a new chain.
internal sealed class Pending(
    object action,
    Origin origin,
    int depth,
    long correlationId,
    bool inFailure,
    bool isFailure,
    TaskCompletionSource<DispatchResult>? completion,
    Activity? producer)
{
    // The producer is the creating flow's activity unless given (a core failure action takes the failed action's).
    internal Pending(
        object action,
        Origin origin,
        int depth,
        long correlationId,
        bool inFailure,
        bool isFailure,
        TaskCompletionSource<DispatchResult>? completion)
        : this(action, origin, depth, correlationId, inFailure, isFailure, completion, Activity.Current)
    {
    }

    internal object Action { get; } = action;

    internal Origin Origin { get; } = origin;

    internal int Depth { get; } = depth;

    // Enqueue replaces 0 with the Id, under _gate.
    internal long CorrelationId { get; set; } = correlationId;

    internal bool InFailure { get; } = inFailure;

    // Assigned under _gate in enqueue order; the first Id is 1.
    internal long Id { get; set; }

    // A failure action (a core one, §6.4 steps 1 and 6, or DispatchSystem with isFailure): step 2 marks its scope
    // InFailure, so failures under it are logged only (INV-12).
    internal bool IsFailure { get; } = isFailure;

    // The effect run that dispatched it through EffectContext, checked again at step 3 (§6.4, §6.6); null otherwise.
    internal EffectRunToken? Run { get; set; }

    // The producer's activity, read on the producer's flow when the Pending is created, before Enqueue (a core failure
    // action takes the failed action's): ambient while the action is processed and, through ParentActivity, the parent of
    // its ducky.dispatch span, whichever flow drains it (§6.3, §6.4 step 2).
    internal Activity? Producer { get; } = producer;

    internal ActivityContext ParentActivity => Producer?.Context ?? default;

    internal void Complete(DispatchResult result) => completion?.TrySetResult(result);
}

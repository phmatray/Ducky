namespace Ducky;

// One queued action (SPEC §6.3). Only DispatchAsync creates a completion, always with RunContinuationsAsynchronously
// (INV-10), and the dispatcher completes it only after releasing _gate.
internal sealed class Pending(object action, Origin origin, TaskCompletionSource<DispatchResult>? completion)
{
    internal object Action { get; } = action;

    internal Origin Origin { get; } = origin;

    // Assigned under _gate in enqueue order; the first Id is 1.
    internal long Id { get; set; }

    internal void Complete(DispatchResult result) => completion?.TrySetResult(result);
}

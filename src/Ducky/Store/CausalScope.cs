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

        // _causal is still the drainer's ambient scope here, so the failure takes p's chain and InFailure explicitly.
        var type = TypeName(p.Action);
        RouteFailure(new(type, null, new DispatchLoopException(type, p.Depth)), p.CorrelationId, p.InFailure);
        return true;
    }

    // Step 2. Returns the drainer's previous scope, which ExitScope restores.
    private Cause? EnterScope(Pending p)
    {
        var outer = _causal.Value;
        Volatile.Write(ref _processingSeq, p.Id);
        _causal.Value = new(p.Id, p.Depth, p.CorrelationId, p.InFailure || p.IsFailure);
        return outer;
    }

    private void ExitScope(Cause? outer)
    {
        _causal.Value = outer;
        Volatile.Write(ref _processingSeq, 0);
    }
}

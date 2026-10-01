namespace Ducky;

/// <summary>An action as the store processes it, given to every middleware hook. Immutable.</summary>
public sealed class ActionContext
{
    internal ActionContext(Pending pending, string actionType, StateSnapshot previousState, StateSnapshot state, IReadOnlyList<string> changedKeys)
    {
        Action = pending.Action;
        ActionType = actionType;
        Origin = pending.Origin;
        Depth = pending.Depth;
        Id = pending.Id;
        CorrelationId = pending.CorrelationId;
        PreviousState = previousState;
        State = state;
        ChangedKeys = changedKeys;
    }

    /// <summary>Gets the action.</summary>
    public object Action { get; }

    /// <summary>Gets the action's readable type name (§9).</summary>
    public string ActionType { get; }

    /// <summary>Gets where the action came from.</summary>
    public Origin Origin { get; }

    /// <summary>Gets the action's synchronous causal depth: 0 for a new chain.</summary>
    public int Depth { get; }

    /// <summary>
    /// Gets the per-store id, strictly increasing in enqueue order. It is not monotonic in processing order across
    /// the init gate; use your own counter for processing order.
    /// </summary>
    public long Id { get; }

    /// <summary>Gets the <see cref="Id"/> of the root action of the causal chain.</summary>
    public long CorrelationId { get; }

    /// <summary>Gets the state before the reduce.</summary>
    public StateSnapshot PreviousState { get; }

    /// <summary>Gets the state after the reduce; the same as <see cref="PreviousState"/> before it.</summary>
    public StateSnapshot State { get; }

    /// <summary>Gets the keys of the slices the reduce changed; empty before it.</summary>
    public IReadOnlyList<string> ChangedKeys { get; }
}

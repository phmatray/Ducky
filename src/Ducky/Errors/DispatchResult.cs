namespace Ducky;

/// <summary>How a dispatched action ended.</summary>
public enum DispatchResult
{
    /// <summary>The action was reduced (its state committed, possibly unchanged).</summary>
    Reduced,

    /// <summary>A middleware's <c>MayDispatch</c> refused the action.</summary>
    Vetoed,

    /// <summary>The action was dropped: it exceeded <c>MaxDispatchDepth</c>, or its effect run was superseded.</summary>
    Dropped,

    /// <summary>A reducer, <c>MayDispatch</c> or <c>BeforeReduce</c> threw; nothing was committed.</summary>
    Failed,

    /// <summary>The store's disposal had begun.</summary>
    Disposed,
}

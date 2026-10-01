namespace Ducky;

/// <summary>
/// The exception of the <see cref="ReducerFailed"/> enqueued when an action exceeds <c>MaxDispatchDepth</c> and is
/// dropped.
/// </summary>
public sealed class DispatchLoopException : Exception
{
    internal DispatchLoopException(string actionType, int depth)
        : base($"Action '{actionType}' was dropped at causal depth {depth}, above MaxDispatchDepth: something dispatches it in a synchronous loop (a reducer, middleware, subscriber or effect prefix that dispatches what it handles).")
    {
        ActionType = actionType;
        Depth = depth;
    }

    /// <summary>Gets the action type name of the dropped action.</summary>
    public string ActionType { get; }

    /// <summary>Gets the causal depth of the dropped action.</summary>
    public int Depth { get; }
}

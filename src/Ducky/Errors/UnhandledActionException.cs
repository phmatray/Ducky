namespace Ducky;

/// <summary>
/// The exception of the <see cref="ReducerFailed"/> enqueued, when <c>ThrowOnUnhandledAction</c> is set, for an action
/// that no reducer or async effect handles.
/// </summary>
public sealed class UnhandledActionException : Exception
{
    internal UnhandledActionException(string actionType)
        : base($"Action '{actionType}' matched no reducer or async effect and ThrowOnUnhandledAction is set.") =>
        ActionType = actionType;

    /// <summary>Gets the action type name of the unhandled action.</summary>
    public string ActionType { get; }
}

namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action is aborted during processing.
/// </summary>
public class ActionAbortedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionAbortedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The context associated with the action.</param>
    /// <param name="reason">The reason for aborting the action.</param>
    public ActionAbortedEventArgs(IActionContext context, string reason)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// The context associated with the action.
    /// </summary>
    public IActionContext Context { get; }

    /// <summary>
    /// The reason for aborting the action.
    /// </summary>
    public string Reason { get; }
}

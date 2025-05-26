namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action finishes processing.
/// </summary>
public class ActionCompletedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCompletedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The context associated with the action.</param>
    public ActionCompletedEventArgs(ActionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// The context associated with the action.
    /// </summary>
    public ActionContext Context { get; }
}

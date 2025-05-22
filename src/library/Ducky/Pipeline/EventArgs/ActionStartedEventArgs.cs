namespace Ducky.Pipeline;

/// <summary>
/// Event published when an action starts processing.
/// </summary>
public class ActionStartedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionStartedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The context associated with the action.</param>
    public ActionStartedEventArgs(IActionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// The context associated with the action.
    /// </summary>
    public IActionContext Context { get; }
}

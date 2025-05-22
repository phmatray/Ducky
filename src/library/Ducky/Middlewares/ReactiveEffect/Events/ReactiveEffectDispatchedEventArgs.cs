using Ducky.Pipeline;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Event published when a reactive effect dispatches an action.
/// </summary>
public class ReactiveEffectDispatchedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveEffectDispatchedEventArgs"/> class.
    /// </summary>
    public ReactiveEffectDispatchedEventArgs(object action)
    {
        Action = action;
    }

    /// <summary>
    /// The action that was dispatched.
    /// </summary>
    public object Action { get; }
}

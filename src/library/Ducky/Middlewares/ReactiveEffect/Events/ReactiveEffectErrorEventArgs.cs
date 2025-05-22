using Ducky.Pipeline;

namespace Ducky.Middlewares.ReactiveEffect;

/// <summary>
/// Event published when a reactive effect encounters an error.
/// </summary>
public class ReactiveEffectErrorEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReactiveEffectErrorEventArgs"/> class.
    /// </summary>
    public ReactiveEffectErrorEventArgs(object? action, Exception exception)
    {
        Action = action;
        Exception = exception;
    }

    /// <summary>
    /// The action that caused the error, if any.
    /// </summary>
    public object? Action { get; }
    
    /// <summary>
    /// The exception that was thrown.
    /// </summary>
    public Exception Exception { get; }
}

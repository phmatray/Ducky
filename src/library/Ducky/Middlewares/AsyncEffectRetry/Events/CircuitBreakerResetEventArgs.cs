using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Provides data for the event raised when a circuit breaker is reset after being previously opened.
/// </summary>
public class CircuitBreakerResetEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerResetEventArgs"/> class.
    /// </summary>
    /// <param name="context">The action context in which the reset occurred.</param>
    /// <param name="action">The action that triggered the reset.</param>
    public CircuitBreakerResetEventArgs(ActionContext context, object action)
    {
        Context = context;
        Action = action;
    }

    /// <summary>
    /// Gets the action context in which the reset occurred.
    /// </summary>
    public ActionContext Context { get; }

    /// <summary>
    /// Gets the action that triggered the reset.
    /// </summary>
    public object Action { get; }
}

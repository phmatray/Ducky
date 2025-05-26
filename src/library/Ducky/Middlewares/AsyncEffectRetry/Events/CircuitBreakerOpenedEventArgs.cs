using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Provides data for the event raised when a circuit breaker is opened due to an exception during action processing.
/// </summary>
public class CircuitBreakerOpenedEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerOpenedEventArgs"/> class.
    /// </summary>
    /// <param name="context">The action context in which the exception occurred.</param>
    /// <param name="action">The action that triggered the circuit breaker.</param>
    /// <param name="exception">The exception that caused the circuit breaker to open.</param>
    public CircuitBreakerOpenedEventArgs(
        ActionContext context,
        object action,
        Exception exception)
    {
        Context = context;
        Action = action;
        Exception = exception;
    }

    /// <summary>
    /// Gets the action context in which the exception occurred.
    /// </summary>
    public ActionContext Context { get; }

    /// <summary>
    /// Gets the action that triggered the circuit breaker.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the exception that caused the circuit breaker to open.
    /// </summary>
    public Exception Exception { get; }
}

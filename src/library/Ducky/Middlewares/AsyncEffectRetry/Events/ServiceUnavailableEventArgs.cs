using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Provides data for the event raised when a service becomes unavailable during action processing.
/// </summary>
public class ServiceUnavailableEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceUnavailableEventArgs"/> class.
    /// </summary>
    /// <param name="context">The action context in which the service became unavailable.</param>
    /// <param name="action">The action that triggered the service unavailability event.</param>
    /// <param name="reason">The reason why the service is unavailable.</param>
    /// <param name="exception">The exception that caused the service to become unavailable, if any.</param>
    public ServiceUnavailableEventArgs(
        IActionContext context,
        object action,
        string reason,
        Exception exception)
    {
        Context = context;
        Action = action;
        Reason = reason;
        Exception = exception;
    }

    /// <summary>
    /// Gets the action context in which the service became unavailable.
    /// </summary>
    public IActionContext Context { get; }

    /// <summary>
    /// Gets the action that triggered the service unavailability event.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the reason why the service is unavailable.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the exception that caused the service to become unavailable, if any.
    /// </summary>
    public Exception Exception { get; }
}

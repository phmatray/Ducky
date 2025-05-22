using Ducky.Pipeline;

namespace Ducky.Middlewares.AsyncEffectRetry;

/// <summary>
/// Provides data for the event raised when a retry attempt is made after an exception during action processing.
/// </summary>
public class RetryAttemptEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryAttemptEventArgs"/> class.
    /// </summary>
    /// <param name="context">The action context in which the retry is occurring.</param>
    /// <param name="action">The action being retried.</param>
    /// <param name="attempt">The current retry attempt number.</param>
    /// <param name="exception">The exception that caused the retry.</param>
    public RetryAttemptEventArgs(
        IActionContext context,
        object action,
        int attempt,
        Exception exception)
    {
        Context = context;
        Action = action;
        Attempt = attempt;
        Exception = exception;
    }

    /// <summary>
    /// Gets the action context in which the retry is occurring.
    /// </summary>
    public IActionContext Context { get; }

    /// <summary>
    /// Gets the action being retried.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// Gets the current retry attempt number.
    /// </summary>
    public int Attempt { get; }

    /// <summary>
    /// Gets the exception that caused the retry.
    /// </summary>
    public Exception Exception { get; }
}

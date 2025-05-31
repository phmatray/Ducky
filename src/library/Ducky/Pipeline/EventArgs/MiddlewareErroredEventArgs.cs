using Ducky.Middlewares;

namespace Ducky.Pipeline;

/// <summary>
/// Event published when a middleware throws an error while processing an action.
/// </summary>
public class MiddlewareErroredEventArgs : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareErroredEventArgs"/> class.
    /// </summary>
    /// <param name="action">The action being processed.</param>
    /// <param name="middleware">The middleware instance.</param>
    /// <param name="phase">The phase, either "Before" or "After".</param>
    /// <param name="exception">The exception thrown.</param>
    public MiddlewareErroredEventArgs(object action, IMiddleware middleware, StoreMiddlewarePhase phase, Exception exception)
    {
        Action = action;
        Middleware = middleware;
        Phase = phase;
        Exception = exception;
    }

    /// <summary>
    /// The action being processed.
    /// </summary>
    public object Action { get; }

    /// <summary>
    /// The middleware instance.
    /// </summary>
    public IMiddleware Middleware { get; }

    /// <summary>
    /// The phase, either "Before" or "After".
    /// </summary>
    public StoreMiddlewarePhase Phase { get; }

    /// <summary>
    /// The exception thrown.
    /// </summary>
    public Exception Exception { get; }
}

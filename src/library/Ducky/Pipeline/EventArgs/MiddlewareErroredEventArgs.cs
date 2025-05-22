using Ducky.Middlewares;

namespace Ducky.Pipeline;

/// <summary>
/// Event published when a middleware throws an error while processing an action.
/// </summary>
public class MiddlewareErroredEventArgs : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareErroredEventArgs"/> class.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="middleware">The middleware instance.</param>
    /// <param name="phase">The phase, either "Before" or "After".</param>
    /// <param name="exception">The exception thrown.</param>
    public MiddlewareErroredEventArgs(IActionContext context, IStoreMiddleware middleware, StoreMiddlewarePhase phase, Exception exception)
    {
        Context = context;
        Middleware = middleware;
        Phase = phase;
        Exception = exception;
    }

    /// <summary>
    /// The action context.
    /// </summary>
    public IActionContext Context { get; }

    /// <summary>
    /// The middleware instance.
    /// </summary>
    public IStoreMiddleware Middleware { get; }

    /// <summary>
    /// The phase, either "Before" or "After".
    /// </summary>
    public StoreMiddlewarePhase Phase { get; }

    /// <summary>
    /// The exception thrown.
    /// </summary>
    public Exception Exception { get; }
}

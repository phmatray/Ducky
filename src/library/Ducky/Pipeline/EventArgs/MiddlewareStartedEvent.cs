using Ducky.Middlewares;

namespace Ducky.Pipeline;

/// <summary>
/// Event published when a middleware begins processing an action.
/// </summary>
public class MiddlewareStartedEvent : StoreEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareStartedEvent"/> class.
    /// </summary>
    /// <param name="action">The action being processed.</param>
    /// <param name="middleware">The middleware instance.</param>
    /// <param name="phase">The phase, either "Before" or "After".</param>
    public MiddlewareStartedEvent(object action, IMiddleware middleware, StoreMiddlewarePhase phase)
    {
        Action = action;
        Middleware = middleware;
        Phase = phase;
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
}

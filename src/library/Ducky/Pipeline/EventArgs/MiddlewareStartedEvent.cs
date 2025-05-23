using Ducky.Middlewares;
using Ducky.Pipeline.Reactive;

namespace Ducky.Pipeline;

/// <summary>
/// Event published when a middleware begins processing an action.
/// </summary>
public class MiddlewareStartedEvent : PipelineEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MiddlewareStartedEvent"/> class.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="middleware">The middleware instance.</param>
    /// <param name="phase">The phase, either "Before" or "After".</param>
    public MiddlewareStartedEvent(ActionContext context, IStoreMiddleware middleware, StoreMiddlewarePhase phase)
    {
        Context = context;
        Middleware = middleware;
        Phase = phase;
    }

    /// <summary>
    /// The action context.
    /// </summary>
    public ActionContext Context { get; }

    /// <summary>
    /// The middleware instance.
    /// </summary>
    public IStoreMiddleware Middleware { get; }

    /// <summary>
    /// The phase, either "Before" or "After".
    /// </summary>
    public StoreMiddlewarePhase Phase { get; }
}

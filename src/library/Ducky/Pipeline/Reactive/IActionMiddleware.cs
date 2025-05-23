using R3;

namespace Ducky.Pipeline.Reactive;

/// <summary>
/// A reactive middleware: transforms an incoming stream of ActionContext, returning a new stream.
/// </summary>
public interface IActionMiddleware
{
    /// <summary>
    /// Invoke the middleware with an incoming stream of ActionContext.
    /// </summary>
    /// <param name="actions">The incoming stream of ActionContext.</param>
    /// <returns>The transformed stream of ActionContext.</returns>
    Observable<ActionContext> Invoke(Observable<ActionContext> actions);
}

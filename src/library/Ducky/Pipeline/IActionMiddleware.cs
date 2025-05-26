using R3;

namespace Ducky.Pipeline;

/// <summary>
/// Defines a reactive middleware that can process actions before and after the reducer.
/// </summary>
public interface IActionMiddleware
{
    /// <summary>
    /// Invoked before the reducer is executed, allowing the middleware to transform, filter,
    /// or augment the action stream.
    /// </summary>
    /// <param name="actions">The incoming stream of <see cref="ActionContext"/>.</param>
    /// <returns>
    /// The transformed stream of <see cref="ActionContext"/> that will be processed by the next middleware or the reducer.
    /// </returns>
    Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions);

    /// <summary>
    /// Invoked after the reducer has executed, allowing the middleware to perform additional processing,
    /// such as side-effects, logging, or notifications.
    /// </summary>
    /// <param name="actions">The stream of <see cref="ActionContext"/> produced after state has been updated.</param>
    /// <returns>
    /// The transformed stream of <see cref="ActionContext"/> that will be processed by the next after-reduce middleware or subscriber.
    /// </returns>
    Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions);
}

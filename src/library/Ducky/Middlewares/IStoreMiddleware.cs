using Ducky.Pipeline;
using Ducky.Pipeline.Reactive;

namespace Ducky.Middlewares;

/// <summary>
/// Contract for a middleware component in the Redux-style pipeline.
/// </summary>
public interface IStoreMiddleware
{
    /// <summary>
    /// Initializes the middleware with the dispatcher and store.
    /// </summary>
    /// <param name="dispatcher">The action dispatcher.</param>
    /// <param name="store">The Redux store.</param>
    /// <param name="eventPublisher">The event publisher for pipeline events.</param>
    /// <returns>A task representing the initialization operation.</returns>
    Task InitializeAsync(
        IDispatcher dispatcher,
        IStore store,
        IPipelineEventPublisher eventPublisher);

    /// <summary>
    /// Determines if the middleware can handle actions of the specified type.
    /// </summary>
    /// <param name="actionType">The action type.</param>
    /// <returns>True if can handle, otherwise false.</returns>
    bool CanHandle(Type actionType);

    /// <summary>
    /// Executes before the action is processed (async version).
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task BeforeDispatchAsync(
        ActionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after the action is processed (async version).
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AfterDispatchAsync(
        ActionContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether async execution should be awaited or run fire-and-forget.
    /// </summary>
    StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.Await;
}

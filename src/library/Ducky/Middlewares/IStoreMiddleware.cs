using Ducky.Pipeline;

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
    /// <returns>A task representing the initialization operation.</returns>
    Task InitializeAsync(IDispatcher dispatcher, IStore store);

    /// <summary>
    /// Determines if the middleware can handle actions of the specified type.
    /// </summary>
    /// <param name="actionType">The action type.</param>
    /// <returns>True if can handle, otherwise false.</returns>
    bool CanHandle(Type actionType);

    /// <summary>
    /// Executes before the action is processed (async version).
    /// </summary>
    /// <typeparam name="TAction">The action type.</typeparam>
    /// <param name="context">The action context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes after the action is processed (async version).
    /// </summary>
    /// <typeparam name="TAction">The action type.</typeparam>
    /// <param name="context">The action context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indicates whether async execution should be awaited or run fire-and-forget.
    /// </summary>
    StoreMiddlewareAsyncMode AsyncMode
        => StoreMiddlewareAsyncMode.Await;
}

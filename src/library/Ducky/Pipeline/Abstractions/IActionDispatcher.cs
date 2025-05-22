using Ducky.Middlewares;

namespace Ducky.Pipeline;

/// <summary>
/// Dispatches actions through the middleware pipeline.
/// </summary>
public interface IActionDispatcher
{
    /// <summary>
    /// Dispatches the specified action context through the list of middlewares.
    /// </summary>
    /// <param name="context">The action context.</param>
    /// <param name="middlewares">The middlewares to execute.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task DispatchAsync(
        IActionContext context,
        List<IStoreMiddleware> middlewares,
        CancellationToken cancellationToken = default);
}

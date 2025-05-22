namespace Ducky.Pipeline;

/// <summary>
/// A pipeline for processing actions using registered Redux-style middleware.
/// </summary>
public interface IReduxMiddlewarePipeline
{
    /// <summary>
    /// Processes the specified action asynchronously using the middleware pipeline.
    /// </summary>
    /// <typeparam name="TAction">The type of the action.</typeparam>
    /// <param name="action">The action to process.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    Task ProcessAsync<TAction>(TAction action, CancellationToken cancellationToken = default);
}

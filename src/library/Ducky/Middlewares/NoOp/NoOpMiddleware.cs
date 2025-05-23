using Ducky.Pipeline;

namespace Ducky.Middlewares.NoOp;

/// <summary>
/// A no-op middleware that does nothing. Useful for testing, benchmarking, or as a placeholder.
/// </summary>
public sealed class NoOpMiddleware : StoreMiddleware
{
    /// <summary>
    /// No operation before action.
    /// </summary>
    public override async Task BeforeDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// No operation after action.
    /// </summary>
    public override async Task AfterDispatchAsync<TAction>(
        ActionContext<TAction> context,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
    }
}

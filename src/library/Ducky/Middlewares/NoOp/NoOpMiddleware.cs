using Ducky.Pipeline;

namespace Ducky.Middlewares.NoOp;

/// <summary>
/// A no-op middleware that simply passes through all actions unchanged.
/// Useful for testing, benchmarking, or as a pipeline placeholder.
/// </summary>
public sealed class NoOpMiddleware : MiddlewareBase
{
    /// <inheritdoc />
    public override Task InitializeAsync(IDispatcher dispatcher, IStore store)
        => Task.CompletedTask;

    /// <inheritdoc />
    public override void BeforeDispatch(object action)
    {
        // Nothing to do
    }

    /// <inheritdoc />
    public override void AfterDispatch(object action)
    {
        // Nothing to do
    }
}

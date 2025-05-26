using Ducky.Pipeline;
using R3;

namespace Ducky.Middlewares.NoOp;

/// <summary>
/// A no-op middleware that simply passes through all actions unchanged.
/// Useful for testing, benchmarking, or as a pipeline placeholder.
/// </summary>
public sealed class NoOpMiddleware : IActionMiddleware
{
    /// <inheritdoc />
    public Observable<ActionContext> InvokeBeforeReduce(Observable<ActionContext> actions)
    {
        return actions;
    }

    /// <inheritdoc />
    public Observable<ActionContext> InvokeAfterReduce(Observable<ActionContext> actions)
    {
        return actions;
    }
}

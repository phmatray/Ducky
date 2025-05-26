using Ducky.Pipeline;
using R3;

namespace Ducky;

/// <summary>
/// Represents an observer that processes actions in a reactive manner.
/// </summary>
/// <param name="onNext">The action to execute when an action context is received.</param>
public sealed class SliceObserver(Action<ActionContext> onNext) : Observer<ActionContext>
{
    /// <inheritdoc />
    protected override void OnNextCore(ActionContext value)
    {
        onNext(value);
    }

    /// <inheritdoc />
    protected override void OnErrorResumeCore(Exception error)
    {
        // Log or ignore
    }

    /// <inheritdoc />
    protected override void OnCompletedCore(Result result)
    {
        // Optional: handle completion
    }
}

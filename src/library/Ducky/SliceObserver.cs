using Ducky.Pipeline;
using R3;

namespace Ducky;

internal sealed class SliceObserver(Action<ActionContext> onNext) : Observer<ActionContext>
{
    protected override void OnNextCore(ActionContext value)
    {
        onNext(value);
    }

    protected override void OnErrorResumeCore(Exception error)
    {
        // Log or ignore
    }

    protected override void OnCompletedCore(Result result)
    {
        // Optional: handle completion
    }
}

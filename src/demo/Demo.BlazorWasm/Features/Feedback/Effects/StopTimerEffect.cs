using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that stops the timer.
/// </summary>
public class StopTimerEffect : AsyncEffect<StopTimer>
{
    /// <inheritdoc />
    public override Task HandleAsync(StopTimer action, IRootState rootState)
    {
        // Cancel the timer if it exists
        StartTimerEffect._timerCancellationTokenSource?.Cancel();
        StartTimerEffect._timerCancellationTokenSource = null;

        return Task.CompletedTask;
    }
}

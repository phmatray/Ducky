// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Effect that starts the timer and manages ticks.
/// </summary>
public class StartTimerEffect : AsyncEffect<StartTimer>
{
    internal static CancellationTokenSource? _timerCancellationTokenSource;

    /// <inheritdoc />
    public override async Task HandleAsync(StartTimer action, IRootState rootState)
    {
        // Cancel any existing timer
        _timerCancellationTokenSource?.Cancel();
        _timerCancellationTokenSource = new CancellationTokenSource();

        CancellationToken cancellationToken = _timerCancellationTokenSource.Token;

        // Start the timer loop
        await Task.Run(
            async () =>
            {
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

                        if (!cancellationToken.IsCancellationRequested)
                        {
                            TimerState timerState = rootState.GetSliceState<TimerState>();

                            // Check if timer is still running
                            if (timerState.IsRunning)
                            {
                                Dispatcher?.Dispatch(new Tick());

                                // Check if timer reached zero (optional: auto-stop at zero)
                                if (timerState.Time >= 60) // Stop at 60 seconds
                                {
                                    Dispatcher?.Dispatch(new StopTimer());
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Timer was cancelled, this is expected
                }
            },
            cancellationToken);
    }
}

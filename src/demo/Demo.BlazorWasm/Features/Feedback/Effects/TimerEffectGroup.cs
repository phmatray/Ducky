// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.AppStore;
using Ducky.Middlewares.AsyncEffect;

namespace Demo.BlazorWasm.Features.Feedback.Effects;

/// <summary>
/// Groups all timer-related effects with shared timer management logic.
/// </summary>
public class TimerEffectGroup : AsyncEffectGroup
{
    private readonly ILogger<TimerEffectGroup> _logger;
    private CancellationTokenSource? _timerCancellationTokenSource;

    public TimerEffectGroup(ILogger<TimerEffectGroup> logger)
    {
        _logger = logger;

        On<StartTimer>(HandleStartTimerAsync);
        On<StopTimer>(HandleStopTimerAsync);
        On<ResetTimer>(HandleResetTimerAsync);
        On<Tick>(HandleTickAsync);
    }

    private async Task HandleStartTimerAsync(StartTimer action, IRootState rootState)
    {
        _logger.LogInformation("Starting timer");

        // Cancel any existing timer
        StopCurrentTimer();

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

                        if (!cancellationToken.IsCancellationRequested && IsTimerRunning(rootState))
                        {
                            Dispatcher.Tick();

                            // Auto-stop at 60 seconds
                            if (GetCurrentTime(rootState) >= 60)
                            {
                                Dispatcher.StopTimer();
                                break;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Timer was cancelled");
                }
            },
            cancellationToken);
    }

    private Task HandleStopTimerAsync(StopTimer action, IRootState rootState)
    {
        _logger.LogInformation("Stopping timer");
        StopCurrentTimer();
        return Task.CompletedTask;
    }

    private Task HandleResetTimerAsync(ResetTimer action, IRootState rootState)
    {
        _logger.LogInformation("Resetting timer");
        StopCurrentTimer();
        // The reducer will handle resetting the time to 0
        return Task.CompletedTask;
    }

    private Task HandleTickAsync(Tick action, IRootState rootState)
    {
        TimerState timerState = rootState.GetSliceState<TimerState>();
        _logger.LogDebug("Timer tick: {Time}s", timerState.Time);

        // Could add additional logic here like notifications at certain intervals
        if (timerState.Time % 10 == 0 && timerState.Time > 0)
        {
            InfoNotification notification = new($"Timer: {timerState.Time} seconds");
            Dispatcher.AddNotification(notification);
        }

        return Task.CompletedTask;
    }

    // Shared helper methods
    private void StopCurrentTimer()
    {
        _timerCancellationTokenSource?.Cancel();
        _timerCancellationTokenSource?.Dispose();
        _timerCancellationTokenSource = null;
    }

    private static bool IsTimerRunning(IRootState rootState)
    {
        TimerState timerState = rootState.GetSliceState<TimerState>();
        return timerState.IsRunning;
    }

    private static int GetCurrentTime(IRootState rootState)
    {
        TimerState timerState = rootState.GetSliceState<TimerState>();
        return timerState.Time;
    }
}

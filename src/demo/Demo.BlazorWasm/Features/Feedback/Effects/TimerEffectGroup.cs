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

    private async Task HandleStartTimerAsync(StartTimer action, IStateProvider stateProvider)
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

                        if (!cancellationToken.IsCancellationRequested && IsTimerRunning(stateProvider))
                        {
                            Dispatcher.Tick();

                            // Auto-stop at 60 seconds
                            if (GetCurrentTime(stateProvider) >= 60)
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

    private Task HandleStopTimerAsync(StopTimer action, IStateProvider stateProvider)
    {
        _logger.LogInformation("Stopping timer");
        StopCurrentTimer();
        return Task.CompletedTask;
    }

    private Task HandleResetTimerAsync(ResetTimer action, IStateProvider stateProvider)
    {
        _logger.LogInformation("Resetting timer");
        StopCurrentTimer();
        // The reducer will handle resetting the time to 0
        return Task.CompletedTask;
    }

    private Task HandleTickAsync(Tick action, IStateProvider stateProvider)
    {
        TimerState timerState = stateProvider.GetSlice<TimerState>();
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

    private static bool IsTimerRunning(IStateProvider stateProvider)
    {
        TimerState timerState = stateProvider.GetSlice<TimerState>();
        return timerState.IsRunning;
    }

    private static int GetCurrentTime(IStateProvider stateProvider)
    {
        TimerState timerState = stateProvider.GetSlice<TimerState>();
        return timerState.Time;
    }
}

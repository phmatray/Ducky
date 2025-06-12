// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;

namespace Ducky.Reactive.Monitoring;

/// <summary>
/// Interface for monitoring reactive effects execution and performance.
/// </summary>
public interface IReactiveEffectMonitor
{
    /// <summary>
    /// Called when an effect starts processing an action.
    /// </summary>
    /// <param name="effectType">The type of effect.</param>
    /// <param name="action">The action being processed.</param>
    void OnEffectStarted(Type effectType, object action);

    /// <summary>
    /// Called when an effect completes processing an action.
    /// </summary>
    /// <param name="effectType">The type of effect.</param>
    /// <param name="action">The action that was processed.</param>
    /// <param name="duration">The processing duration.</param>
    /// <param name="resultCount">Number of actions produced.</param>
    void OnEffectCompleted(Type effectType, object action, TimeSpan duration, int resultCount);

    /// <summary>
    /// Called when an effect encounters an error.
    /// </summary>
    /// <param name="effectType">The type of effect.</param>
    /// <param name="action">The action being processed.</param>
    /// <param name="error">The error that occurred.</param>
    void OnEffectError(Type effectType, object action, Exception error);

    /// <summary>
    /// Called when an effect is initialized.
    /// </summary>
    /// <param name="effectType">The type of effect.</param>
    void OnEffectInitialized(Type effectType);

    /// <summary>
    /// Called when an effect is disposed.
    /// </summary>
    /// <param name="effectType">The type of effect.</param>
    void OnEffectDisposed(Type effectType);
}

/// <summary>
/// Default implementation of effect monitor that logs to ILogger.
/// </summary>
public class LoggingEffectMonitor : IReactiveEffectMonitor
{
    private readonly ILogger<LoggingEffectMonitor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingEffectMonitor"/> class.
    /// </summary>
    /// <param name="logger">The logger to use.</param>
    public LoggingEffectMonitor(ILogger<LoggingEffectMonitor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void OnEffectStarted(Type effectType, object action)
    {
        _logger.LogDebug(
            "Effect {EffectType} started processing {ActionType}",
            effectType.Name,
            action.GetType().Name);
    }

    /// <inheritdoc/>
    public void OnEffectCompleted(Type effectType, object action, TimeSpan duration, int resultCount)
    {
        _logger.LogDebug(
            "Effect {EffectType} completed {ActionType} in {Duration}ms, produced {ResultCount} actions",
            effectType.Name,
            action.GetType().Name,
            duration.TotalMilliseconds,
            resultCount);
    }

    /// <inheritdoc/>
    public void OnEffectError(Type effectType, object action, Exception error)
    {
        _logger.LogError(
            error,
            "Effect {EffectType} failed processing {ActionType}",
            effectType.Name,
            action.GetType().Name);
    }

    /// <inheritdoc/>
    public void OnEffectInitialized(Type effectType)
    {
        _logger.LogInformation(
            "Effect {EffectType} initialized",
            effectType.Name);
    }

    /// <inheritdoc/>
    public void OnEffectDisposed(Type effectType)
    {
        _logger.LogInformation(
            "Effect {EffectType} disposed",
            effectType.Name);
    }
}

/// <summary>
/// Metrics-based effect monitor for performance tracking.
/// </summary>
public class MetricsEffectMonitor : IReactiveEffectMonitor
{
    private readonly Dictionary<Type, EffectMetrics> _metrics = [];
    private readonly object _lock = new();

    /// <summary>
    /// Gets the metrics for all effects.
    /// </summary>
    public IReadOnlyDictionary<Type, EffectMetrics> Metrics
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<Type, EffectMetrics>(_metrics);
            }
        }
    }

    /// <inheritdoc/>
    public void OnEffectStarted(Type effectType, object action)
    {
        // No action needed for start
    }

    /// <inheritdoc/>
    public void OnEffectCompleted(Type effectType, object action, TimeSpan duration, int resultCount)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(effectType, out EffectMetrics? metrics))
            {
                metrics = new EffectMetrics();
                _metrics[effectType] = metrics;
            }

            metrics.TotalExecutions++;
            metrics.TotalDuration += duration;
            metrics.TotalActionsProduced += resultCount;

            if (duration > metrics.MaxDuration)
            {
                metrics.MaxDuration = duration;
            }

            if (metrics.MinDuration == TimeSpan.Zero || duration < metrics.MinDuration)
            {
                metrics.MinDuration = duration;
            }
        }
    }

    /// <inheritdoc/>
    public void OnEffectError(Type effectType, object action, Exception error)
    {
        lock (_lock)
        {
            if (!_metrics.TryGetValue(effectType, out EffectMetrics? metrics))
            {
                metrics = new EffectMetrics();
                _metrics[effectType] = metrics;
            }

            metrics.ErrorCount++;
        }
    }

    /// <inheritdoc/>
    public void OnEffectInitialized(Type effectType)
    {
        lock (_lock)
        {
            if (!_metrics.ContainsKey(effectType))
            {
                _metrics[effectType] = new EffectMetrics();
            }
        }
    }

    /// <inheritdoc/>
    public void OnEffectDisposed(Type effectType)
    {
        // No action needed for disposal
    }
}

/// <summary>
/// Metrics for a single effect type.
/// </summary>
public class EffectMetrics
{
    /// <summary>
    /// Gets or sets the total number of executions.
    /// </summary>
    public int TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the total duration of all executions.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the maximum duration of a single execution.
    /// </summary>
    public TimeSpan MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the minimum duration of a single execution.
    /// </summary>
    public TimeSpan MinDuration { get; set; }

    /// <summary>
    /// Gets or sets the total number of actions produced.
    /// </summary>
    public int TotalActionsProduced { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets the average duration per execution.
    /// </summary>
    public TimeSpan AverageDuration
        => TotalExecutions > 0
            ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalExecutions)
            : TimeSpan.Zero;

    /// <summary>
    /// Gets the average actions produced per execution.
    /// </summary>
    public double AverageActionsProduced
        => TotalExecutions > 0
            ? (double)TotalActionsProduced / TotalExecutions
            : 0;

    /// <summary>
    /// Gets the error rate as a percentage.
    /// </summary>
    public double ErrorRate
        => TotalExecutions > 0
            ? (double)ErrorCount / TotalExecutions * 100
            : 0;
}

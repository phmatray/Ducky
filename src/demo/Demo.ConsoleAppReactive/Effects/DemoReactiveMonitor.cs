using System.Collections.Concurrent;

namespace Demo.ConsoleAppReactive.Effects;

public class DemoReactiveMonitor : IReactiveEffectMonitor
{
    private readonly ILogger<DemoReactiveMonitor> _logger;
    private readonly ConcurrentDictionary<Type, EffectMetrics> _metrics = [];

    public DemoReactiveMonitor(ILogger<DemoReactiveMonitor> logger)
    {
        _logger = logger;
    }

    public void OnEffectStarted(Type effectType, object action)
    {
        EffectMetrics metrics = _metrics.GetOrAdd(effectType, _ => new EffectMetrics());
        metrics.StartCount++;

        _logger.LogDebug(
            "Effect started: {EffectType} with action {ActionType}",
            effectType.Name,
            action?.GetType().Name ?? "None");
    }

    public void OnEffectCompleted(Type effectType, object action, TimeSpan duration, int resultCount)
    {
        if (!_metrics.TryGetValue(effectType, out EffectMetrics? metrics))
        {
            return;
        }

        metrics.TotalExecutions++;
        metrics.TotalDuration += duration;
        metrics.TotalActionsProduced += resultCount;

        _logger.LogDebug(
            "Effect completed: {EffectType} in {Duration}ms with {ResultCount} actions",
            effectType.Name,
            duration.TotalMilliseconds,
            resultCount);
    }

    public void OnEffectError(Type effectType, object action, Exception error)
    {
        if (!_metrics.TryGetValue(effectType, out EffectMetrics? metrics))
        {
            return;
        }

        metrics.ErrorCount++;

        _logger.LogWarning(
            error,
            "Effect error: {EffectType} processing {ActionType}",
            effectType.Name,
            action?.GetType().Name ?? "None");
    }

    public void OnEffectInitialized(Type effectType)
    {
        _metrics.GetOrAdd(effectType, _ => new EffectMetrics());
        _logger.LogInformation("Effect initialized: {EffectType}", effectType.Name);
    }

    public void OnEffectDisposed(Type effectType)
    {
        _logger.LogInformation("Effect disposed: {EffectType}", effectType.Name);
    }

    public IReadOnlyDictionary<Type, EffectMetrics> GetMetrics() => _metrics;

    public class EffectMetrics
    {
        public int StartCount { get; set; }
        public int TotalExecutions { get; set; }
        public int ErrorCount { get; set; }
        public int TotalActionsProduced { get; set; }
        public TimeSpan TotalDuration { get; set; }

        public TimeSpan AverageDuration => TotalExecutions > 0 
            ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalExecutions) 
            : TimeSpan.Zero;
    }
}

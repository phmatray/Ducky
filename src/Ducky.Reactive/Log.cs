using Microsoft.Extensions.Logging;

namespace Ducky.Reactive;

// Ducky.Reactive log events (SPEC §9): EventIds 3000-3999, types only, never values. M11-01 takes 3000-3003, which SPEC
// does not assign (the host's per-effect start, a reactive effect's throwing disposal, a pipeline's throwing teardown, a
// reactive failure); M11-02 takes 3004 (resubscribing stopped).
internal static partial class Log
{
    [LoggerMessage(EventId = 3000, Level = LogLevel.Debug, Message = "Reactive effect {EffectType} subscribed")]
    internal static partial void EffectSubscribed(SafeLogger logger, Type effectType);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Error, Message = "Disposing reactive effect {EffectType} threw")]
    internal static partial void EffectDisposeThrew(SafeLogger logger, Exception exception, Type effectType);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Error, Message = "Unsubscribing reactive effect {EffectType} threw")]
    internal static partial void SubscriptionDisposeThrew(SafeLogger logger, Exception exception, Type effectType);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error, Message = "Reactive effect {EffectType} failed")]
    internal static partial void EffectFailed(SafeLogger logger, Exception exception, Type effectType);

    [LoggerMessage(EventId = 3004, Level = LogLevel.Warning, Message = "Reactive effect {EffectType} stopped resubscribing after {Attempts} failures")]
    internal static partial void ResubscribeStopped(SafeLogger logger, Type effectType, int attempts);
}

using Microsoft.Extensions.Logging;

namespace Ducky;

// Core log events (SPEC §9): EventIds 1000-1999, types and keys only, never values. M1-05 uses 1000, M1-07 1001, M1-09
// 1004, M1-11 1002, 1010, 1011 and 1013, M4-01 1015 (widened to effects by M2-01), 1020 and 1021, M2-02 1003, M2-08 1030,
// M2-04 1014 and 1050, M2-05 1051, M4-03 1031 and 1032, M4-03c 1016, M2-03b 1040, M3-01b 1060, M4-06 1070 and 1071,
// M4-08 1080, M4-09 1017.
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Reducer of slice '{SliceKey}' threw while reducing {ActionType}")]
    internal static partial void ReducerThrew(SafeLogger logger, Exception exception, string sliceKey, Type actionType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "{FailureType} for {ActionType} raised while handling a failure action, logged and not dispatched")]
    internal static partial void FailureNotDispatched(SafeLogger logger, Exception exception, Type failureType, string actionType);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "{ActionType} ignored: the store is disposed")]
    internal static partial void DispatchAfterDispose(SafeLogger logger, Type actionType);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Error, Message = "Effect {EffectType} threw while handling {ActionType}")]
    internal static partial void EffectThrew(SafeLogger logger, Exception exception, Type effectType, Type actionType);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Warning, Message = "The store is a Singleton on a non-browser host: every user and circuit shares one store. Use ServiceLifetime.Scoped on the server")]
    internal static partial void SingletonOutsideBrowser(SafeLogger logger);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Warning, Message = "prefer DisposeAsync; pending persistence writes may be lost")]
    internal static partial void SyncDispose(SafeLogger logger);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Warning, Message = "The drain did not exit within DisposeTimeout ({DisposeTimeout}); DisposeAsync completes without it")]
    internal static partial void DrainExitTimedOut(SafeLogger logger, TimeSpan disposeTimeout);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Error, Message = "unexpected exception while processing {Type}")]
    internal static partial void ProcessEscaped(SafeLogger logger, Exception exception, Type type);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Error, Message = "A cancellation callback threw")]
    internal static partial void CancellationCallbackThrew(SafeLogger logger, AggregateException exception);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Warning, Message = "A cancellation callback of a superseded Switch run threw")]
    internal static partial void CancelCallbackThrew(SafeLogger logger, AggregateException exception);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Error, Message = "Disposing {Owned} threw")]
    internal static partial void DisposeThrew(SafeLogger logger, Exception exception, Type owned);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Warning, Message = "DisposeAsync of middleware {Middleware} did not complete within DisposeTimeout ({DisposeTimeout}); disposal goes on without it")]
    internal static partial void MiddlewareDisposeTimedOut(SafeLogger logger, Type middleware, TimeSpan disposeTimeout);

    [LoggerMessage(EventId = 1017, Level = LogLevel.Warning, Message = "A telemetry listener threw in {Call}; the step goes on")]
    internal static partial void TelemetryFailed(SafeLogger logger, Exception exception, string call);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Debug, Message = "{ActionType} vetoed by {Middleware}")]
    internal static partial void Vetoed(SafeLogger logger, Type actionType, Type middleware);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Error, Message = "{Middleware}.{Hook} threw while handling {ActionType}")]
    internal static partial void MiddlewareHookThrew(SafeLogger logger, Exception exception, Type middleware, string hook, Type actionType);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Debug, Message = "Constructor check of {Type} skipped: the container offers no {Service}")]
    internal static partial void CtorCheckSkipped(SafeLogger logger, Type type, Type service);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Error, Message = "Store init aborted ({Reason}): the store is ready without waiting for the remaining middleware init")]
    internal static partial void StoreInitAborted(SafeLogger logger, InitAbortReason reason);

    [LoggerMessage(EventId = 1032, Level = LogLevel.Error, Message = "InitializeAsync of middleware {Middleware} failed; init counts it as finished")]
    internal static partial void MiddlewareInitFailed(SafeLogger logger, Exception exception, Type middleware);

    [LoggerMessage(EventId = 1040, Level = LogLevel.Debug, Message = "No effect started for {ActionType}: the store is disposing")]
    internal static partial void EffectsNotStarted(SafeLogger logger, Type actionType);

    [LoggerMessage(EventId = 1050, Level = LogLevel.Debug, Message = "{ActionType} dropped: the effect run that dispatched it was superseded")]
    internal static partial void RunDropped(SafeLogger logger, Type actionType);

    [LoggerMessage(EventId = 1051, Level = LogLevel.Debug, Message = "{Effect} dropped {ActionType}: a run for the same key is in flight")]
    internal static partial void EffectDropped(SafeLogger logger, Type effect, Type actionType);

    [LoggerMessage(EventId = 1060, Level = LogLevel.Error, Message = "A subscriber threw while notified of {ActionType}; it stays subscribed")]
    internal static partial void SubscriberThrew(SafeLogger logger, Exception exception, Type actionType);

    [LoggerMessage(EventId = 1070, Level = LogLevel.Debug, Message = "{Type} could not be serialized (no JsonTypeInfo, or the serializer threw); logged once per type per store")]
    internal static partial void Unserializable(SafeLogger logger, Exception? exception, Type type);

    [LoggerMessage(EventId = 1071, Level = LogLevel.Warning, Message = "The value of '{Key}' could not be deserialized as {Type}")]
    internal static partial void Undeserializable(SafeLogger logger, Exception? exception, string? key, Type type);

    [LoggerMessage(EventId = 1080, Level = LogLevel.Debug, Message = "Action {Type} matched no reducer or async effect (reactive effects and middleware not inspected)")]
    internal static partial void UnhandledAction(SafeLogger logger, Type type);
}

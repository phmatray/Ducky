using Microsoft.Extensions.Logging;

namespace Ducky;

// Core log events (SPEC §9): EventIds 1000-1999, types and keys only, never values. Reserved by later stories:
// 1014, 1016, 1017. M1-05 uses 1000, M1-07 1001, M1-09 1004, M1-11 1002, 1010, 1011 and 1013, M4-01 1015, 1020 and 1021.
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Reducer of slice '{SliceKey}' threw while reducing {ActionType}")]
    internal static partial void ReducerThrew(SafeLogger logger, Exception exception, string sliceKey, Type actionType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "{FailureType} for {ActionType} raised while handling a failure action, logged and not dispatched")]
    internal static partial void FailureNotDispatched(SafeLogger logger, Exception exception, Type failureType, string actionType);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Debug, Message = "{ActionType} ignored: the store is disposed")]
    internal static partial void DispatchAfterDispose(SafeLogger logger, Type actionType);

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

    [LoggerMessage(EventId = 1015, Level = LogLevel.Error, Message = "DisposeAsync of middleware {Middleware} threw")]
    internal static partial void MiddlewareDisposeThrew(SafeLogger logger, Exception exception, Type middleware);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Debug, Message = "{ActionType} vetoed by {Middleware}")]
    internal static partial void Vetoed(SafeLogger logger, Type actionType, Type middleware);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Error, Message = "{Middleware}.{Hook} threw while handling {ActionType}")]
    internal static partial void MiddlewareHookThrew(SafeLogger logger, Exception exception, Type middleware, string hook, Type actionType);
}

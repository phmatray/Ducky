using Microsoft.Extensions.Logging;

namespace Ducky;

// Core log events (SPEC §9): EventIds 1000-1999, types and keys only, never values. Reserved by later stories: 1004,
// 1010-1017. M1-05 uses 1000, M1-07 1001.
internal static partial class Log
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Reducer of slice '{SliceKey}' threw while reducing {ActionType}")]
    internal static partial void ReducerThrew(SafeLogger logger, Exception exception, string sliceKey, Type actionType);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Error, Message = "{FailureType} for {ActionType} raised while handling a failure action, logged and not dispatched")]
    internal static partial void FailureNotDispatched(SafeLogger logger, Exception exception, Type failureType, string actionType);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Error, Message = "unexpected exception while processing {Type}")]
    internal static partial void ProcessEscaped(SafeLogger logger, Exception exception, Type type);
}

using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

// Ducky.Blazor log events (SPEC §9): EventIds 2000-2999, types and keys only, never values. Assigned by SPEC §6.3 and §11
// (the source of truth); M5-02 uses 2000, M6-02 2030-2033 (the envelope reader).
internal static partial class Log
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "JS interop call '{Identifier}' interrupted: circuit disconnected or interop timed out")]
    internal static partial void InteropInterrupted(SafeLogger logger, Exception exception, string identifier);

    [LoggerMessage(EventId = 2030, Level = LogLevel.Debug, Message = "The value of '{Key}' is not a 2.x envelope (1.x or foreign data); discarded")]
    internal static partial void NotAnEnvelope(SafeLogger logger, string key);

    [LoggerMessage(EventId = 2031, Level = LogLevel.Warning, Message = "The envelope of '{Key}' has version {Found}, newer than version {Version}; read as not found")]
    internal static partial void NewerEnvelope(SafeLogger logger, string key, int found, int version);

    [LoggerMessage(EventId = 2032, Level = LogLevel.Debug, Message = "The envelope of '{Key}' is older than its MaxAge; discarded")]
    internal static partial void ExpiredEnvelope(SafeLogger logger, string key);

    [LoggerMessage(EventId = 2033, Level = LogLevel.Warning, Message = "The envelope of '{Key}' could not be read as {Type} (malformed, a migration failed or the state is null); the state is kept")]
    internal static partial void UnreadableEnvelope(SafeLogger logger, Exception? exception, string key, Type type);
}

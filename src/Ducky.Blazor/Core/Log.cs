using Microsoft.Extensions.Logging;

namespace Ducky.Blazor;

// Ducky.Blazor log events (SPEC §9): EventIds 2000-2999, types and keys only, never values. Assigned by SPEC §6.3 and §11
// (the source of truth); M5-02 uses 2000.
internal static partial class Log
{
    [LoggerMessage(EventId = 2000, Level = LogLevel.Debug, Message = "JS interop call '{Identifier}' interrupted: circuit disconnected or interop timed out")]
    internal static partial void InteropInterrupted(SafeLogger logger, Exception exception, string identifier);
}

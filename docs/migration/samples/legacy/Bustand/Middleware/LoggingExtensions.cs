using Microsoft.Extensions.Logging;

namespace Bustand.Middleware;

/// <summary>
/// High-performance logging extensions using source generation.
/// </summary>
internal static partial class LoggingExtensions
{
    /// <summary>
    /// Logs a state change with diff information.
    /// </summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "[{StoreName}] {ActionName}: {Differences}")]
    internal static partial void LogStateChange(
        this ILogger logger,
        string storeName,
        string actionName,
        string differences);

    /// <summary>
    /// Logs when a state change is blocked by middleware.
    /// </summary>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "[{StoreName}] {ActionName}: State change blocked")]
    internal static partial void LogStateChangeBlocked(
        this ILogger logger,
        string storeName,
        string actionName);
}

using Microsoft.Extensions.Logging;

namespace R3dux.Blazor;

internal static partial class R3duxBlazorLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component initializing")]
    public static partial void ComponentInitializing(
        this ILogger logger, string componentName);
    
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "[{ComponentName}] component initialized")]
    public static partial void ComponentInitialized(
        this ILogger logger, string componentName);
    
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "[{ComponentName}] subscription already assigned")]
    public static partial void SubscriptionAlreadyAssigned(
        this ILogger logger, string componentName);
    
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "[{ComponentName}] disposing component")]
    public static partial void DisposingComponent(
        this ILogger logger, string componentName);
    
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] subscribing to StateObservable")]
    public static partial void SubscribingToStateObservable(
        this ILogger logger, string componentName);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "[{ComponentName}] component refreshed")]
    public static partial void ComponentRefreshed(
        this ILogger logger, string componentName);
}
using Microsoft.Extensions.Logging;

namespace R3dux.Blazor;

public static partial class R3duxComponentLogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component initializing")]
    public static partial void ComponentInitializing(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);
    
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component initialized")]
    public static partial void ComponentInitialized(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);
    
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] subscription already assigned")]
    public static partial void SubscriptionAlreadyAssigned(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);
    
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] disposing component")]
    public static partial void DisposingComponent(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);
    
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] subscribing to StateObservable")]
    public static partial void SubscribingToStateObservable(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);

    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Debug,
        Message = "[{ComponentName}] component refreshed")]
    public static partial void ComponentRefreshed(
        this ILogger<R3duxComponent<object>> logger,
        string componentName);
}
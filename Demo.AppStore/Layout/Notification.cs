namespace Demo.AppStore;

public abstract record Notification(
    string Message,
    NotificationSeverity Severity)
{
    public Guid Id { get; init; } = Guid.NewGuid();
 
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    public bool IsRead { get; init; }
}

public record InfoNotification(string Message)
    : Notification(Message, NotificationSeverity.Info);

public record SuccessNotification(string Message)
    : Notification(Message, NotificationSeverity.Success);

public record WarningNotification(string Message)
    : Notification(Message, NotificationSeverity.Warning);

public record ErrorNotification(string Message)
    : Notification(Message, NotificationSeverity.Error);

public record ExceptionNotification : Notification
{
    public ExceptionNotification(Exception ex)
        : base(ex.Message, NotificationSeverity.Error)
    {
        StackTrace = ex.StackTrace;
    }
    
    public string? StackTrace { get; init; }
}
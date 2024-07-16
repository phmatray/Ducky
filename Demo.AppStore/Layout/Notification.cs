namespace Demo.AppStore;

public record Notification
{
    public Notification(string message)
    {
        Message = message;
    }
    
    public Guid Id { get; init; } = Guid.NewGuid();
 
    public string Message { get; init; } = string.Empty;
    
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    
    public bool IsRead { get; init; } = false;
    
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
}

public record ExceptionNotification : Notification
{
    public ExceptionNotification(Exception ex)
        : base(ex.Message)
    {
        StackTrace = ex.StackTrace;
        Severity = NotificationSeverity.Error;
    }
    
    public string? StackTrace { get; init; }
}
namespace Demo.Website2.Extensions;

public static class NotificationSeverityExtensions
{
    public static Severity ToMudSeverity(
        this NotificationSeverity severity)
    {
        return (Severity)(int)severity;
    }

    public static Color ToMudColor(
        this NotificationSeverity severity)
    {
        return severity switch
        {
            NotificationSeverity.Info => Color.Info,
            NotificationSeverity.Success => Color.Success,
            NotificationSeverity.Warning => Color.Warning,
            NotificationSeverity.Error => Color.Error,
            _ => Color.Primary
        };
    }
}

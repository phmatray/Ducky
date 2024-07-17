using Demo.AppStore;
using MudBlazor;

namespace Demo.App.Extensions;

public static class NotificationSeverityExtensions
{
    public static Severity ToMudSeverity(
        this NotificationSeverity severity)
        => (Severity)(int)severity;

    public static Color ToMudColor(
        this NotificationSeverity severity)
        => severity switch
        {
            NotificationSeverity.Info => Color.Info,
            NotificationSeverity.Success => Color.Success,
            NotificationSeverity.Warning => Color.Warning,
            NotificationSeverity.Error => Color.Error,
            _ => Color.Primary
        };
}
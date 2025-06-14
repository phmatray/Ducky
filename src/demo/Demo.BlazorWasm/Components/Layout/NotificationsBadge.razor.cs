namespace Demo.BlazorWasm.Components.Layout;

public partial class NotificationsBadge
{
    private int UnreadNotificationCount
        => State.SelectUnreadNotificationCount();

    private bool NotificationBadgeVisible
        => UnreadNotificationCount > 0;

    private void ToggleNotifications()
        => Dispatcher.ToggleNotifications();
}

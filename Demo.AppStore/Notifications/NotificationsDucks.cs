namespace Demo.AppStore;

#region State

public record NotificationsState
{
    public required ImmutableList<Notification> Notifications { get; init; }
    
    // Selectors
    public ImmutableList<Notification> SelectUnreadNotifications()
        =>
        [
            ..Notifications
                .Where(n => !n.IsRead)
                .Reverse()
        ];
    
    public bool SelectHasUnreadNotifications()
        => !SelectUnreadNotifications().IsEmpty;

    public int SelectUnreadNotificationCount()
        => SelectUnreadNotifications().Count;

    public ImmutableList<Notification> SelectNotificationsBySeverity(
        NotificationSeverity severity)
        =>
        [
            ..SelectUnreadNotifications()
                .Where(n => n.Severity == severity)
        ];
    
    public ImmutableList<Notification> SelectErrorNotifications()
        => SelectNotificationsBySeverity(NotificationSeverity.Error);
}

#endregion

#region Actions

public record AddNotification(Notification Notification) : IAction;
public record MarkNotificationAsRead(Guid NotificationId) : IAction;

#endregion

#region Reducers

public record NotificationsReducers : SliceReducers<NotificationsState>
{
    public NotificationsReducers()
    {
        Map<AddNotification>(ReduceAddNotification);
        Map<MarkNotificationAsRead>(ReduceMarkNotificationAsRead);
    }

    private static NotificationsState ReduceAddNotification(
        NotificationsState state, AddNotification action)
        => new() { Notifications = state.Notifications.Add(action.Notification) };

    private static NotificationsState ReduceMarkNotificationAsRead(
        NotificationsState state, MarkNotificationAsRead action)
        => new()
        {
            Notifications = state.Notifications
                .Select(n => n.Id == action.NotificationId
                    ? n with { IsRead = true }
                    : n)
                .ToImmutableList()
        };

    public override NotificationsState GetInitialState()
    {
        return new NotificationsState
        {
            Notifications =
            [
                new SuccessNotification("Welcome to R3dux!"),
                new WarningNotification("This is a warning."),
                new ErrorNotification("This is an error.")
            ]
        };
    }
}

#endregion

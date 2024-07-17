namespace Demo.AppStore;

#region State

public record LayoutState
{
    public required string Title { get; init; }
    public required string Version { get; init; }
    public required bool IsDarkMode { get; init; }
    public required bool IsDrawerOpen { get; init; }
    public required bool IsNotificationOpen { get; init; }
    public required ImmutableList<Notification> Notifications { get; init; }

    // Selectors
    public string SelectFullTitle()
        => $"{Title} - {Version}";

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

public record SetTitle(string Title) : IAction;
public record ToggleDarkMode : IAction;
public record ToggleDrawer : IAction;
public record ToggleNotifications : IAction;
public record AddNotification(Notification Notification) : IAction;
public record MarkNotificationAsRead(Guid NotificationId) : IAction;

#endregion

#region Reducers

public record LayoutReducers : SliceReducers<LayoutState>
{
    public LayoutReducers()
    {
        Map<SetTitle>((state, action)
            => state with { Title = action.Title });
        
        Map<ToggleDarkMode>((state, _)
            => state with { IsDarkMode = !state.IsDarkMode });

        Map<ToggleDrawer>((state, _)
            => state with { IsDrawerOpen = !state.IsDrawerOpen });
        
        Map<ToggleNotifications>((state, _)
            => state with { IsNotificationOpen = !state.IsNotificationOpen });
        
        Map<AddNotification>((state, action)
            => state with { Notifications = state.Notifications.Add(action.Notification) });

        Map<MarkNotificationAsRead>((state, action)
            => state with
            {
                Notifications =
                [
                    ..state.Notifications.Select(n => 
                        n.Id == action.NotificationId ? n with { IsRead = true } : n)
                ]
            });
    }

    public override LayoutState GetInitialState()
    {
        return new LayoutState
        {
            Title = "R3dux",
            Version = "v1.0.0",
            IsDarkMode = true,
            IsDrawerOpen = true,
            IsNotificationOpen = false,
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

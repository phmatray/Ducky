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
        => Notifications
            .Where(n => !n.IsRead)
            .ToImmutableList();
    
    public bool SelectHasUnreadNotifications()
        => Notifications
            .Any(n => !n.IsRead);

    public int SelectUnreadNotificationCount()
        => Notifications
            .Count(n => !n.IsRead);
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
                Notifications = state.Notifications
                    .Select(n => n.Id == action.NotificationId ? n with { IsRead = true } : n)
                    .ToImmutableList()
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
                new Notification("Welcome to R3dux!")
                {
                    Severity = NotificationSeverity.Success
                },
                new Notification("This is a warning.")
                {
                    Severity = NotificationSeverity.Warning
                },
                new Notification("This is an error.")
                {
                    Severity = NotificationSeverity.Error
                },
            ]
        };
    }
}

#endregion

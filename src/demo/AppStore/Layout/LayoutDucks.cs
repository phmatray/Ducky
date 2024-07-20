namespace AppStore;

#region State

public record LayoutState
{
    public required string Title { get; init; }
    public required string Version { get; init; }
    public required bool IsDarkMode { get; init; }
    public required bool IsDrawerOpen { get; init; }
    public required bool IsNotificationOpen { get; init; }

    // Selectors
    public string SelectFullTitle()
        => $"{Title} - {Version}";
}

#endregion

#region Actions

public record SetTitle(string Title) : IAction;
public record ToggleDarkMode : IAction;
public record ToggleDrawer : IAction;
public record ToggleNotifications : IAction;

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
    }

    public override LayoutState GetInitialState()
    {
        return new LayoutState
        {
            Title = "R3dux",
            Version = "v1.0.0",
            IsDarkMode = true,
            IsDrawerOpen = true,
            IsNotificationOpen = false
        };
    }
}

#endregion

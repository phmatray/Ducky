// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace AppStore.Layout;

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
    {
        return $"{Title} - {Version}";
    }
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
        On<SetTitle>((state, action)
            => state with { Title = action.Title });

        On<ToggleDarkMode>((state, _)
            => state with { IsDarkMode = !state.IsDarkMode });

        On<ToggleDrawer>((state, _)
            => state with { IsDrawerOpen = !state.IsDrawerOpen });

        On<ToggleNotifications>((state, _)
            => state with { IsNotificationOpen = !state.IsNotificationOpen });
    }

    public override LayoutState GetInitialState()
    {
        return new LayoutState
        {
            Title = "R3dux",
            Version = R3duxVersioning.GetVersion().ToString(),
            IsDarkMode = true,
            IsDrawerOpen = true,
            IsNotificationOpen = false
        };
    }
}

#endregion

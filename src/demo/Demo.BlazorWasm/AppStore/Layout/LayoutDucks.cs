// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.BlazorWasm.AppStore;

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

public record SetTitle(string Title);

public record ToggleDarkMode;

public record ToggleDrawer;

public record ToggleNotifications;

#endregion

#region Reducers

public record LayoutReducers : SliceReducers<LayoutState>
{
    public LayoutReducers()
    {
        On<SetTitle>(Reduce);
        On<ToggleDarkMode>(Reduce);
        On<ToggleDrawer>(Reduce);
        On<ToggleNotifications>(Reduce);
    }

    public override LayoutState GetInitialState()
        => new()
        {
            Title = "Ducky",
            Version = DuckyVersioning.GetVersion().ToString(),
            IsDarkMode = true,
            IsDrawerOpen = true,
            IsNotificationOpen = false
        };

    private static LayoutState Reduce(LayoutState state, SetTitle action)
        => state with { Title = action.Title };

    private static LayoutState Reduce(LayoutState state, ToggleDarkMode _)
        => state with { IsDarkMode = !state.IsDarkMode };

    private static LayoutState Reduce(LayoutState state, ToggleDrawer _)
        => state with { IsDrawerOpen = !state.IsDrawerOpen };

    private static LayoutState Reduce(LayoutState state, ToggleNotifications _)
        => state with { IsNotificationOpen = !state.IsNotificationOpen };
}

#endregion

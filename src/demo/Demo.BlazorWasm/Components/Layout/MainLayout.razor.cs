// Copyright (c) 2020-2026 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the Apache-2.0 license.
// See the LICENSE file in the project root for full license information.

using Demo.BlazorWasm.Features.Theming;

namespace Demo.BlazorWasm.Components.Layout;

public partial class MainLayout
{
    private readonly AppTheme _theme = new();

    private string FullTitle
        => State.SelectFullTitle();

    private bool IsDrawerOpen
        => State.IsDrawerOpen;

    private bool IsNotificationOpen
        => State.IsNotificationOpen;

    private bool IsDarkMode
        => State.IsDarkMode;

    private string DarkLightModeButtonIcon
        => AppTheme.GetDarkLightModeButtonIcon(State.IsDarkMode);

    private void ToggleDrawer()
        => Dispatcher.ToggleDrawer();

    private void ToggleDarkMode()
        => Dispatcher.ToggleDarkMode();

    private void OpenAboutDialog()
        => Dispatcher.OpenAboutDialog();
}

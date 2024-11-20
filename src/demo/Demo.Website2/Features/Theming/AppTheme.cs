// Copyright (c) 2020-2024 Atypical Consulting SRL. All rights reserved.
// Atypical Consulting SRL licenses this file to you under the GPL-3.0-or-later license.
// See the LICENSE file in the project root for full license information.

namespace Demo.Website2.Features.Theming;

/// <summary>
/// Represents the application theme.
/// </summary>
public class AppTheme : MudTheme
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppTheme"/> class.
    /// </summary>
    public AppTheme()
    {
        PaletteLight = CreatePaletteLight();
        PaletteDark = CreatePaletteDark();
        LayoutProperties = new LayoutProperties();
    }

    /// <summary>
    /// Gets the icon for the dark/light mode button.
    /// </summary>
    /// <param name="isDarkMode">True if the current theme is dark mode, false otherwise.</param>
    /// <returns>The icon for the dark/light mode button.</returns>
    public static string GetDarkLightModeButtonIcon(bool isDarkMode)
    {
        return (isDarkMode)
            ? Icons.Material.Outlined.LightMode
            : Icons.Material.Outlined.DarkMode;
    }

    /// <summary>
    /// Creates a light theme palette.
    /// </summary>
    /// <returns>A light theme palette.</returns>
    public static PaletteLight CreatePaletteLight()
    {
        return new()
        {
            Black = "#110e2d",
            AppbarText = "#424242",
            AppbarBackground = "rgba(255,255,255,0.8)",
            DrawerBackground = "#ffffff",
            GrayLight = "#e8e8e8",
            GrayLighter = "#f9f9f9"
        };
    }

    /// <summary>
    /// Creates a dark theme palette.
    /// </summary>
    /// <returns>A dark theme palette.</returns>
    public static PaletteDark CreatePaletteDark()
    {
        return new()
        {
            // Adapted from Dracula
            // https://github.com/dracula/dracula-theme
            Primary = "#bd93f9", // Purple
            Secondary = "ffb86c", // Orange
            Info = "#8be9fd", // Cyan
            Success = "#50fa7b", // Green
            Warning = "#f1fa8c", // Yellow
            Error = "#ff5555", // Red
            GrayLight = "#2a2833",
            GrayLighter = "#44475a", // Current Line

            Surface = "#44475a", // Current Line
            Background = "#282a36", // Background
            BackgroundGray = "#151521",
            TextPrimary = "#f8f8f2", // Foreground
            TextSecondary = "#f8f8f2", // Foreground
            TextDisabled = "#ffffff33",
            AppbarText = "#f8f8f2", // Foreground
            AppbarBackground = "#6272a4", // Comment

            DrawerBackground = "#44475a", // Current Line
            DrawerIcon = "#f8f8f2", // Foreground
            DrawerText = "#f8f8f2", // Foreground

            ActionDefault = "#74718e",
            ActionDisabled = "#9999994d",
            ActionDisabledBackground = "#605f6d4d",
            LinesDefault = "#33323e",
            TableLines = "#33323e",
            Divider = "#6272a4", // Comment
            OverlayLight = "#44475a80" // Current Line @ 50%
        };
    }
}

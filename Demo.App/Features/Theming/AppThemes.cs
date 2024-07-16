using MudBlazor;

namespace Demo.App.Features.Theming;

public class AppTheme : MudTheme
{
    public AppTheme()
    {
        PaletteLight = CreatePaletteLight();
        PaletteDark = CreatePaletteDark();
        LayoutProperties = new LayoutProperties();
    }

    public static string GetDarkLightModeButtonIcon(bool isDarkMode)
        => isDarkMode
            ? Icons.Material.Outlined.LightMode
            : Icons.Material.Outlined.DarkMode;
    
    public static PaletteLight CreatePaletteLight() => new()
    {
        Black = "#110e2d",
        AppbarText = "#424242",
        AppbarBackground = "rgba(255,255,255,0.8)",
        DrawerBackground = "#ffffff",
        GrayLight = "#e8e8e8",
        GrayLighter = "#f9f9f9",
    };

    public static PaletteDark CreatePaletteDark() => new()
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
        Divider = "#292838",
        OverlayLight = "#44475a80", // Current Line @ 50%
    };
}
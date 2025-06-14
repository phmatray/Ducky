using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen = true;
    private bool _isDarkMode;

    private MudTheme _theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = Colors.Blue.Default,
            Secondary = Colors.Teal.Default,
            AppbarBackground = Colors.Blue.Default,
            AppbarText = Colors.Shades.White,
            DrawerBackground = Colors.Gray.Lighten5,
            DrawerText = Colors.Gray.Darken4,
            Surface = Colors.Shades.White,
            Background = Colors.Gray.Lighten5
        },
        PaletteDark = new PaletteDark
        {
            Primary = Colors.Blue.Lighten1,
            Secondary = Colors.Teal.Lighten1,
            AppbarBackground = Colors.Gray.Darken4,
            DrawerBackground = Colors.Gray.Darken4,
            Surface = Colors.Gray.Darken3,
            Background = Colors.Gray.Darken4
        },
        LayoutProperties = new LayoutProperties
        {
            DrawerWidthLeft = "260px",
            DefaultBorderRadius = "8px"
        }
    };

    protected override void OnInitialized()
    {
        // Load dark mode preference from local storage in a real app
        _isDarkMode = false;
    }

    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }

    private void ToggleDarkMode()
    {
        _isDarkMode = !_isDarkMode;
    }
}

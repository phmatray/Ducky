using Ducky.Generator.WebApp.Components.Dialogs;
using Ducky.Generator.WebApp.Models;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages;

public partial class AppStores
{
    private List<AppStore>? _appStores;

    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("App Stores", href: null, disabled: true, icon: Icons.Material.Filled.Store)
    ];

    protected override async Task OnInitializedAsync()
    {
        await LoadAppStoresAsync();
    }

    private async Task LoadAppStoresAsync()
    {
        _appStores = await AppStoreService.GetAllAppStoresAsync();
    }

    private async Task CreateNewAppStoreAsync()
    {
        DialogParameters<CreateAppStoreDialog> parameters = new();
        DialogOptions options = new()
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseButton = true
        };

        IDialogReference dialog = await DialogService.ShowAsync<CreateAppStoreDialog>("Create New App Store", parameters, options);
        DialogResult? result = await dialog.Result;

        if (!(result is { Canceled: false, Data: AppStore newAppStore }))
        {
            return;
        }

        await LoadAppStoresAsync();
        Snackbar.Add($"App Store '{newAppStore.Name}' created successfully!", Severity.Success);
    }

    private async Task EditAppStoreAsync(AppStore appStore)
    {
        DialogParameters<EnhancedEditAppStoreDialog> parameters = new() { { x => x.AppStoreId, appStore.Id } };

        DialogOptions options = new()
        {
            MaxWidth = MaxWidth.ExtraLarge,
            FullWidth = true,
            CloseButton = true,
            CloseOnEscapeKey = false
        };

        IDialogReference dialog =
            await DialogService.ShowAsync<EnhancedEditAppStoreDialog>(
                $"Design Store: {appStore.Name}",
                parameters,
                options);
        DialogResult? result = await dialog.Result;

        if (!(result is { Canceled: false }))
        {
            return;
        }

        await LoadAppStoresAsync();
        Snackbar.Add($"App Store '{appStore.Name}' updated successfully!", Severity.Success);
    }

    private async Task GenerateCodeAsync(AppStore appStore)
    {
        try
        {
            // Show loading
            Snackbar.Add($"Generating code for '{appStore.Name}'...", Severity.Info);

            List<GeneratedFile> generatedFiles = await AppStoreService.GenerateFilesAsync(appStore.Id);

            DialogParameters<GeneratedCodeDialog> parameters = new()
            {
                { x => x.GeneratedFiles, generatedFiles },
                { x => x.AppStoreName, appStore.Name }
            };

            DialogOptions options = new()
            {
                MaxWidth = MaxWidth.ExtraLarge,
                FullWidth = true,
                CloseButton = true
            };

            await DialogService.ShowAsync<GeneratedCodeDialog>($"Generated Code - {appStore.Name}", parameters,
                options);

            Snackbar.Add($"Successfully generated {generatedFiles.Count} files!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generating code: {ex.Message}", Severity.Error);
        }
    }

    private void DuplicateAppStore(AppStore appStore)
    {
        // TODO: Implement duplicate functionality
        Snackbar.Add("Duplicate feature coming soon!", Severity.Info);
    }

    private void ArchiveAppStore(AppStore appStore)
    {
        // TODO: Implement archive functionality
        Snackbar.Add("Archive feature coming soon!", Severity.Info);
    }

    private async Task DeleteAppStoreAsync(AppStore appStore)
    {
        DialogOptions options = new() { CloseOnEscapeKey = true };
        bool? result = await DialogService.ShowMessageBox(
            "Delete App Store",
            $"Are you sure you want to delete '{appStore.Name}'? This action cannot be undone.",
            yesText: "Delete", cancelText: "Cancel", options: options);

        if (result != true)
        {
            return;
        }

        try
        {
            await AppStoreService.DeleteAppStoreAsync(appStore.Id);
            await LoadAppStoresAsync();
            Snackbar.Add($"App Store '{appStore.Name}' deleted successfully.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error deleting app store: {ex.Message}", Severity.Error);
        }
    }

    private string GetRelativeTime(DateTime dateTime)
    {
        TimeSpan timeSpan = DateTime.UtcNow - dateTime.ToUniversalTime();

        if (timeSpan.TotalDays < 1)
        {
            if (timeSpan.TotalHours < 1)
            {
                return $"{(int)timeSpan.TotalMinutes} minutes ago";
            }
            else
            {
                return $"{(int)timeSpan.TotalHours} hours ago";
            }
        }
        else if (timeSpan.TotalDays < 7)
        {
            return $"{(int)timeSpan.TotalDays} days ago";
        }
        else if (timeSpan.TotalDays < 30)
        {
            return $"{(int)(timeSpan.TotalDays / 7)} weeks ago";
        }
        else if (timeSpan.TotalDays < 365)
        {
            return $"{(int)(timeSpan.TotalDays / 30)} months ago";
        }
        else
        {
            return dateTime.ToString("MMM dd, yyyy");
        }
    }
}

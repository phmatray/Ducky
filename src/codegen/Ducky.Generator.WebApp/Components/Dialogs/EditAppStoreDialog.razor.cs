using Ducky.Generator.WebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Dialogs;

public partial class EditAppStoreDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int AppStoreId { get; set; }

    private AppStore? _appStore;
    private bool _isValid = true;
    private bool _isSaving;

    protected override async Task OnInitializedAsync()
    {
        _appStore = await AppStoreService.GetAppStoreByIdAsync(AppStoreId);
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task SaveAsync()
    {
        if (!_isValid || _appStore is null)
        {
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            await AppStoreService.UpdateAppStoreAsync(_appStore);
            MudDialog.Close(DialogResult.Ok(_appStore));
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving app store: {ex.Message}", Severity.Error);
            _isSaving = false;
            StateHasChanged();
        }
    }

    private async Task AddStateSliceAsync()
    {
        if (_appStore == null)
        {
            return;
        }

        // Simple inline creation for now - could be expanded to a dialog
        string name = $"NewSlice{_appStore.StateSlices.Count + 1}";
        Dictionary<string, object> stateDefinition = new()
        {
            { "Loading", false },
            { "Data", new List<object>() },
            { "Error", string.Empty }
        };

        try
        {
            StateSlice newSlice = await AppStoreService.AddStateSliceAsync(
                _appStore.Id,
                name,
                "New state slice",
                stateDefinition);

            _appStore.StateSlices.Add(newSlice);
            StateHasChanged();
            Snackbar.Add("State slice added successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error adding state slice: {ex.Message}", Severity.Error);
        }
    }

    private async Task AddActionAsync(StateSlice slice)
    {
        try
        {
            string actionName = $"NewAction{slice.Actions.Count + 1}";
            ActionDefinition newAction = await AppStoreService.AddActionAsync(
                slice.Id,
                actionName,
                "New action",
                "object"); // Default payload type

            slice.Actions.Add(newAction);
            StateHasChanged();
            Snackbar.Add("Action added successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error adding action: {ex.Message}", Severity.Error);
        }
    }

    private async Task AddEffectAsync(StateSlice slice)
    {
        try
        {
            string effectName = $"NewEffect{slice.Effects.Count + 1}";
            List<string> triggerActions =
                slice.Actions.Count > 0 ? new List<string> { slice.Actions.First().Name } : new List<string>();

            EffectDefinition newEffect = await AppStoreService.AddEffectAsync(
                slice.Id,
                effectName,
                "New effect",
                "AsyncEffect", // Default to AsyncEffect
                triggerActions);

            slice.Effects.Add(newEffect);
            StateHasChanged();
            Snackbar.Add("Effect added successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error adding effect: {ex.Message}", Severity.Error);
        }
    }
}

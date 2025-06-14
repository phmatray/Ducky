using Ducky.Generator.WebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Dialogs;

public partial class EnhancedEditAppStoreDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public int AppStoreId { get; set; }

    private AppStore? _appStore;
    private bool _isValid = true;
    private bool _isSaving;

    private bool IsReadyToGenerate
        => _appStore?.StateSlices.Count > 0
           && _appStore.StateSlices.Any(s => s.Actions.Count > 0);

    public enum StoreTemplate
    {
        TodoApp,
        ECommerce,
        Dashboard
    }

    protected override async Task OnInitializedAsync()
    {
        _appStore = await AppStoreService.GetAppStoreByIdAsync(AppStoreId);
    }

    private int GetComplexityScore()
    {
        if (_appStore is null)
        {
            return 0;
        }

        int sliceCount = _appStore.StateSlices.Count;
        int actionCount = _appStore.StateSlices.Sum(s => s.Actions.Count);
        int effectCount = _appStore.StateSlices.Sum(s => s.Effects.Count);

        return (sliceCount * 5) + (actionCount * 2) + (effectCount * 3);
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

    private async Task GenerateCodeAsync()
    {
        if (_appStore is null)
        {
            return;
        }

        _isSaving = true;
        StateHasChanged();

        try
        {
            Snackbar.Add($"Generating code for '{_appStore.Name}'...", Severity.Info);

            List<GeneratedFile> generatedFiles = await AppStoreService.GenerateFilesAsync(_appStore.Id);

            Snackbar.Add($"Successfully generated {generatedFiles.Count} files!", Severity.Success);

            // TODO: Show generated code dialog
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generating code: {ex.Message}", Severity.Error);
        }
        finally
        {
            _isSaving = false;
            StateHasChanged();
        }
    }

    private void PreviewCode()
    {
        // TODO: Implement code preview
        Snackbar.Add("Code preview coming soon!", Severity.Info);
    }

    private void ShowTemplates()
    {
        // TODO: Implement templates dialog
        Snackbar.Add("Templates coming soon!", Severity.Info);
    }

    private async Task CreateFromTemplateAsync(StoreTemplate template)
    {
        if (_appStore is null)
        {
            return;
        }

        try
        {
            switch (template)
            {
                case StoreTemplate.TodoApp:
                    {
                        await CreateTodoTemplateAsync();
                        break;
                    }
                case StoreTemplate.ECommerce:
                    {
                        await CreateECommerceTemplateAsync();
                        break;
                    }
                case StoreTemplate.Dashboard:
                    {
                        await CreateDashboardTemplateAsync();
                        break;
                    }
            }

            StateHasChanged();
            Snackbar.Add($"Created {template} template successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error creating template: {ex.Message}", Severity.Error);
        }
    }

    private async Task CreateTodoTemplateAsync()
    {
        Dictionary<string, object> stateDefinition = new()
        {
            { "Items", "NormalizedState<int, TodoItem>" },
            { "Filter", "TodoFilter" },
            { "IsLoading", false },
            { "Error", "string?" }
        };

        StateSlice slice = await AppStoreService.AddStateSliceAsync(
            _appStore!.Id,
            "Todos",
            "Manages todo items and filtering",
            stateDefinition);

        // Add actions
        await AppStoreService.AddActionAsync(slice.Id, "AddTodo", "Add a new todo item", "{ Id: int, Title: string }");
        await AppStoreService.AddActionAsync(slice.Id, "ToggleTodo", "Toggle todo completion", "{ Id: int }");
        await AppStoreService.AddActionAsync(slice.Id, "DeleteTodo", "Delete a todo item", "{ Id: int }");
        await AppStoreService.AddActionAsync(slice.Id, "SetFilter", "Change the todo filter", "{ Filter: TodoFilter }");
        await AppStoreService.AddActionAsync(slice.Id, "LoadTodos", "Load todos from API", "{}", true);

        // Add effects
        await AppStoreService.AddEffectAsync(
            slice.Id,
            "TodoApiEffect",
            "Handles API operations",
            "AsyncEffect",
            new List<string> { "LoadTodos" });

        _appStore.StateSlices.Add(slice);
    }

    private async Task CreateECommerceTemplateAsync()
    {
        // Products slice
        Dictionary<string, object> productsState = new()
        {
            { "Items", "NormalizedState<int, Product>" },
            { "Categories", "List<Category>" },
            { "IsLoading", false },
            { "SearchQuery", "string" }
        };

        StateSlice productsSlice = await AppStoreService.AddStateSliceAsync(
            _appStore!.Id, "Products", "Product catalog management", productsState);

        // Cart slice
        Dictionary<string, object> cartState = new()
        {
            { "Items", "List<CartItem>" },
            { "Total", "decimal" },
            { "IsCheckingOut", false }
        };

        StateSlice cartSlice = await AppStoreService.AddStateSliceAsync(
            _appStore.Id, "Cart", "Shopping cart management", cartState);

        _appStore.StateSlices.AddRange(new[] { productsSlice, cartSlice });
    }

    private async Task CreateDashboardTemplateAsync()
    {
        // Analytics slice
        Dictionary<string, object> analyticsState = new()
        {
            { "Metrics", "DashboardMetrics" },
            { "Charts", "List<ChartData>" },
            { "DateRange", "DateRange" },
            { "IsLoading", false }
        };

        StateSlice analyticsSlice = await AppStoreService.AddStateSliceAsync(
            _appStore!.Id, "Analytics", "Dashboard analytics data", analyticsState);

        _appStore.StateSlices.Add(analyticsSlice);
    }

    private async Task AddStateSliceAsync()
    {
        // TODO: Open enhanced state slice dialog
        if (_appStore is null)
        {
            return;
        }

        string name = $"NewSlice{_appStore.StateSlices.Count + 1}";
        Dictionary<string, object> stateDefinition = new()
        {
            { "IsLoading", false },
            { "Data", "List<object>" },
            { "Error", "string?" }
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

    private void EditStateSlice(StateSlice slice)
    {
        // TODO: Open enhanced state slice editor
        Snackbar.Add("Enhanced state slice editor coming soon!", Severity.Info);
    }

    private void DuplicateStateSlice(StateSlice slice)
    {
        // TODO: Implement duplication
        Snackbar.Add("Duplicate functionality coming soon!", Severity.Info);
    }

    private async Task DeleteStateSliceAsync(StateSlice slice)
    {
        try
        {
            await AppStoreService.DeleteStateSliceAsync(slice.Id);
            _appStore?.StateSlices.Remove(slice);
            StateHasChanged();
            Snackbar.Add("State slice deleted successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error deleting state slice: {ex.Message}", Severity.Error);
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
                "object",
                false);

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
                "AsyncEffect",
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

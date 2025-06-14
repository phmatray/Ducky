using Ducky.Generator.WebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Dialogs;

public partial class CreateAppStoreDialog
{
    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    private MudForm _form = null!;
    private bool _isValid;
    private bool _isCreating;
    private string _appStoreName = string.Empty;
    private string _description = string.Empty;
    private string _namespaceName = string.Empty;
    private AppStoreTemplate _selectedTemplate = AppStoreTemplate.Custom;

    public enum AppStoreTemplate
    {
        Custom,
        TodoApp,
        ECommerce,
        Dashboard
    }

    protected override void OnInitialized()
    {
        // Set default namespace
        _namespaceName = "MyApp.Store";
    }

    private void SelectTemplate(AppStoreTemplate template)
    {
        _selectedTemplate = template;

        switch (template)
        {
            case AppStoreTemplate.TodoApp:
                {
                    if (string.IsNullOrWhiteSpace(_appStoreName))
                    {
                        _appStoreName = "Todo App Store";
                        _description = "Manages todo items with CRUD operations and filtering";
                        OnAppStoreNameChanged(_appStoreName);
                    }

                    break;
                }
            case AppStoreTemplate.ECommerce:
                {
                    if (string.IsNullOrWhiteSpace(_appStoreName))
                    {
                        _appStoreName = "E-Commerce Store";
                        _description = "Manages products, cart, and order processing";
                        OnAppStoreNameChanged(_appStoreName);
                    }

                    break;
                }
            case AppStoreTemplate.Dashboard:
                {
                    if (string.IsNullOrWhiteSpace(_appStoreName))
                    {
                        _appStoreName = "Dashboard Store";
                        _description = "Manages analytics data, metrics, and dashboard state";
                        OnAppStoreNameChanged(_appStoreName);
                    }

                    break;
                }
        }

        StateHasChanged();
    }

    private string GetTemplateName(AppStoreTemplate template)
    {
        return template switch
        {
            AppStoreTemplate.TodoApp => "Todo App",
            AppStoreTemplate.ECommerce => "E-Commerce",
            AppStoreTemplate.Dashboard => "Dashboard",
            _ => "Custom"
        };
    }

    private string GetTemplateDescription(AppStoreTemplate template)
    {
        return template switch
        {
            AppStoreTemplate.TodoApp => "Includes Todos state slice with CRUD actions and API effects",
            AppStoreTemplate.ECommerce => "Includes Products and Cart state slices with shopping functionality",
            AppStoreTemplate.Dashboard => "Includes Analytics state slice with metrics and real-time data",
            _ => "Start with an empty store and add your own state slices"
        };
    }

    private void OnAppStoreNameChanged(string value)
    {
        // Auto-generate namespace based on app store name
        if (string.IsNullOrWhiteSpace(value))
        {
            _namespaceName = "MyApp.Store";
        }
        else
        {
            // Convert to PascalCase and remove invalid characters
            string cleanName = System.Text.RegularExpressions.Regex.Replace(value, @"[^a-zA-Z0-9]", string.Empty);
            if (!string.IsNullOrWhiteSpace(cleanName))
            {
                _namespaceName = $"MyApp.{char.ToUpper(cleanName[0])}{cleanName[1..]}.Store";
            }
        }
    }

    private string? ValidateNamespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Namespace is required";
        }

        // Check for valid C# namespace pattern
        const string namespacePattern = @"^[a-zA-Z_][a-zA-Z0-9_]*(\.[a-zA-Z_][a-zA-Z0-9_]*)*$";
        if (System.Text.RegularExpressions.Regex.IsMatch(value, namespacePattern))
        {
            return null;
        }

        return "Invalid namespace format. Use dot notation (e.g., MyApp.Store)";
    }

    private void Cancel() => MudDialog.Cancel();

    private async Task SubmitAsync()
    {
        if (!_isValid)
        {
            return;
        }

        _isCreating = true;
        StateHasChanged();

        try
        {
            AppStore appStore = await AppStoreService.CreateAppStoreAsync(
                _appStoreName.Trim(),
                string.IsNullOrWhiteSpace(_description) ? null : _description.Trim(),
                _namespaceName.Trim());

            // Apply template if selected
            if (_selectedTemplate != AppStoreTemplate.Custom)
            {
                await ApplyTemplateToStoreAsync(appStore);
            }

            MudDialog.Close(DialogResult.Ok(appStore));
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error creating app store: {ex.Message}", Severity.Error);
            _isCreating = false;
            StateHasChanged();
        }
    }

    private async Task ApplyTemplateToStoreAsync(AppStore appStore)
    {
        switch (_selectedTemplate)
        {
            case AppStoreTemplate.TodoApp:
                {
                    await CreateTodoTemplateAsync(appStore);
                    break;
                }
            case AppStoreTemplate.ECommerce:
                {
                    await CreateECommerceTemplateAsync(appStore);
                    break;
                }
            case AppStoreTemplate.Dashboard:
                {
                    await CreateDashboardTemplateAsync(appStore);
                    break;
                }
        }
    }

    private async Task CreateTodoTemplateAsync(AppStore appStore)
    {
        Dictionary<string, object> stateDefinition = new()
        {
            { "Items", "NormalizedState<int, TodoItem>" },
            { "Filter", "TodoFilter" },
            { "IsLoading", false },
            { "Error", "string?" }
        };

        StateSlice slice = await AppStoreService.AddStateSliceAsync(
            appStore.Id,
            "Todos",
            "Manages todo items and filtering",
            stateDefinition);

        // Add common CRUD actions
        await AppStoreService.AddActionAsync(slice.Id, "AddTodo", "Add a new todo item", "{ Id: int, Title: string }");
        await AppStoreService.AddActionAsync(slice.Id, "ToggleTodo", "Toggle todo completion", "{ Id: int }");
        await AppStoreService.AddActionAsync(slice.Id, "DeleteTodo", "Delete a todo item", "{ Id: int }");
        await AppStoreService.AddActionAsync(slice.Id, "SetFilter", "Change the todo filter", "{ Filter: TodoFilter }");
        await AppStoreService.AddActionAsync(slice.Id, "LoadTodos", "Load todos from API", "{}", true);

        // Add API effect
        await AppStoreService.AddEffectAsync(slice.Id, "TodoApiEffect", "Handles API operations", "AsyncEffect",
            ["LoadTodos"]);
    }

    private async Task CreateECommerceTemplateAsync(AppStore appStore)
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
            appStore.Id, "Products", "Product catalog management", productsState);

        await AppStoreService.AddActionAsync(productsSlice.Id, "LoadProducts", "Load products from API", "{}", true);
        await AppStoreService.AddActionAsync(productsSlice.Id, "SearchProducts", "Search products",
            "{ Query: string }");
        await AppStoreService.AddActionAsync(productsSlice.Id, "FilterByCategory", "Filter by category",
            "{ CategoryId: int }");

        // Cart slice
        Dictionary<string, object> cartState = new()
        {
            { "Items", "List<CartItem>" },
            { "Total", "decimal" },
            { "IsCheckingOut", false }
        };

        StateSlice cartSlice = await AppStoreService.AddStateSliceAsync(
            appStore.Id, "Cart", "Shopping cart management", cartState);

        await AppStoreService.AddActionAsync(cartSlice.Id, "AddToCart", "Add item to cart",
            "{ ProductId: int, Quantity: int }");
        await AppStoreService.AddActionAsync(cartSlice.Id, "RemoveFromCart", "Remove item from cart",
            "{ ProductId: int }");
        await AppStoreService.AddActionAsync(cartSlice.Id, "UpdateQuantity", "Update item quantity",
            "{ ProductId: int, Quantity: int }");
        await AppStoreService.AddActionAsync(cartSlice.Id, "Checkout", "Start checkout process", "{}", true);
    }

    private async Task CreateDashboardTemplateAsync(AppStore appStore)
    {
        Dictionary<string, object> analyticsState = new()
        {
            { "Metrics", "DashboardMetrics" },
            { "Charts", "List<ChartData>" },
            { "DateRange", "DateRange" },
            { "IsLoading", false }
        };

        StateSlice analyticsSlice = await AppStoreService.AddStateSliceAsync(
            appStore.Id, "Analytics", "Dashboard analytics data", analyticsState);

        await AppStoreService.AddActionAsync(analyticsSlice.Id, "LoadMetrics", "Load dashboard metrics", "{}", true);
        await AppStoreService.AddActionAsync(analyticsSlice.Id, "SetDateRange", "Set date range",
            "{ DateRange: DateRange }");
        await AppStoreService.AddActionAsync(analyticsSlice.Id, "RefreshData", "Refresh all dashboard data", "{}",
            true);

        await AppStoreService.AddEffectAsync(analyticsSlice.Id, "AnalyticsEffect", "Handles analytics data loading",
            "AsyncEffect", ["LoadMetrics", "RefreshData"]);
    }
}

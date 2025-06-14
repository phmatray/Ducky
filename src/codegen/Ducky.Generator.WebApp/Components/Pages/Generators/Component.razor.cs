using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class Component
{
    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new BreadcrumbItem("Component", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private string? _generatedCode;
    private string? _livePreviewCode;
    private string? _previewError;
    private string _namespace = "MyApp.Components";
    private string _rootStateType = "IStateProvider";
    private string _componentName = "TodoStateComponent";
    private string _stateSliceName = "Todos";
    private string _stateSliceType = "TodoState";

    private List<MutableActionDescriptor> _actions = [
        new MutableActionDescriptor
        {
            ActionName = "AddTodo",
            ActionType = "AddTodoAction",
            Parameters = new List<MutableParameterDescriptor>
            {
                new MutableParameterDescriptor { ParamName = "id", ParamType = "int" },
                new MutableParameterDescriptor { ParamName = "title", ParamType = "string" }
            }
        },
        new MutableActionDescriptor
        {
            ActionName = "ToggleTodo",
            ActionType = "ToggleTodoAction",
            Parameters = new List<MutableParameterDescriptor> { new MutableParameterDescriptor { ParamName = "id", ParamType = "int" } }
        }
    ];

    public class MutableActionDescriptor
    {
        public string ActionName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public List<MutableParameterDescriptor> Parameters { get; set; } = [];

        public ComponentActionDescriptor ToComponentAction()
        {
            return new()
            {
                ActionName = ActionName,
                ActionType = ActionType,
                Parameters = Parameters
                    .Select(p => new ParameterDescriptor
                    {
                        ParamName = p.ParamName,
                        ParamType = p.ParamType
                    })
                    .ToList()
            };
        }
    }

    public class MutableParameterDescriptor
    {
        public string ParamName { get; set; } = string.Empty;
        public string ParamType { get; set; } = string.Empty;
    }

    protected override async Task OnInitializedAsync()
    {
        await UpdateLivePreviewAsync();
    }

    private async Task GenerateAsync()
    {
        try
        {
            ComponentGeneratorOptions options = CreateComponentOptions();
            ComponentGenerator generator = new();
            _generatedCode = await generator.GenerateCodeAsync(options);
            Snackbar.Add("Component generated successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generating code: {ex.Message}", Severity.Error);
        }
    }

    private async Task UpdateLivePreviewAsync()
    {
        try
        {
            _previewError = null;

            if (string.IsNullOrWhiteSpace(_namespace)
                || string.IsNullOrWhiteSpace(_rootStateType)
                || string.IsNullOrWhiteSpace(_componentName)
                || string.IsNullOrWhiteSpace(_stateSliceName)
                || string.IsNullOrWhiteSpace(_stateSliceType))
            {
                _livePreviewCode = null;
                StateHasChanged();
                return;
            }

            ComponentGeneratorOptions options = CreateComponentOptions();
            ComponentGenerator generator = new();
            _livePreviewCode = await generator.GenerateCodeAsync(options);
        }
        catch (Exception ex)
        {
            _previewError = $"Preview error: {ex.Message}";
            _livePreviewCode = null;
        }

        StateHasChanged();
    }

    private ComponentGeneratorOptions CreateComponentOptions()
    {
        return new()
        {
            Namespace = _namespace,
            RootStateType = _rootStateType,
            Components = new List<ComponentDescriptor>
            {
                new ComponentDescriptor
                {
                    ComponentName = _componentName,
                    StateSliceName = _stateSliceName,
                    StateSliceType = _stateSliceType,
                    StateSliceProperty = _stateSliceName,
                    Actions = _actions.Select(a => a.ToComponentAction()).ToList()
                }
            }
        };
    }

    private async Task AddActionAsync()
    {
        _actions.Add(new MutableActionDescriptor
        {
            ActionName = $"Action{_actions.Count + 1}",
            ActionType = $"Action{_actions.Count + 1}Action",
            Parameters = new List<MutableParameterDescriptor>()
        });
        await UpdateLivePreviewAsync();
    }

    private async Task RemoveActionAsync(int index)
    {
        if (index < 0 || index >= _actions.Count)
        {
            return;
        }

        _actions.RemoveAt(index);
        await UpdateLivePreviewAsync();
    }

    private async Task AddParameterAsync(int actionIndex)
    {
        if (actionIndex < 0 || actionIndex >= _actions.Count)
        {
            return;
        }

        _actions[actionIndex].Parameters.Add(new MutableParameterDescriptor
        {
            ParamName = "param",
            ParamType = "string"
        });
        await UpdateLivePreviewAsync();
    }

    private async Task RemoveParameterAsync(int actionIndex, int paramIndex)
    {
        if (actionIndex < 0 || actionIndex >= _actions.Count
            || paramIndex < 0 || paramIndex >= _actions[actionIndex].Parameters.Count)
        {
            return;
        }

        _actions[actionIndex].Parameters.RemoveAt(paramIndex);
        await UpdateLivePreviewAsync();
    }

    private async Task CopyToClipboardAsync()
    {
        string? codeToUse = !string.IsNullOrEmpty(_livePreviewCode) ? _livePreviewCode : _generatedCode;
        if (string.IsNullOrEmpty(codeToUse))
        {
            return;
        }

        await Js.InvokeVoidAsync("navigator.clipboard.writeText", codeToUse);
        Snackbar.Add("Code copied to clipboard!", Severity.Success);
    }

    private async Task DownloadCodeAsync()
    {
        string? codeToUse = !string.IsNullOrEmpty(_livePreviewCode) ? _livePreviewCode : _generatedCode;
        if (string.IsNullOrEmpty(codeToUse))
        {
            return;
        }

        string fileName = $"{_componentName}.cs";
        await Js.InvokeVoidAsync("downloadFile", fileName, codeToUse);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}

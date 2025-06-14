using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class ActionCreator
{
    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new BreadcrumbItem("Action Creator", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private readonly ActionCreatorGeneratorOptions _options = new();
    private string? _generatedCode;

    protected override void OnInitialized()
    {
        // Set some better defaults
        _options.Namespace = "MyApp.Actions";
        _options.StateType = "AppState";
        _options.Actions = new List<ActionDescriptor>
        {
            new ActionDescriptor
            {
                ActionName = "IncrementCounter",
                Parameters = new List<ParameterDescriptor> { new() { ParamType = "int", ParamName = "amount" } }
            }
        };
    }

    private void UpdateActionName(ActionDescriptor action, string newValue)
    {
        int index = _options.Actions.IndexOf(action);
        _options.Actions[index] = action with { ActionName = newValue };
    }

    private void UpdateParameterType(ActionDescriptor action, ParameterDescriptor parameter, string newValue)
    {
        List<ParameterDescriptor> parameters = action.Parameters.ToList();
        int paramIndex = parameters.IndexOf(parameter);
        parameters[paramIndex] = parameter with { ParamType = newValue };

        int index = _options.Actions.IndexOf(action);
        _options.Actions[index] = action with { Parameters = parameters };
    }

    private void UpdateParameterName(ActionDescriptor action, ParameterDescriptor parameter, string newValue)
    {
        List<ParameterDescriptor> parameters = action.Parameters.ToList();
        int paramIndex = parameters.IndexOf(parameter);
        parameters[paramIndex] = parameter with { ParamName = newValue };

        int index = _options.Actions.IndexOf(action);
        _options.Actions[index] = action with { Parameters = parameters };
    }

    private void AddAction()
    {
        _options.Actions.Add(new ActionDescriptor
        {
            ActionName = $"NewAction{_options.Actions.Count + 1}",
            Parameters = new List<ParameterDescriptor>()
        });
    }

    private void RemoveAction(ActionDescriptor action)
    {
        _options.Actions.Remove(action);
    }

    private void AddParameter(ActionDescriptor action)
    {
        List<ParameterDescriptor> parameters = action.Parameters.ToList();
        parameters.Add(new ParameterDescriptor
        {
            ParamType = "string",
            ParamName = $"param{parameters.Count + 1}"
        });

        // Update the action with new parameters list
        int index = _options.Actions.IndexOf(action);
        _options.Actions[index] = action with { Parameters = parameters };
    }

    private void RemoveParameter(ActionDescriptor action, ParameterDescriptor parameter)
    {
        List<ParameterDescriptor> parameters = action.Parameters.ToList();
        parameters.Remove(parameter);

        // Update the action with new parameters list
        int index = _options.Actions.IndexOf(action);
        _options.Actions[index] = action with { Parameters = parameters };
    }

    private async Task GenerateAsync()
    {
        try
        {
            _generatedCode = await Generator.GenerateCodeAsync(_options);
            Snackbar.Add("Action creators generated successfully!", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generating code: {ex.Message}", Severity.Error);
        }
    }

    private async Task CopyToClipboardAsync()
    {
        if (string.IsNullOrEmpty(_generatedCode))
        {
            return;
        }

        await Js.InvokeVoidAsync("navigator.clipboard.writeText", _generatedCode);
        Snackbar.Add("Code copied to clipboard!", Severity.Success);
    }

    private async Task DownloadCodeAsync()
    {
        if (string.IsNullOrEmpty(_generatedCode))
        {
            return;
        }

        string fileName = $"{_options.StateType}ActionCreators.cs";
        await Js.InvokeVoidAsync("downloadFile", fileName, _generatedCode);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}

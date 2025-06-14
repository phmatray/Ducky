using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class Reducer
{
    private readonly List<BreadcrumbItem> _breadcrumbs =
    [
        new("Home", href: "/", icon: Icons.Material.Filled.Home),
        new("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new("Reducer", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private readonly ReducerGeneratorOptions _options = new();
    private string? _generatedCode;
    private string _newActionName = string.Empty;
    private readonly Dictionary<ReducerDescriptor, string> _reducerClassNames = [];
    private readonly Dictionary<ReducerDescriptor, string> _stateTypes = [];

    protected override void OnInitialized()
    {
        // Set some better defaults
        _options.Namespace = "MyApp.Reducers";
        ReducerDescriptor initialReducer = new()
        {
            ReducerClassName = "CounterReducers",
            StateType = "CounterState",
            Actions = new List<string> { "IncrementAction", "DecrementAction", "ResetAction" }
        };

        _options.Reducers = new List<ReducerDescriptor> { initialReducer };

        // Initialize dictionaries
        _reducerClassNames[initialReducer] = initialReducer.ReducerClassName;
        _stateTypes[initialReducer] = initialReducer.StateType;
    }

    private string GetReducerClassNameWrapper(ReducerDescriptor reducer)
    {
        return _reducerClassNames.TryGetValue(reducer, out string? value) ? value : reducer.ReducerClassName;
    }

    private string GetStateTypeWrapper(ReducerDescriptor reducer)
    {
        return _stateTypes.TryGetValue(reducer, out string? value) ? value : reducer.StateType;
    }

    private void UpdateReducerClassName(ReducerDescriptor reducer, string newValue)
    {
        _reducerClassNames[reducer] = newValue;
        int index = _options.Reducers.IndexOf(reducer);
        _options.Reducers[index] = reducer with { ReducerClassName = newValue };
    }

    private void UpdateStateType(ReducerDescriptor reducer, string newValue)
    {
        _stateTypes[reducer] = newValue;
        int index = _options.Reducers.IndexOf(reducer);
        _options.Reducers[index] = reducer with { StateType = newValue };
    }

    private void AddReducer()
    {
        ReducerDescriptor newReducer = new()
        {
            ReducerClassName = $"NewReducers{_options.Reducers.Count + 1}",
            StateType = $"NewState{_options.Reducers.Count + 1}",
            Actions = new List<string>()
        };

        _options.Reducers.Add(newReducer);
        _reducerClassNames[newReducer] = newReducer.ReducerClassName;
        _stateTypes[newReducer] = newReducer.StateType;
    }

    private void RemoveReducer(ReducerDescriptor reducer)
    {
        _options.Reducers.Remove(reducer);
        _reducerClassNames.Remove(reducer);
        _stateTypes.Remove(reducer);
    }

    private void AddAction(ReducerDescriptor reducer)
    {
        if (string.IsNullOrWhiteSpace(_newActionName))
        {
            return;
        }

        List<string> actions = reducer.Actions.ToList();
        if (actions.Contains(_newActionName))
        {
            return;
        }

        actions.Add(_newActionName);

        // Update the reducer with new actions list
        int index = _options.Reducers.IndexOf(reducer);
        _options.Reducers[index] = reducer with { Actions = actions };

        _newActionName = string.Empty;
    }

    private void RemoveAction(ReducerDescriptor reducer, string action)
    {
        List<string> actions = reducer.Actions.ToList();
        actions.Remove(action);

        // Update the reducer with new actions list
        int index = _options.Reducers.IndexOf(reducer);
        _options.Reducers[index] = reducer with { Actions = actions };
    }

    private async Task GenerateAsync()
    {
        try
        {
            _generatedCode = await Generator.GenerateCodeAsync(_options);
            Snackbar.Add("Reducers generated successfully!", Severity.Success);
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

        var fileName = "Reducers.cs";
        if (_options.Reducers.Count == 1)
        {
            fileName = $"{_options.Reducers.First().ReducerClassName}.cs";
        }

        await Js.InvokeVoidAsync("downloadFile", fileName, _generatedCode);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}

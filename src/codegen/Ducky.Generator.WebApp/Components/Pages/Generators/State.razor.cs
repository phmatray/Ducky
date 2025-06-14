using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class State
{
    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new BreadcrumbItem("State", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private readonly StateGeneratorOptions _options = new();
    private string? _generatedCode;
    private readonly Dictionary<StateDescriptor, string> _stateNames = [];
    private readonly Dictionary<StateDescriptor, string> _baseClasses = [];
    private readonly Dictionary<StateDescriptor, bool> _implementsIState = [];

    protected override void OnInitialized()
    {
        // Set some better defaults
        _options.Namespace = "MyApp.States";
        StateDescriptor initialState = new()
        {
            StateName = "TodoState",
            Properties = new List<PropertyDescriptor>
            {
                new PropertyDescriptor
                {
                    PropertyName = "Items",
                    PropertyType = "List<TodoItem>",
                    DefaultValue = "new()"
                },
                new PropertyDescriptor
                {
                    PropertyName = "IsLoading",
                    PropertyType = "bool",
                    DefaultValue = "false"
                }
            }
        };

        _options.States = new List<StateDescriptor> { initialState };

        // Initialize dictionaries
        _stateNames[initialState] = initialState.StateName;
        _baseClasses[initialState] = initialState.BaseClass ?? string.Empty;
        _implementsIState[initialState] = initialState.ImplementsIState;
    }

    private string GetStateNameWrapper(StateDescriptor state)
    {
        return _stateNames.TryGetValue(state, out string? value) ? value : state.StateName;
    }

    private string GetBaseClassWrapper(StateDescriptor state)
    {
        return _baseClasses.TryGetValue(state, out string? value) ? value : (state.BaseClass ?? string.Empty);
    }

    private bool GetImplementsIStateWrapper(StateDescriptor state)
    {
        return _implementsIState.TryGetValue(state, out bool value) ? value : state.ImplementsIState;
    }

    private void UpdateStateName(StateDescriptor state, string newValue)
    {
        _stateNames[state] = newValue;
        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { StateName = newValue };
    }

    private void UpdateBaseClass(StateDescriptor state, string newValue)
    {
        _baseClasses[state] = newValue;
        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { BaseClass = string.IsNullOrWhiteSpace(newValue) ? null : newValue };
    }

    private void UpdateImplementsIState(StateDescriptor state, bool newValue)
    {
        _implementsIState[state] = newValue;
        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { ImplementsIState = newValue };
    }

    private void UpdatePropertyType(StateDescriptor state, PropertyDescriptor property, string newValue)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        int propIndex = properties.IndexOf(property);
        properties[propIndex] = property with { PropertyType = newValue };

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private void UpdatePropertyName(StateDescriptor state, PropertyDescriptor property, string newValue)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        int propIndex = properties.IndexOf(property);
        properties[propIndex] = property with { PropertyName = newValue };

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private void UpdatePropertyDefaultValue(StateDescriptor state, PropertyDescriptor property, string newValue)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        int propIndex = properties.IndexOf(property);
        properties[propIndex] = property with { DefaultValue = string.IsNullOrWhiteSpace(newValue) ? null : newValue };

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private void UpdatePropertySummary(StateDescriptor state, PropertyDescriptor property, string newValue)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        int propIndex = properties.IndexOf(property);
        properties[propIndex] = property with { Summary = string.IsNullOrWhiteSpace(newValue) ? null : newValue };

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private void AddState()
    {
        StateDescriptor newState = new()
        {
            StateName = $"NewState{_options.States.Count + 1}",
            Properties = new List<PropertyDescriptor>()
        };

        _options.States.Add(newState);
        _stateNames[newState] = newState.StateName;
        _baseClasses[newState] = string.Empty;
        _implementsIState[newState] = true;
    }

    private void RemoveState(StateDescriptor state)
    {
        _options.States.Remove(state);
        _stateNames.Remove(state);
        _baseClasses.Remove(state);
        _implementsIState.Remove(state);
    }

    private void AddProperty(StateDescriptor state)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        properties.Add(new PropertyDescriptor
        {
            PropertyName = $"Property{properties.Count + 1}",
            PropertyType = "string"
        });

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private void RemoveProperty(StateDescriptor state, PropertyDescriptor property)
    {
        List<PropertyDescriptor> properties = state.Properties.ToList();
        properties.Remove(property);

        int index = _options.States.IndexOf(state);
        _options.States[index] = state with { Properties = properties };
    }

    private async Task GenerateAsync()
    {
        try
        {
            _generatedCode = await Generator.GenerateCodeAsync(_options);
            Snackbar.Add("State classes generated successfully!", Severity.Success);
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

        var fileName = "States.cs";
        if (_options.States.Count == 1)
        {
            fileName = $"{_options.States.First().StateName}.cs";
        }

        await Js.InvokeVoidAsync("downloadFile", fileName, _generatedCode);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}

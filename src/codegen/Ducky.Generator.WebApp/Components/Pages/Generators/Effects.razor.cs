using Ducky.Generator.Core;
using Microsoft.JSInterop;
using MudBlazor;

namespace Ducky.Generator.WebApp.Components.Pages.Generators;

public partial class Effects
{
    private readonly List<BreadcrumbItem> _breadcrumbs = [
        new BreadcrumbItem("Home", href: "/", icon: Icons.Material.Filled.Home),
        new BreadcrumbItem("Generators", href: "#", icon: Icons.Material.Filled.Build),
        new BreadcrumbItem("Effects", href: null, disabled: true)
    ];

    private MudForm _form = null!;
    private bool _isValid;
    private readonly EffectsGeneratorOptions _options = new();
    private string? _generatedCode;
    private string _newTriggerAction = string.Empty;
    private string _newResultAction = string.Empty;
    private string _newDependency = string.Empty;
    private readonly Dictionary<EffectDescriptor, string> _effectNames = [];
    private readonly Dictionary<EffectDescriptor, EffectType> _effectTypes = [];
    private readonly Dictionary<EffectDescriptor, string> _summaries = [];
    private readonly Dictionary<EffectDescriptor, bool> _handleErrors = [];
    private readonly Dictionary<EffectDescriptor, int> _timeouts = [];

    protected override void OnInitialized()
    {
        // Set some better defaults
        _options.Namespace = "MyApp.Effects";
        EffectDescriptor initialEffect = new()
        {
            EffectName = "LoadDataEffect",
            EffectType = EffectType.Reactive,
            TriggerActions = new List<string> { "LoadDataAction" },
            ResultActions = new List<string> { "LoadDataSuccessAction", "LoadDataFailureAction" },
            Dependencies = new List<string> { "IDataService" },
            Summary = "Loads data from the API"
        };

        _options.Effects = new List<EffectDescriptor> { initialEffect };

        // Initialize dictionaries
        _effectNames[initialEffect] = initialEffect.EffectName;
        _effectTypes[initialEffect] = initialEffect.EffectType;
        _summaries[initialEffect] = initialEffect.Summary ?? string.Empty;
        _handleErrors[initialEffect] = initialEffect.HandleErrors;
        _timeouts[initialEffect] = initialEffect.TimeoutMs;
    }

    private string GetEffectNameWrapper(EffectDescriptor effect)
    {
        return _effectNames.TryGetValue(effect, out string? value) ? value : effect.EffectName;
    }

    private EffectType GetEffectTypeWrapper(EffectDescriptor effect)
    {
        return _effectTypes.TryGetValue(effect, out EffectType value) ? value : effect.EffectType;
    }

    private string GetSummaryWrapper(EffectDescriptor effect)
    {
        return _summaries.TryGetValue(effect, out string? value) ? value : (effect.Summary ?? string.Empty);
    }

    private bool GetHandleErrorsWrapper(EffectDescriptor effect)
    {
        return _handleErrors.TryGetValue(effect, out bool value) ? value : effect.HandleErrors;
    }

    private int GetTimeoutWrapper(EffectDescriptor effect)
    {
        return _timeouts.TryGetValue(effect, out int value) ? value : effect.TimeoutMs;
    }

    private void UpdateEffectName(EffectDescriptor effect, string newValue)
    {
        _effectNames[effect] = newValue;
        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { EffectName = newValue };
    }

    private void UpdateEffectType(EffectDescriptor effect, EffectType newValue)
    {
        _effectTypes[effect] = newValue;
        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { EffectType = newValue };
    }

    private void UpdateSummary(EffectDescriptor effect, string newValue)
    {
        _summaries[effect] = newValue;
        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { Summary = string.IsNullOrWhiteSpace(newValue) ? null : newValue };
    }

    private void UpdateHandleErrors(EffectDescriptor effect, bool newValue)
    {
        _handleErrors[effect] = newValue;
        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { HandleErrors = newValue };
    }

    private void UpdateTimeout(EffectDescriptor effect, int newValue)
    {
        _timeouts[effect] = newValue;
        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { TimeoutMs = newValue };
    }

    private void UpdateTriggerAction(EffectDescriptor effect, string oldAction, string newAction)
    {
        List<string> actions = effect.TriggerActions.ToList();
        int actionIndex = actions.IndexOf(oldAction);
        actions[actionIndex] = newAction;

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { TriggerActions = actions };
    }

    private void UpdateResultAction(EffectDescriptor effect, string oldAction, string newAction)
    {
        List<string> actions = effect.ResultActions.ToList();
        int actionIndex = actions.IndexOf(oldAction);
        actions[actionIndex] = newAction;

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { ResultActions = actions };
    }

    private void UpdateDependency(EffectDescriptor effect, string oldDep, string newDep)
    {
        List<string> deps = effect.Dependencies.ToList();
        int depIndex = deps.IndexOf(oldDep);
        deps[depIndex] = newDep;

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { Dependencies = deps };
    }

    private void AddEffect()
    {
        EffectDescriptor newEffect = new()
        {
            EffectName = $"NewEffect{_options.Effects.Count + 1}",
            EffectType = EffectType.Reactive,
            TriggerActions = new List<string>(),
            ResultActions = new List<string>(),
            Dependencies = new List<string>()
        };

        _options.Effects.Add(newEffect);
        _effectNames[newEffect] = newEffect.EffectName;
        _effectTypes[newEffect] = newEffect.EffectType;
        _summaries[newEffect] = string.Empty;
        _handleErrors[newEffect] = true;
        _timeouts[newEffect] = 30000;
    }

    private void RemoveEffect(EffectDescriptor effect)
    {
        _options.Effects.Remove(effect);
        _effectNames.Remove(effect);
        _effectTypes.Remove(effect);
        _summaries.Remove(effect);
        _handleErrors.Remove(effect);
        _timeouts.Remove(effect);
    }

    private void AddTriggerAction(EffectDescriptor effect)
    {
        if (string.IsNullOrWhiteSpace(_newTriggerAction))
        {
            return;
        }

        List<string> actions = effect.TriggerActions.ToList();
        actions.Add(_newTriggerAction);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { TriggerActions = actions };

        _newTriggerAction = string.Empty;
    }

    private void RemoveTriggerAction(EffectDescriptor effect, string action)
    {
        List<string> actions = effect.TriggerActions.ToList();
        actions.Remove(action);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { TriggerActions = actions };
    }

    private void AddResultAction(EffectDescriptor effect)
    {
        if (string.IsNullOrWhiteSpace(_newResultAction))
        {
            return;
        }

        List<string> actions = effect.ResultActions.ToList();
        actions.Add(_newResultAction);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { ResultActions = actions };

        _newResultAction = string.Empty;
    }

    private void RemoveResultAction(EffectDescriptor effect, string action)
    {
        List<string> actions = effect.ResultActions.ToList();
        actions.Remove(action);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { ResultActions = actions };
    }

    private void AddDependency(EffectDescriptor effect)
    {
        if (string.IsNullOrWhiteSpace(_newDependency))
        {
            return;
        }

        List<string> deps = effect.Dependencies.ToList();
        deps.Add(_newDependency);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { Dependencies = deps };

        _newDependency = string.Empty;
    }

    private void RemoveDependency(EffectDescriptor effect, string dependency)
    {
        List<string> deps = effect.Dependencies.ToList();
        deps.Remove(dependency);

        int index = _options.Effects.IndexOf(effect);
        _options.Effects[index] = effect with { Dependencies = deps };
    }

    private async Task GenerateAsync()
    {
        try
        {
            _generatedCode = await Generator.GenerateCodeAsync(_options);
            Snackbar.Add("Effects generated successfully!", Severity.Success);
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

        var fileName = "Effects.cs";
        if (_options.Effects.Count == 1)
        {
            fileName = $"{_options.Effects.First().EffectName}.cs";
        }

        await Js.InvokeVoidAsync("downloadFile", fileName, _generatedCode);
        Snackbar.Add($"Downloaded {fileName}", Severity.Success);
    }
}

namespace Ducky.Generator.Core;

/// <summary>
/// Options for configuring the EffectsGenerator.
/// </summary>
public class EffectsGeneratorOptions
{
    /// <summary>
    /// Gets or sets the namespace for generated effect classes.
    /// </summary>
    public string Namespace { get; set; } = "Ducky.Effects";
    
    /// <summary>
    /// Gets or sets the list of effect descriptors to generate.
    /// </summary>
    public List<EffectDescriptor> Effects { get; set; } = new()
    {
        new EffectDescriptor
        {
            EffectName = "LoadTodosEffect",
            EffectType = EffectType.Reactive,
            TriggerActions = new List<string> { "LoadTodosAction" },
            ResultActions = new List<string> { "LoadTodosSuccessAction", "LoadTodosFailureAction" },
            Dependencies = new List<string> { "ITodoService" },
            Summary = "Loads todos from the API when LoadTodosAction is dispatched"
        },
        new EffectDescriptor
        {
            EffectName = "SaveTodoEffect",
            EffectType = EffectType.Async,
            TriggerActions = new List<string> { "SaveTodoAction" },
            ResultActions = new List<string> { "SaveTodoSuccessAction", "SaveTodoFailureAction" },
            Dependencies = new List<string> { "ITodoService", "ILogger<SaveTodoEffect>" },
            Summary = "Saves a todo to the API"
        }
    };
}

/// <summary>
/// Describes an effect to be generated.
/// </summary>
public record EffectDescriptor
{
    /// <summary>
    /// Gets the name of the effect class.
    /// </summary>
    public required string EffectName { get; init; }
    /// <summary>
    /// Gets the type of effect (Reactive or Async).
    /// </summary>
    public EffectType EffectType { get; init; } = EffectType.Reactive;
    /// <summary>
    /// Gets the list of action types that trigger this effect.
    /// </summary>
    public IEnumerable<string> TriggerActions { get; init; } = new List<string>();
    /// <summary>
    /// Gets the list of action types that this effect can dispatch.
    /// </summary>
    public IEnumerable<string> ResultActions { get; init; } = new List<string>();
    /// <summary>
    /// Gets the list of dependencies to inject into the effect.
    /// </summary>
    public IEnumerable<string> Dependencies { get; init; } = new List<string>();
    /// <summary>
    /// Gets an optional summary description for the effect.
    /// </summary>
    public string? Summary { get; init; }
    /// <summary>
    /// Gets a value indicating whether the effect should handle errors automatically.
    /// </summary>
    public bool HandleErrors { get; init; } = true;
    /// <summary>
    /// Gets the timeout in milliseconds for async operations.
    /// </summary>
    public int TimeoutMs { get; init; } = 30000;
    
    /// <summary>
    /// Returns a string representation of the effect descriptor.
    /// </summary>
    /// <returns>A string containing the effect name and trigger count.</returns>
    public override string ToString()
    {
        var triggerCount = TriggerActions.Count();
        return $"{EffectName} ({triggerCount.ToString()} triggers)";
    }
}

/// <summary>
/// Specifies the type of effect.
/// </summary>
public enum EffectType
{
    /// <summary>
    /// Reactive effect that uses observables for handling side effects.
    /// </summary>
    Reactive,
    
    /// <summary>
    /// Async effect that uses Task-based async operations.
    /// </summary>
    Async
}
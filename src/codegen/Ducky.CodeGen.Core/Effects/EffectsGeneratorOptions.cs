namespace Ducky.CodeGen.Core;

public class EffectsGeneratorOptions
{
    public string Namespace { get; set; } = "Ducky.Effects";
    
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

public record EffectDescriptor
{
    public required string EffectName { get; init; }
    public EffectType EffectType { get; init; } = EffectType.Reactive;
    public IEnumerable<string> TriggerActions { get; init; } = new List<string>();
    public IEnumerable<string> ResultActions { get; init; } = new List<string>();
    public IEnumerable<string> Dependencies { get; init; } = new List<string>();
    public string? Summary { get; init; }
    public bool HandleErrors { get; init; } = true;
    public int TimeoutMs { get; init; } = 30000;
    
    public override string ToString()
    {
        var triggerCount = TriggerActions.Count();
        return $"{EffectName} ({triggerCount} triggers)";
    }
}

public enum EffectType
{
    Reactive,
    Async
}
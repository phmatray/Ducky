namespace Ducky.CodeGen.Core;

/// <summary>
/// Generates effect classes for handling side effects in Ducky applications.
/// </summary>
public class EffectsGenerator : SourceGeneratorBase<EffectsGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(EffectsGeneratorOptions opts)
    {
        return new CompilationUnitElement
        {
            Usings = 
            [
                "System",
                "System.Reactive.Linq",
                "System.Threading.Tasks",
                "Ducky",
                "Ducky.Abstractions",
                "Ducky.Middlewares.ReactiveEffect",
                "Ducky.Middlewares.AsyncEffect",
                "Microsoft.Extensions.Logging",
                "R3"
            ],
            Namespaces = 
            [
                new NamespaceElement
                {
                    Name = opts.Namespace,
                    Classes = opts.Effects.Select(BuildEffectClass).ToList()
                }
            ]
        };
    }

    private ClassElement BuildEffectClass(EffectDescriptor effect)
    {
        var methods = new List<MethodElement>();
        
        if (effect.EffectType == EffectType.Reactive)
        {
            methods.Add(BuildReactiveHandleMethod(effect));
        }
        else
        {
            methods.Add(BuildAsyncHandleMethod(effect));
        }
        
        return new ClassElement
        {
            Name = GenerateEffectClassCode(effect),
            IsStatic = false,
            Methods = methods
        };
    }
    
    private string GenerateEffectClassCode(EffectDescriptor effect)
    {
        List<string> lines = new();
        
        // Generate XML documentation
        lines.Add("/// <summary>");
        lines.Add($"/// {effect.Summary ?? $"Handles {string.Join(", ", effect.TriggerActions)} actions."}");
        lines.Add("/// </summary>");
        
        // Generate class declaration with base class
        string baseClass = effect.EffectType == EffectType.Reactive ? "ReactiveEffect" : "AsyncEffect";
        lines.Add($"public class {effect.EffectName} : {baseClass}");
        lines.Add("{");
        
        // Generate constructor with dependencies
        if (effect.Dependencies.Any())
        {
            lines.Add("    private readonly " + string.Join(";\n    private readonly ", effect.Dependencies.Select(dep => $"{dep} _{ToCamelCase(ExtractTypeName(dep))}")));
            lines.Add(";");
            lines.Add("");
            
            lines.Add($"    public {effect.EffectName}(");
            lines.Add("        " + string.Join(",\n        ", effect.Dependencies.Select(dep => $"{dep} {ToCamelCase(ExtractTypeName(dep))}")));
            lines.Add("    )");
            lines.Add("    {");
            foreach (var dep in effect.Dependencies)
            {
                var fieldName = ToCamelCase(ExtractTypeName(dep));
                lines.Add($"        _{fieldName} = {fieldName};");
            }
            lines.Add("    }");
            lines.Add("");
        }
        
        lines.Add("}");
        
        return string.Join("\n", lines);
    }
    
    private MethodElement BuildReactiveHandleMethod(EffectDescriptor effect)
    {
        return new MethodElement
        {
            Name = "Handle",
            ReturnType = "Observable<object>",
            Parameters = new List<ParameterDescriptor>
            {
                new() { ParamName = "actions", ParamType = "Observable<object>" },
                new() { ParamName = "rootState", ParamType = "Observable<IRootState>" }
            },
            ExpressionBody = new ExpressionElement
            {
                Code = GenerateReactiveEffectCode(effect)
            }
        };
    }
    
    private MethodElement BuildAsyncHandleMethod(EffectDescriptor effect)
    {
        return new MethodElement
        {
            Name = "HandleAsync",
            ReturnType = "Task<object?>",
            Parameters = new List<ParameterDescriptor>
            {
                new() { ParamName = "action", ParamType = "object" },
                new() { ParamName = "rootState", ParamType = "IRootState" }
            },
            ExpressionBody = new ExpressionElement
            {
                Code = GenerateAsyncEffectCode(effect)
            }
        };
    }
    
    private string GenerateReactiveEffectCode(EffectDescriptor effect)
    {
        string? successAction = effect.ResultActions.FirstOrDefault(a => a.Contains("Success"));
        string? failureAction = effect.ResultActions.FirstOrDefault(a => a.Contains("Failure") || a.Contains("Error"));
        
        string code = $@"actions
            .OfActionType<{effect.TriggerActions.First()}>()
            .SwitchSelect(action => 
                Observable.FromAsync(async () =>
                {{
                    try
                    {{
                        // TODO: Implement your effect logic here
                        // Example: var result = await _service.ProcessAsync(action);
                        return new {successAction ?? "SuccessAction"}();
                    }}
                    catch (Exception ex)
                    {{";
        
        if (effect.HandleErrors && !string.IsNullOrEmpty(failureAction))
        {
            code += $@"
                        return new {failureAction}(ex.Message);";
        }
        else
        {
            code += @"
                        throw;";
        }
        
        code += @"
                    }
                })
                .Timeout(TimeSpan.FromMilliseconds(" + effect.TimeoutMs + @"))
            )";
            
        return code;
    }
    
    private string GenerateAsyncEffectCode(EffectDescriptor effect)
    {
        string triggerAction = effect.TriggerActions.First();
        
        string code = $@"action switch
        {{
            {triggerAction} typedAction => await ProcessAsync(typedAction),
            _ => null
        }}";
        
        return code;
    }
    
    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length == 1)
        {
            return input.ToLowerInvariant();
        }

        return char.ToLowerInvariant(input[0]) + input[1..];
    }
    
    private string ExtractTypeName(string dependency)
    {
        // Extract type name from interface (e.g., "ITodoService" -> "TodoService")
        if (dependency.StartsWith("I") && dependency.Length > 1 && char.IsUpper(dependency[1]))
        {
            return dependency[1..];
        }
        
        // Handle generic types (e.g., "ILogger<SaveTodoEffect>" -> "Logger")
        if (dependency.Contains('<'))
        {
            string baseName = dependency.Split('<')[0];
            return baseName.StartsWith("I") && baseName.Length > 1 && char.IsUpper(baseName[1])
                ? baseName[1..]
                : baseName;
        }
        
        return dependency;
    }
}
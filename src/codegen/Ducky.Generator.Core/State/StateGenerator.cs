namespace Ducky.CodeGen.Core;

/// <summary>
/// Generates immutable state record classes for Ducky state management.
/// </summary>
public class StateGenerator : SourceGeneratorBase<StateGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(StateGeneratorOptions opts)
    {
        return new CompilationUnitElement
        {
            Usings = 
            [
                "System",
                "System.Collections.Generic",
                "Ducky",
                "Ducky.Abstractions",
                "Ducky.Normalization"
            ],
            Namespaces = 
            [
                new NamespaceElement
                {
                    Name = opts.Namespace,
                    Classes = opts.States.Select(BuildStateClass).ToList()
                }
            ]
        };
    }

    private ClassElement BuildStateClass(StateDescriptor state)
    {
        var methods = new List<MethodElement>();
        
        // Add constructor method for record with default values
        if (state.Properties.Any(p => !string.IsNullOrEmpty(p.DefaultValue)))
        {
            methods.Add(BuildConstructorMethod(state));
        }
        
        // Add helper methods
        methods.AddRange(BuildHelperMethods(state));
        
        return new ClassElement
        {
            Name = GenerateStateClassCode(state),
            IsStatic = false,
            Methods = methods
        };
    }
    
    private string GenerateStateClassCode(StateDescriptor state)
    {
        var lines = new List<string>();
        
        // Generate XML documentation
        lines.Add("/// <summary>");
        lines.Add($"/// Represents the state for {state.StateName.Replace("State", "")}.");
        lines.Add("/// </summary>");
        
        // Generate record declaration
        List<string> inheritance = new();
        if (state.ImplementsIState)
        {
            inheritance.Add("IState");
        }

        if (!string.IsNullOrEmpty(state.BaseClass))
        {
            inheritance.Add(state.BaseClass!);
        }
            
        string inheritanceClause = inheritance.Count > 0 ? $" : {string.Join(", ", inheritance)}" : "";
        
        lines.Add($"public record {state.StateName}{inheritanceClause}");
        lines.Add("{");
        
        // Generate properties
        foreach (var prop in state.Properties)
        {
            if (!string.IsNullOrEmpty(prop.Summary))
            {
                lines.Add("    /// <summary>");
                lines.Add($"    /// {prop.Summary}");
                lines.Add("    /// </summary>");
            }
            
            string defaultValue = !string.IsNullOrEmpty(prop.DefaultValue) ? $" = {prop.DefaultValue};" : "";
            lines.Add($"    public {prop.PropertyType} {prop.PropertyName} {{ get; init; }}{defaultValue}");
            lines.Add("");
        }
        
        lines.Add("}");
        
        return string.Join("\n", lines);
    }
    
    private MethodElement BuildConstructorMethod(StateDescriptor state)
    {
        return new MethodElement
        {
            Name = "CreateDefault",
            ReturnType = state.StateName,
            Parameters = new List<ParameterDescriptor>(),
            ExpressionBody = new ExpressionElement
            {
                Code = $"new {state.StateName}()"
            }
        };
    }
    
    private IEnumerable<MethodElement> BuildHelperMethods(StateDescriptor state)
    {
        // Add WithDefaults method
        yield return new MethodElement
        {
            Name = "WithDefaults",
            ReturnType = state.StateName,
            IsExtensionMethod = true,
            Parameters = new List<ParameterDescriptor>
            {
                new() { ParamName = "state", ParamType = state.StateName }
            },
            ExpressionBody = new ExpressionElement
            {
                Code = GenerateWithDefaultsCode(state)
            }
        };
        
        // Add Reset method for properties that have defaults
        List<PropertyDescriptor> resettableProps = state.Properties.Where(p => !string.IsNullOrEmpty(p.DefaultValue)).ToList();
        if (resettableProps.Any())
        {
            yield return new MethodElement
            {
                Name = "Reset",
                ReturnType = state.StateName,
                IsExtensionMethod = true,
                Parameters = new List<ParameterDescriptor>
                {
                    new() { ParamName = "state", ParamType = state.StateName }
                },
                ExpressionBody = new ExpressionElement
                {
                    Code = $"new {state.StateName}()"
                }
            };
        }
    }
    
    private string GenerateWithDefaultsCode(StateDescriptor state)
    {
        List<string> assignments = state.Properties
            .Where(p => !string.IsNullOrEmpty(p.DefaultValue))
            .Select(p => $"{p.PropertyName} = state.{p.PropertyName}")
            .ToList();
            
        if (!assignments.Any())
            return "state";
            
        return $"state with {{ {string.Join(", ", assignments)} }}";
    }
}
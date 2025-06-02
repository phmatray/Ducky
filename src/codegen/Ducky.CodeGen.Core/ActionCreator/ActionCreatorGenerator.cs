using System;
using System.Collections.Generic;
using System.Linq;

namespace Ducky.CodeGen.Core;

/// <summary>
/// Encapsulates the logic to generate ActionCreator classes using the visitor pattern.
/// </summary>
public class ActionCreatorGenerator : SourceGeneratorBase<ActionCreatorGeneratorOptions>
{
    protected override CompilationUnitElement BuildModel(ActionCreatorGeneratorOptions opts)
    {
        return new CompilationUnitElement()
        {
            Usings = new List<string>
            {
                "System",
                "System.Text.Json",
                "Ducky",
                opts.Namespace
            },
            Namespaces = new List<NamespaceElement>
            {
                new NamespaceElement()
                {
                    Name = opts.Namespace,
                    Classes = opts.Actions
                        .Select(action => new ClassElement()
                        {
                            Name      = action.ActionName + "Creator",
                            IsStatic  = true,
                            Methods   = BuildMethods(opts.StateType, action)
                        })
                        .ToList()
                }
            }
        };
    }

    // Helper to build the MethodElement list
    private static IEnumerable<MethodElement> BuildMethods(
        string stateType,
        ActionDescriptor action)
    {
        var parametersList = action.Parameters.ToList();
        string[] names = parametersList.Select(p => p.ParamName).ToArray();

        var result = new List<MethodElement>();

        // 1) Create(...)
        result.Add(new MethodElement()
        {
            Name           = "Create",
            ReturnType     = action.ActionName,
            Parameters     = parametersList,
            ExpressionBody = new ExpressionElement()
            {
                // target‐typed new
                Code = $"new {action.ActionName}({string.Join(", ", names)})"
            }
        });

        // 2) Dispatch(...)
        var dispatchParameters = new List<ParameterDescriptor> { new ParameterDescriptor() { ParamName = "store", ParamType = "IStore" } };
        dispatchParameters.AddRange(parametersList);
        
        result.Add(new MethodElement()
        {
            Name              = "Dispatch",
            ReturnType        = "void",
            IsExtensionMethod = true,
            Parameters        = dispatchParameters,
            ExpressionBody    = new ExpressionElement() { Code = $"store.Dispatch(Create({string.Join(", ", names)}))" }
        });

        // 3) AsFluxStandardAction(...)
        result.Add(new MethodElement()
        {
            Name = "AsFluxStandardAction",
            ReturnType = "string",
            IsExtensionMethod = true,
            Parameters = new List<ParameterDescriptor> { new ParameterDescriptor() { ParamName = "action", ParamType = action.ActionName } },
            ExpressionBody = new ExpressionElement()
            {
                Code = $@"JsonSerializer.Serialize(new {{
    type    = nameof({action.ActionName}),
    payload = new {{ {string.Join(", ", names.Select(n => $"action.{n[0].ToString().ToUpperInvariant()}{n.Substring(1)}"))} }},
    meta    = new {{ timestamp = DateTime.UtcNow }}
}})"
            }
        });

        return result;
    }
}

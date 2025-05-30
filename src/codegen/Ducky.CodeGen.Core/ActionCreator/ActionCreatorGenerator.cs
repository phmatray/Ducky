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
            Usings = [
                "System",
                "System.Text.Json",
                "Ducky",
                opts.Namespace
            ],
            Namespaces = [
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
            ]
        };
    }

    // Helper to build the MethodElement list
    private static IEnumerable<MethodElement> BuildMethods(
        string stateType,
        ActionDescriptor action)
    {
        List<ParameterDescriptor> parameters = [.. action.Parameters];
        string[] names = parameters.Select(p => p.ParamName).ToArray();

        // 1) Create(...)
        yield return new MethodElement()
        {
            Name           = "Create",
            ReturnType     = action.ActionName,
            Parameters     = parameters,
            ExpressionBody = new ExpressionElement()
            {
                // target‐typed new
                Code = $"new({string.Join(", ", names)})"
            }
        };

        // 2) Dispatch(...)
        yield return new MethodElement()
        {
            Name              = "Dispatch",
            ReturnType        = "void",
            IsExtensionMethod = true,
            // first param is the store, then all Create() args
            Parameters        = [new ParameterDescriptor() { ParamName = "store", ParamType = "IStore" }, .. parameters],
            ExpressionBody    = new ExpressionElement() { Code = $"store.Dispatch(Create({string.Join(", ", names)}))" }
        };

        // 3) AsFluxStandardAction(...)
        yield return new MethodElement()
        {
            Name = "AsFluxStandardAction",
            ReturnType = "string",
            IsExtensionMethod = true,
            Parameters = [new ParameterDescriptor() { ParamName = "action", ParamType = action.ActionName }],
            ExpressionBody = new ExpressionElement()
            {
                Code = $$"""
                         JsonSerializer.Serialize(new {
                             type    = nameof({{action.ActionName}}),
                             payload = new { {{string.Join(", ", names.Select(n => $"action.{char.ToUpperInvariant(n[0])}{n[1..]}"))}} },
                             meta    = new { timestamp = DateTime.UtcNow }
                         })
                         """
            }
        };
    }
}

namespace Ducky.Generator.Core;

/// <summary>
/// Generates ActionDispatcher extension methods for dispatching actions directly on IDispatcher instances.
/// </summary>
public class ActionDispatcherGenerator : SourceGeneratorBase<ActionDispatcherGeneratorOptions>
{
    /// <summary>
    /// Builds the model representing the compilation unit for action dispatcher generation.
    /// </summary>
    /// <param name="opts">The options containing configuration for action dispatcher generation.</param>
    /// <returns>A <see cref="CompilationUnitElement"/> representing the generated code structure.</returns>
    protected override CompilationUnitElement BuildModel(ActionDispatcherGeneratorOptions opts)
    {
        return new CompilationUnitElement
        {
            Usings = new List<string>
            {
                "System"
            },
            Namespaces = new List<NamespaceElement>
            {
                new()
                {
                    Name = string.Empty, // Global namespace for ActionDispatcher
                    Classes = new List<ClassElement>
                    {
                        new()
                        {
                            Name = "ActionDispatcher",
                            IsStatic = true,
                            IsPartial = true,
                            Methods = new List<MethodElement>
                            {
                                BuildDispatcherMethod(opts)
                            }
                        }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Builds a method element for dispatching a specific action.
    /// </summary>
    /// <param name="opts">The options containing action details and parameters.</param>
    /// <returns>A <see cref="MethodElement"/> representing the dispatcher extension method.</returns>
    private static MethodElement BuildDispatcherMethod(ActionDispatcherGeneratorOptions opts)
    {
        var parameters = new List<ParameterDescriptor>
        {
            new() { ParamName = "dispatcher", ParamType = "Ducky.IDispatcher" }
        };
        parameters.AddRange(opts.Parameters);

        var argumentNames = opts.Parameters.Select(p => p.ParamName).ToList();
        var argumentList = string.Join(", ", argumentNames);

        var methodBody = new ExpressionElement
        {
            Code = $@"{{
        if (dispatcher is null)
        {{
            throw new System.ArgumentNullException(nameof(dispatcher));
        }}

        dispatcher.Dispatch(new {opts.ActionFullyQualifiedName}({argumentList}));
    }}"
        };

        return new MethodElement
        {
            Name = opts.ActionName,
            ReturnType = "void",
            IsExtensionMethod = true,
            Parameters = parameters,
            MethodBody = methodBody,
            XmlDocumentation = $@"/// <summary>
    /// Dispatches a new {opts.ActionName} action.
    /// </summary>
    /// <param name=""dispatcher"">The dispatcher instance.</param>"
        };
    }
}
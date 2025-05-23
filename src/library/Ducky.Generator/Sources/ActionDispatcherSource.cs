using System.Collections.Generic;
using System.Linq;
using Ducky.Generator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generator.Sources;

/// <summary>
/// Represents the generated source for an ActionDispatcher extension method.
/// </summary>
public class ActionDispatcherSource : GeneratedSource
{
    private const string DispatcherParameter = "this IDispatcher dispatcher";

    private readonly string _recordName;
    private readonly string _recordNameFullyQualified;
    private readonly string _methodName;
    private readonly List<string> _methodParameters = [DispatcherParameter];
    private readonly string _argumentList;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActionDispatcherSource"/> class.
    /// </summary>
    /// <param name="semanticModel">The semantic model used to resolve types.</param>
    /// <param name="recordSyntax">The syntax node representing the record declaration.</param>
    /// <param name="recordSymbol">The symbol representing the record type.</param>
    public ActionDispatcherSource(
        SemanticModel semanticModel,
        RecordDeclarationSyntax recordSyntax,
        INamedTypeSymbol recordSymbol)
    {
        _recordName = recordSymbol.Name;
        _recordNameFullyQualified = recordSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        _methodName = _recordName;

        List<string> argListBuilder = [];

        // Helper to process a record parameter.
        void ProcessRecordParameter(ParameterSyntax parameter)
        {
            if (parameter.Type is null)
            {
                return;
            }

            TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
            string typeStr = typeInfo.Type?.ToDisplayString() ?? parameter.Type.ToString();
            string originalName = parameter.Identifier.Text;
            string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);
            string defaultText = parameter.Default is not null ? " = " + parameter.Default.Value : string.Empty;

            _methodParameters.Add($"{typeStr} {paramName}{defaultText}");
            argListBuilder.Add(paramName);
        }

        // Process parameters if record declaration has a parameter list.
        if (recordSyntax.ParameterList is not null)
        {
            foreach (ParameterSyntax parameter in recordSyntax.ParameterList.Parameters)
            {
                ProcessRecordParameter(parameter);
            }
        }
        else
        {
            // Retrieve parameters from the first non-implicit constructor.
            List<IMethodSymbol> ctors = recordSymbol.InstanceConstructors
                .Where(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0)
                .ToList();

            if (ctors.Count > 0)
            {
                IMethodSymbol ctor = ctors[0];
                foreach (IParameterSymbol parameter in ctor.Parameters)
                {
                    string originalName = parameter.Name;
                    string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);
                    string defaultText = parameter.HasExplicitDefaultValue
                        ? parameter.ExplicitDefaultValue is string
                            ? " = \"" + parameter.ExplicitDefaultValue + "\""
                            : " = " + parameter.ExplicitDefaultValue
                        : string.Empty;

                    _methodParameters.Add($"{parameter.Type.ToDisplayString()} {paramName}{defaultText}");
                    argListBuilder.Add(paramName);
                }
            }
        }

        _argumentList = string.Join(", ", argListBuilder);
    }

    /// <inheritdoc/>
    protected override void Build()
    {
        // Using directives.
        Builder.Line("using System;");
        Builder.EmptyLine();
        Builder.Line("public static partial class ActionDispatcher");
        Builder.Braces(() =>
        {
            Builder.Summary($"Dispatches a new {_recordName} action.");
            Builder.SummaryParam("dispatcher", "The dispatcher instance.");
            // Additional parameter summaries can be inserted here.

            Builder.Line($"public static void {_methodName}(");
            Builder.Indent(() =>
            {
                // Append each parameter on its own indented line.
                for (int i = 0; i < _methodParameters.Count; i++)
                {
                    string parameter = _methodParameters[i].Replace("IDispatcher", "Ducky.IDispatcher");
                    Builder.Line(i < _methodParameters.Count - 1 ? $"{parameter}," : $"{parameter})");
                }
            });

            Builder.Braces(() =>
            {
                Builder.Line("if (dispatcher is null)");
                Builder.Braces(() => Builder.Line("throw new System.ArgumentNullException(nameof(dispatcher));"));
                Builder.EmptyLine();
                Builder.Line($"dispatcher.Dispatch(new {_recordNameFullyQualified}({_argumentList}));");
            });
        });
    }
}

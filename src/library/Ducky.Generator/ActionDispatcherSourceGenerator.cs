using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generator.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generator;

/// <summary>
/// A source generator that creates ActionDispatcher extension methods for each action record decorated with the [DuckyAction] attribute.
/// Each generated method is placed in its own partial declaration of the ActionDispatcher class.
/// </summary>
[Generator]
public class ActionDispatcherSourceGenerator : SourceGeneratorBase
{
    private const string GeneratorNamespace = "Ducky.Generators";
    private const string AttributeName = "DuckyActionAttribute";

    /// <inheritdoc/>
    public override void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Inject the marker attribute into the user's compilation.
        context.RegisterPostInitializationOutput(ctx =>
        {
            ActionAttributeSource attributeSource = new(GeneratorNamespace, AttributeName);
            ctx.AddSource($"{AttributeName}.g.cs", attributeSource.ToSourceText());
        });

        // Create a syntax provider that filters for record declarations with any attributes.
        IncrementalValuesProvider<RecordDeclarationSyntax> recordsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is RecordDeclarationSyntax { AttributeLists.Count: > 0 },
                static (ctx, _) => GetRecordDeclarationForSourceGen(ctx))
            .Where(result => result.actionAttributeFound)
            .Select((result, _) => result.recordDeclaration);

        // Combine the overall compilation with the collected record declarations.
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(recordsProvider.Collect()),
            (spc, source) => GenerateCode(source.Left, source.Right, spc));
    }

    private static (RecordDeclarationSyntax recordDeclaration, bool actionAttributeFound)
        GetRecordDeclarationForSourceGen(in GeneratorSyntaxContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        var found = false;
        foreach (AttributeListSyntax attributeList in recordDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                if (context.SemanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol methodSymbol)
                {
                    string attributeFullName = methodSymbol.ContainingType.ToDisplayString();
                    if (attributeFullName == $"{GeneratorNamespace}.{AttributeName}"
                        || attributeFullName.EndsWith("." + AttributeName))
                    {
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                break;
            }
        }

        return (recordDeclaration, found);
    }

    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<RecordDeclarationSyntax> records,
        in SourceProductionContext context)
    {
        foreach (RecordDeclarationSyntax recordSyntax in records)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(recordSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(recordSyntax) is INamedTypeSymbol recordSymbol)
            {
                // Extract parameters from the symbol
                List<ParameterDescriptor> parameters =
                    ExtractParametersFromSymbol(semanticModel, recordSyntax, recordSymbol);

                // Generate the code
                string generatedCode = GenerateActionDispatcherCode(
                    recordSymbol.Name,
                    recordSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    parameters);

                string hintName = $"ActionDispatcher.{recordSymbol.Name}.g.cs";
                AddSource(context, hintName, generatedCode);
            }
        }
    }

    private static List<ParameterDescriptor> ExtractParametersFromSymbol(
        SemanticModel semanticModel,
        RecordDeclarationSyntax recordSyntax,
        INamedTypeSymbol recordSymbol)
    {
        List<ParameterDescriptor> parameters = [];

        // Process parameters if record declaration has a parameter list
        if (recordSyntax.ParameterList is not null)
        {
            foreach (ParameterSyntax parameter in recordSyntax.ParameterList.Parameters)
            {
                if (parameter.Type is not null)
                {
                    TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
                    string typeStr = typeInfo.Type?.ToDisplayString() ?? parameter.Type.ToString();
                    string originalName = parameter.Identifier.Text;
                    string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);

                    // Check for default value
                    string? defaultValue = null;
                    if (parameter.Default is not null)
                    {
                        defaultValue = parameter.Default.Value.ToString();
                    }

                    parameters.Add(new ParameterDescriptor
                    {
                        ParamName = paramName,
                        ParamType = typeStr,
                        DefaultValue = defaultValue
                    });
                }
            }
        }
        else
        {
            // Retrieve parameters from the first non-implicit constructor
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

                    // Check for default value from parameter symbol
                    string? defaultValue = null;
                    if (parameter.HasExplicitDefaultValue)
                    {
                        defaultValue = parameter.ExplicitDefaultValue?.ToString();
                    }

                    parameters.Add(new ParameterDescriptor
                    {
                        ParamName = paramName,
                        ParamType = parameter.Type.ToDisplayString(),
                        DefaultValue = defaultValue
                    });
                }
            }
        }

        return parameters;
    }

    private static string GenerateActionDispatcherCode(
        string actionName,
        string actionFullyQualifiedName,
        List<ParameterDescriptor> parameters)
    {
        System.Text.StringBuilder sb = new();

        // Header comments
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is auto-generated by Ducky.");
        sb.AppendLine();

        // Using directives
        sb.AppendLine("using System;");
        sb.AppendLine();

        // Class declaration
        sb.AppendLine("public static partial class ActionDispatcher");
        sb.AppendLine("{");

        // XML documentation
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Dispatches a new {actionName} action.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"dispatcher\">The dispatcher instance.</param>");

        // Method signature
        sb.Append($"    public static void {actionName}(");
        sb.AppendLine();
        sb.Append("        this Ducky.IDispatcher dispatcher");

        foreach (ParameterDescriptor parameter in parameters)
        {
            sb.AppendLine(",");
            sb.Append($"        {parameter.ParamType} {parameter.ParamName}");
            if (parameter.DefaultValue is not null)
            {
                sb.Append($" = {parameter.DefaultValue}");
            }
        }

        sb.AppendLine(")");
        sb.AppendLine("    {");

        // Method body
        sb.AppendLine("        if (dispatcher is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new System.ArgumentNullException(nameof(dispatcher));");
        sb.AppendLine("        }");
        sb.AppendLine();

        string argumentList = string.Join(", ", parameters.Select(p => p.ParamName));
        sb.AppendLine($"        dispatcher.Dispatch(new {actionFullyQualifiedName}({argumentList}));");

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

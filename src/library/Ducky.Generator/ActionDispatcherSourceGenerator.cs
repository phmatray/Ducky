using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Ducky.Generator.Descriptors;
using Ducky.Generator.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
                static (node, _) => IsCandidateRecord(node),
                static (ctx, _) => GetRecordDeclarationForSourceGen(ctx))
            .Where(result => result.actionAttributeFound)
            .Select((result, _) => result.recordDeclaration);

        // Combine the overall compilation with the collected record declarations.
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(recordsProvider.Collect()),
            (spc, source) => GenerateCode(source.Left, source.Right, spc));
    }

    private static bool IsCandidateRecord(SyntaxNode node)
    {
        if (node is not RecordDeclarationSyntax record)
        {
            return false;
        }

        if (record.AttributeLists.Count == 0)
        {
            return false;
        }

        // Check if any attribute might be DuckyAction by syntax alone
        foreach (AttributeListSyntax attributeList in record.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string attributeName = attribute.Name.ToString();
                // Check for both simple name and fully qualified name
                if (attributeName.Contains("DuckyAction")
                    || attributeName == "DuckyAction"
                    || attributeName == "Ducky.Generators.DuckyAction")
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (RecordDeclarationSyntax recordDeclaration, bool actionAttributeFound)
        GetRecordDeclarationForSourceGen(in GeneratorSyntaxContext context)
    {
        var recordDeclaration = (RecordDeclarationSyntax)context.Node;
        
        // We already know it has DuckyAction in the name from IsCandidateRecord
        // Now verify via semantic model
        foreach (AttributeListSyntax attributeList in recordDeclaration.AttributeLists)
        {
            foreach (AttributeSyntax attribute in attributeList.Attributes)
            {
                string attributeName = attribute.Name.ToString();
                if (attributeName.Contains("DuckyAction"))
                {
                    return (recordDeclaration, true);
                }
            }
        }

        return (recordDeclaration, false);
    }

    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<RecordDeclarationSyntax> records,
        in SourceProductionContext context)
    {
        if (records.IsEmpty)
        {
            // No records found, nothing to generate
            return;
        }

        foreach (RecordDeclarationSyntax recordSyntax in records)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(recordSyntax.SyntaxTree);
            if (semanticModel.GetDeclaredSymbol(recordSyntax) is { } recordSymbol)
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
        // Build the parameters string
        List<string> parameterStrings = [];
        foreach (ParameterDescriptor parameter in parameters)
        {
            string paramStr = $"{parameter.ParamType} {parameter.ParamName}";
            if (parameter.DefaultValue is not null)
            {
                paramStr += $" = {parameter.DefaultValue}";
            }

            parameterStrings.Add(paramStr);
        }

        // Build the argument list for action creation
        string actionArguments = string.Join(", ", parameters.Select(p => p.ParamName));

        // Generate the exact formatted code
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// This file is auto-generated by Ducky.");
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("public static partial class ActionDispatcher");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine($"    /// Dispatches a new {actionName} action.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    /// <param name=\"dispatcher\">The dispatcher instance.</param>");
        sb.AppendLine($"    public static void {actionName}(");
        sb.Append("        this Ducky.IDispatcher dispatcher");

        if (parameterStrings.Count > 0)
        {
            sb.AppendLine(",");
            for (int i = 0; i < parameterStrings.Count; i++)
            {
                sb.Append($"        {parameterStrings[i]}");
                sb.AppendLine(i < parameterStrings.Count - 1 ? "," : ")");
            }
        }
        else
        {
            sb.AppendLine(")");
        }

        sb.AppendLine("    {");
        sb.AppendLine("        if (dispatcher is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            throw new System.ArgumentNullException(nameof(dispatcher));");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        dispatcher.Dispatch(new {actionFullyQualifiedName}({actionArguments}));");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

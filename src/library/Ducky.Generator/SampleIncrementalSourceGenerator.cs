using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generator;

/// <summary>
/// A sample source generator that creates a custom report based on class properties. The target class should be annotated with the 'Generators.ReportAttribute' attribute.
/// When using the source code as a baseline, an incremental source generator is preferable because it reduces the performance overhead.
/// </summary>
[Generator]
public class ActionDispatcherSourceGenerator : IIncrementalGenerator
{
    private const string Namespace = "Ducky.Generators";
    private const string AttributeName = "DuckyActionAttribute";

    private const string AttributeSourceCode =
        $$"""
          // <auto-generated/>

          namespace {{Namespace}}
          {
              [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Record)]
              public class {{AttributeName}} : System.Attribute
              {
              }
          }
          """;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Inject the marker attribute into the user's compilation.
        context.RegisterPostInitializationOutput(ctx =>
            ctx.AddSource("DuckyActionAttribute.g.cs", SourceText.From(AttributeSourceCode, Encoding.UTF8)));

        // Create a syntax provider that filters for record declarations that have any attributes.
        IncrementalValuesProvider<RecordDeclarationSyntax> recordsProvider = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is RecordDeclarationSyntax r && r.AttributeLists.Count > 0,
                static (ctx, _) => GetRecordDeclarationForSourceGen(ctx))
            .Where(tuple => tuple.actionAttributeFound)
            .Select((tuple, token) => tuple.recordDeclaration);

        // Combine the overall compilation with all the collected record declarations.
        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(recordsProvider.Collect()),
            (spc, source) => GenerateCode(source.Left, source.Right, spc));
    }

    /// <summary>
    /// For a given record declaration syntax, check whether it has the [DuckyAction] attribute.
    /// </summary>
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

                    // Either the full name matches or it ends with the attribute short name.
                    if (attributeFullName == $"{Namespace}.{AttributeName}" || attributeFullName.EndsWith("." + AttributeName))
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


    /// <summary>
    /// Generates a single source file "ActionDispatcher.g.cs" containing all the extension methods.
    /// </summary>
    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<RecordDeclarationSyntax> records,
        in SourceProductionContext context)
    {
        StringBuilder sb = new();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("public static partial class ActionDispatcher");
        sb.AppendLine("{");

        foreach (RecordDeclarationSyntax? recordSyntax in records)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(recordSyntax.SyntaxTree);
            if (!(semanticModel.GetDeclaredSymbol(recordSyntax) is INamedTypeSymbol recordSymbol))
            {
                continue;
            }

            // The method name will be "Dispatch" plus the record's name.
            string recordName = recordSymbol.Name;
            string methodName = "Dispatch" + recordName;

            // Process the primary constructor parameters (if any)
            string parameterList = string.Empty;
            string argumentList = string.Empty;
            if (recordSyntax.ParameterList is not null)
            {
                SeparatedSyntaxList<ParameterSyntax> parameters = recordSyntax.ParameterList.Parameters;
                List<string> paramListBuilder = [];
                List<string> argListBuilder = [];

                foreach (ParameterSyntax parameter in parameters)
                {
                    if (parameter.Type is null)
                    {
                        continue;
                    }
                    
                    // Retrieve the parameter type.
                    TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
                    string typeStr = typeInfo.Type?.ToDisplayString() ?? parameter.Type.ToString();

                    // Convert the record's parameter name (e.g. "Amount") to a lower-case name (e.g. "amount")
                    string originalName = parameter.Identifier.Text;
                    string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);

                    // Check if there is a default value (e.g. "= 1")
                    string defaultText = string.Empty;
                    if (parameter.Default is not null)
                    {
                        defaultText = " = " + parameter.Default.Value.ToString();
                    }

                    paramListBuilder.Add($"{typeStr} {paramName}{defaultText}");
                    argListBuilder.Add(paramName);
                }

                if (paramListBuilder.Count > 0)
                {
                    parameterList = ", " + string.Join(", ", paramListBuilder);
                    argumentList = string.Join(", ", argListBuilder);
                }
            }

            // Generate the extension method for this action record.
            sb.AppendLine($"    public static void {methodName}(this IDispatcher dispatcher{parameterList})");
            sb.AppendLine("        => dispatcher.Dispatch(new " + recordName + "(" + argumentList + "));");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        context.AddSource("ActionDispatcher.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}

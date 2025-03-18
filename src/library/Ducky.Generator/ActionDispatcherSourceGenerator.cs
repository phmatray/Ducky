using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generator;

/// <summary>
/// A source generator that creates ActionDispatcher extension methods for each action record decorated with the [DuckyAction] attribute.
/// Each generated method is placed in its own partial declaration of the ActionDispatcher class.
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
              /// <summary>
              /// An attribute that marks a record as an action that can be dispatched.
              /// </summary>
              [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
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
    /// Generates one source file per action record.
    /// Each file "ActionDispatcher.{RecordName}.g.cs" contains a partial class ActionDispatcher with a single dispatch extension method.
    /// </summary>
    private void GenerateCode(
        Compilation compilation,
        in ImmutableArray<RecordDeclarationSyntax> records,
        in SourceProductionContext context)
    {
        foreach (RecordDeclarationSyntax recordSyntax in records)
        {
            SemanticModel semanticModel = compilation.GetSemanticModel(recordSyntax.SyntaxTree);
            if (!(semanticModel.GetDeclaredSymbol(recordSyntax) is INamedTypeSymbol recordSymbol))
            {
                continue;
            }

            // The method name will be "Dispatch" plus the record's name.
            string recordName = recordSymbol.Name;
            string methodName = recordName;

            // Use fully qualified type name so that actions from any assembly/namespace are referenced correctly.
            string fullyQualifiedRecordName = recordSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // Build the parameter lines for the dispatch method.
            // Always include the first parameter: "this IDispatcher dispatcher".
            List<string> parameterLines = ["this IDispatcher dispatcher"];

            List<string> argListBuilder = [];

            // First try using the primary constructor (record syntax parameter list).
            if (recordSyntax.ParameterList is not null)
            {
                foreach (ParameterSyntax parameter in recordSyntax.ParameterList.Parameters)
                {
                    if (parameter.Type is null)
                    {
                        continue;
                    }

                    TypeInfo typeInfo = semanticModel.GetTypeInfo(parameter.Type);
                    string typeStr = typeInfo.Type?.ToDisplayString() ?? parameter.Type.ToString();
                    string originalName = parameter.Identifier.Text;
                    string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);
                    string defaultText = string.Empty;
                    if (parameter.Default is not null)
                    {
                        defaultText = " = " + parameter.Default.Value.ToString();
                    }

                    parameterLines.Add($"{typeStr} {paramName}{defaultText}");
                    argListBuilder.Add(paramName);
                }
            }
            else
            {
                // No primary constructor parameters, so look for user-defined constructors.
                List<IMethodSymbol> ctors = recordSymbol.InstanceConstructors
                    .Where(c => !c.IsImplicitlyDeclared && c.Parameters.Length > 0)
                    .ToList();
                if (ctors.Count > 0)
                {
                    // Choose the first available declared constructor.
                    IMethodSymbol ctor = ctors[0];
                    foreach (IParameterSymbol parameter in ctor.Parameters)
                    {
                        string typeStr = parameter.Type.ToDisplayString();
                        string originalName = parameter.Name;
                        string paramName = char.ToLower(originalName[0]) + originalName.Substring(1);
                        string defaultText = string.Empty;
                        if (parameter.HasExplicitDefaultValue)
                        {
                            if (parameter.ExplicitDefaultValue is string)
                            {
                                defaultText = " = \"" + parameter.ExplicitDefaultValue + "\"";
                            }
                            else if (parameter.ExplicitDefaultValue is not null)
                            {
                                defaultText = " = " + parameter.ExplicitDefaultValue.ToString();
                            }
                            else
                            {
                                defaultText = " = null";
                            }
                        }

                        parameterLines.Add($"{typeStr} {paramName}{defaultText}");
                        argListBuilder.Add(paramName);
                    }
                }
            }

            string argumentList = string.Join(", ", argListBuilder);

            // Format the parameter list with each parameter on its own line, appending a comma for all but the last.
            StringBuilder parametersText = new();
            for (int i = 0; i < parameterLines.Count; i++)
            {
                if (i < parameterLines.Count - 1)
                {
                    parametersText.AppendLine($"        {parameterLines[i]},");
                }
                else
                {
                    parametersText.AppendLine($"        {parameterLines[i]}");
                }
            }

            // Build the source file content with the desired formatting.
            StringBuilder sb = new();
            sb.AppendLine("// <auto-generated/>");
            sb.AppendLine("using System;");
            sb.AppendLine();
            sb.AppendLine("public static partial class ActionDispatcher");
            sb.AppendLine("{");
            sb.AppendLine($"    public static void {methodName}(");
            sb.Append(parametersText.ToString());
            sb.AppendLine("    )");
            sb.AppendLine("        => dispatcher.Dispatch(new " + fullyQualifiedRecordName + "(" + argumentList + "));");
            sb.AppendLine("}");

            // Use a unique hint name for each generated file.
            string hintName = $"ActionDispatcher.{recordName}.g.cs";
            context.AddSource(hintName, SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }
}

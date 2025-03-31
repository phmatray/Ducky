using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generator.Core;
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
                ActionDispatcherSource dispatcherSource = new(semanticModel, recordSyntax, recordSymbol);

                string hintName = $"ActionDispatcher.{recordSymbol.Name}.g.cs";
                AddSource(context, hintName, dispatcherSource.ToString());
            }
        }
    }
}

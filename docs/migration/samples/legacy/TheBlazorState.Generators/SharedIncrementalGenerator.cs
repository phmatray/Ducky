using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TheBlazorState.Generators;

[Generator]
internal sealed class SharedIncrementalGenerator : IIncrementalGenerator
{
    private const string SharedAttributeFullName = "TheBlazorState.Attributes.SharedAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var propertyInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            SharedAttributeFullName,
            predicate: static (node, _) => node is PropertyDeclarationSyntax,
            transform: static (ctx, ct) => ExtractPropertyInfo(ctx, ct))
            .Where(static x => x != null);

        var collected = propertyInfos.Collect();

        context.RegisterSourceOutput(collected, Execute);
    }

    private static (SharedFieldData Data, Location PropertyLocation)? ExtractPropertyInfo(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetNode is not PropertyDeclarationSyntax propertyDecl)
            return null;

        if (ctx.TargetSymbol is not IPropertySymbol propertySymbol)
            return null;

        var containingType = propertySymbol.ContainingType;
        if (containingType == null)
            return null;

        // Check if property is partial
        bool isPartialProperty = false;
        foreach (var modifier in propertyDecl.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                isPartialProperty = true;
                break;
            }
        }

        // Check if class is partial
        bool isPartialClass = false;
        foreach (var syntaxRef in containingType.DeclaringSyntaxReferences)
        {
            var syntax = syntaxRef.GetSyntax(ct);
            if (syntax is ClassDeclarationSyntax classDecl)
            {
                foreach (var modifier in classDecl.Modifiers)
                {
                    if (modifier.IsKind(SyntaxKind.PartialKeyword))
                    {
                        isPartialClass = true;
                        break;
                    }
                }
            }
            if (isPartialClass) break;
        }

        // Property type (fully qualified, with nullable annotation)
        var fullyQualifiedWithNullable = SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        string fullTypeName = propertySymbol.Type.ToDisplayString(fullyQualifiedWithNullable);

        string className = containingType.Name;
        string ns = containingType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : containingType.ContainingNamespace.ToDisplayString();

        var propertyLocation = propertySymbol.Locations.FirstOrDefault() ?? Location.None;

        var data = new SharedFieldData(
            PropertyName: propertySymbol.Name,
            FullTypeName: fullTypeName,
            ContainingClassName: className,
            ContainingClassNamespace: ns,
            IsPartialProperty: isPartialProperty,
            IsPartialClass: isPartialClass);

        return (data, propertyLocation);
    }

    private static void Execute(SourceProductionContext spc,
        ImmutableArray<(SharedFieldData Data, Location PropertyLocation)?> properties)
    {
        if (properties.IsDefaultOrEmpty)
            return;

        var grouped = new Dictionary<(string ClassName, string Namespace),
            List<(SharedFieldData Data, Location PropertyLocation)>>();

        foreach (var prop in properties)
        {
            if (prop == null) continue;
            var key = (prop.Value.Data.ContainingClassName, prop.Value.Data.ContainingClassNamespace);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<(SharedFieldData Data, Location PropertyLocation)>();
                grouped[key] = list;
            }
            list.Add(prop.Value);
        }

        foreach (var kvp in grouped)
        {
            ProcessClass(spc, kvp.Value);
        }
    }

    private static void ProcessClass(SourceProductionContext spc,
        List<(SharedFieldData Data, Location PropertyLocation)> properties)
    {
        var first = properties[0];
        var className = first.Data.ContainingClassName;
        var ns = first.Data.ContainingClassNamespace;

        // Validation: partial class
        if (!first.Data.IsPartialClass)
        {
            foreach (var prop in properties)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NonPartialClassForPersist,
                    prop.PropertyLocation,
                    prop.Data.PropertyName,
                    "Shared",
                    className));
            }
            return;
        }

        // Validate each property
        var validProps = new List<SharedPropertyModel>();
        bool hasErrors = false;

        foreach (var prop in properties)
        {
            if (!prop.Data.IsPartialProperty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NonPartialProperty,
                    prop.PropertyLocation,
                    prop.Data.PropertyName,
                    "Shared"));
                hasErrors = true;
                continue;
            }

            validProps.Add(new SharedPropertyModel
            {
                PropertyName = prop.Data.PropertyName,
                FullTypeName = prop.Data.FullTypeName,
                PropertyLocation = prop.PropertyLocation
            });
        }

        if (hasErrors || validProps.Count == 0)
            return;

        var model = new SharedStateModel
        {
            Namespace = ns,
            ClassName = className,
            Properties = validProps,
        };

        string source = SharedEmitter.Emit(model);
        string hintName = string.IsNullOrEmpty(ns)
            ? $"{className}.Shared.g.cs"
            : $"{ns}.{className}.Shared.g.cs";
        spc.AddSource(hintName, source);
    }

    private sealed record SharedFieldData(
        string PropertyName,
        string FullTypeName,
        string ContainingClassName,
        string ContainingClassNamespace,
        bool IsPartialProperty,
        bool IsPartialClass);
}

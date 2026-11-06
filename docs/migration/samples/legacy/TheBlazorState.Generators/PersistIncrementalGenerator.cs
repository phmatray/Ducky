using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TheBlazorState.Generators;

[Generator]
internal sealed class PersistIncrementalGenerator : IIncrementalGenerator
{
    private const string PersistAttributeFullName = "TheBlazorState.Attributes.PersistAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all properties annotated with [Persist]
        var propertyInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            PersistAttributeFullName,
            predicate: static (node, _) => node is PropertyDeclarationSyntax,
            transform: static (ctx, ct) => ExtractPropertyInfo(ctx, ct))
            .Where(static x => x != null);

        var collected = propertyInfos.Collect();

        context.RegisterSourceOutput(collected, Execute);
    }

    private static (PersistFieldData Data, PersistLocationData Location)? ExtractPropertyInfo(
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

        // Get [Persist] attribute data
        AttributeData? persistAttr = null;
        foreach (var attr in ctx.Attributes)
        {
            persistAttr = attr;
            break;
        }
        if (persistAttr == null)
            return null;

        // Extract attribute properties
        string? timeToLive = null;
        foreach (var namedArg in persistAttr.NamedArguments)
        {
            if (namedArg is { Key: "TimeToLive", Value.Value: string ttl })
            {
                timeToLive = ttl;
            }
        }

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

        // Check if the type (or a base type) already implements IDisposable
        bool userImplementsDisposable = false;
        foreach (var member in containingType.GetMembers())
        {
            if (member is IMethodSymbol { Name: "Dispose", Parameters.Length: 0, IsAbstract: false })
            {
                userImplementsDisposable = true;
                break;
            }
        }
        // Also check base types for a virtual Dispose (e.g., StateComponentBase)
        if (!userImplementsDisposable)
        {
            var baseType = containingType.BaseType;
            while (baseType is not null && baseType.SpecialType != Microsoft.CodeAnalysis.SpecialType.System_Object)
            {
                foreach (var member in baseType.GetMembers())
                {
                    if (member is IMethodSymbol { Name: "Dispose", Parameters.Length: 0, IsVirtual: true })
                    {
                        userImplementsDisposable = true;
                        break;
                    }
                }
                if (userImplementsDisposable) break;
                baseType = baseType.BaseType;
            }
        }

        // Check user overrides
        bool userOverridesOnInitialized = false;
        bool userOverridesOnInitializedAsync = false;
        foreach (var member in containingType.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                if (method is { Name: "OnInitialized", Parameters.Length: 0, IsOverride: true })
                    userOverridesOnInitialized = true;
                else if (method is { Name: "OnInitializedAsync", Parameters.Length: 0, IsOverride: true })
                    userOverridesOnInitializedAsync = true;
            }
        }

        // Property type (fully qualified, with nullable annotation)
        var fullyQualifiedWithNullable = SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);
        string fullTypeName = propertySymbol.Type.ToDisplayString(fullyQualifiedWithNullable);

        string className = containingType.Name;
        string ns = containingType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : containingType.ContainingNamespace.ToDisplayString();

        string baseKey = className + "." + propertySymbol.Name;

        var propertyLocation = propertySymbol.Locations.FirstOrDefault();
        var classLocation = containingType.Locations.FirstOrDefault();

        var data = new PersistFieldData(
            PropertyName: propertySymbol.Name,
            FullTypeName: fullTypeName,
            ContainingClassName: className,
            ContainingClassNamespace: ns,
            TimeToLive: timeToLive,
            BaseKey: baseKey,
            IsPartialProperty: isPartialProperty,
            IsPartialClass: isPartialClass,
            UserImplementsDisposable: userImplementsDisposable,
            UserOverridesOnInitialized: userOverridesOnInitialized,
            UserOverridesOnInitializedAsync: userOverridesOnInitializedAsync);

        var locationData = new PersistLocationData
        {
            PropertyLocation = propertyLocation ?? Location.None
        };

        return (data, locationData);
    }

    private static void Execute(SourceProductionContext spc,
        ImmutableArray<(PersistFieldData Data, PersistLocationData Location)?> properties)
    {
        if (properties.IsDefaultOrEmpty)
            return;

        var grouped = new Dictionary<(string ClassName, string Namespace),
            List<(PersistFieldData Data, PersistLocationData Location)>>();

        foreach (var prop in properties)
        {
            if (prop == null) continue;
            var key = (prop.Value.Data.ContainingClassName, prop.Value.Data.ContainingClassNamespace);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<(PersistFieldData Data, PersistLocationData Location)>();
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
        List<(PersistFieldData Data, PersistLocationData Location)> properties)
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
                    prop.Location.PropertyLocation,
                    prop.Data.PropertyName,
                    "Persist",
                    className));
            }
            return;
        }

        // Validate each property
        var validProps = new List<PersistPropertyModel>();
        bool hasErrors = false;

        foreach (var prop in properties)
        {
            // Check partial property
            if (!prop.Data.IsPartialProperty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.NonPartialProperty,
                    prop.Location.PropertyLocation,
                    prop.Data.PropertyName,
                    "Persist"));
                hasErrors = true;
                continue;
            }

            // Validate TimeToLive
            if (prop.Data.TimeToLive != null)
            {
                if (!TimeSpan.TryParse(prop.Data.TimeToLive, out _))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.InvalidPersistTimeToLive,
                        prop.Location.PropertyLocation,
                        prop.Data.TimeToLive,
                        prop.Data.PropertyName));
                    hasErrors = true;
                    continue;
                }
            }

            validProps.Add(new PersistPropertyModel
            {
                PropertyName = prop.Data.PropertyName,
                FullTypeName = prop.Data.FullTypeName,
                TimeToLive = prop.Data.TimeToLive,
                BaseKey = prop.Data.BaseKey,
                PropertyLocation = prop.Location.PropertyLocation
            });
        }

        if (hasErrors || validProps.Count == 0)
            return;

        var model = new PersistComponentModel
        {
            Namespace = ns,
            ClassName = className,
            Properties = validProps,
            UserImplementsDisposable = first.Data.UserImplementsDisposable,
            UserOverridesOnInitialized = first.Data.UserOverridesOnInitialized,
            UserOverridesOnInitializedAsync = first.Data.UserOverridesOnInitializedAsync,
        };

        string source = PersistEmitter.Emit(model);
        string hintName = string.IsNullOrEmpty(ns)
            ? $"{className}.Persist.g.cs"
            : $"{ns}.{className}.Persist.g.cs";
        spc.AddSource(hintName, source);
    }

    private readonly struct PersistLocationData
    {
        public Location PropertyLocation { get; init; }
    }

    private sealed record PersistFieldData(
        string PropertyName,
        string FullTypeName,
        string ContainingClassName,
        string ContainingClassNamespace,
        string? TimeToLive,
        string BaseKey,
        bool IsPartialProperty,
        bool IsPartialClass,
        bool UserImplementsDisposable,
        bool UserOverridesOnInitialized,
        bool UserOverridesOnInitializedAsync);
}

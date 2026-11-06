using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TheBlazorState.Generators;

[Generator]
internal sealed class InjectSubscriptionGenerator : IIncrementalGenerator
{
    private const string InjectAttributeFullName = "Microsoft.AspNetCore.Components.InjectAttribute";
    private const string SharedAttributeFullName = "TheBlazorState.Attributes.SharedAttribute";
    private const string PersistAttributeFullName = "TheBlazorState.Attributes.PersistAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all [Inject] properties
        var injectInfos = context.SyntaxProvider.ForAttributeWithMetadataName(
            InjectAttributeFullName,
            predicate: static (node, _) => node is PropertyDeclarationSyntax,
            transform: static (ctx, ct) => ExtractInjectInfo(ctx, ct))
            .Where(static x => x != null);

        var collected = injectInfos.Collect();
        context.RegisterSourceOutput(collected, Execute);
    }

    private static InjectData? ExtractInjectInfo(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (ctx.TargetSymbol is not IPropertySymbol propSymbol)
            return null;

        var containingType = propSymbol.ContainingType;
        if (containingType == null)
            return null;

        // Must be in a partial class inheriting ComponentBase
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

        if (!isPartialClass) return null;

        // Check ComponentBase inheritance
        bool inheritsComponentBase = false;
        var current = containingType.BaseType;
        while (current != null)
        {
            var name = current.ToDisplayString();
            if (name == "Microsoft.AspNetCore.Components.ComponentBase"
                || name == "Microsoft.AspNetCore.Components.LayoutComponentBase")
            {
                inheritsComponentBase = true;
                break;
            }
            current = current.BaseType;
        }

        if (!inheritsComponentBase) return null;

        // Check if the injected type has any [Shared] properties
        bool hasSharedProperties = false;
        var injectedType = propSymbol.Type;
        foreach (var member in injectedType.GetMembers())
        {
            if (member is IPropertySymbol memberProp)
            {
                foreach (var attr in memberProp.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == SharedAttributeFullName)
                    {
                        hasSharedProperties = true;
                        break;
                    }
                }
            }
            if (hasSharedProperties) break;
        }

        if (!hasSharedProperties) return null;

        // Check if containing class has [Persist] properties
        bool hasPersistProperties = false;
        foreach (var member in containingType.GetMembers())
        {
            if (member is IPropertySymbol classProp)
            {
                foreach (var attr in classProp.GetAttributes())
                {
                    if (attr.AttributeClass?.ToDisplayString() == PersistAttributeFullName)
                    {
                        hasPersistProperties = true;
                        break;
                    }
                }
            }
            if (hasPersistProperties) break;
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

        string className = containingType.Name;
        string ns = containingType.ContainingNamespace.IsGlobalNamespace
            ? ""
            : containingType.ContainingNamespace.ToDisplayString();

        return new InjectData(
            PropertyName: propSymbol.Name,
            ClassName: className,
            Namespace: ns,
            HasPersistProperties: hasPersistProperties,
            UserImplementsDisposable: userImplementsDisposable);
    }

    private static void Execute(SourceProductionContext spc, ImmutableArray<InjectData?> injects)
    {
        if (injects.IsDefaultOrEmpty) return;

        // Group by class
        var grouped = new Dictionary<(string, string), List<InjectData>>();
        foreach (var inject in injects)
        {
            if (inject == null) continue;
            var key = (inject.ClassName, inject.Namespace);
            if (!grouped.TryGetValue(key, out var list))
            {
                list = new List<InjectData>();
                grouped[key] = list;
            }
            list.Add(inject);
        }

        foreach (var kvp in grouped)
        {
            var items = kvp.Value;
            var first = items[0];

            // Deduplicate property names
            var uniqueNames = new HashSet<string>();
            foreach (var item in items) uniqueNames.Add(item.PropertyName);

            var model = new InjectSubscriptionModel
            {
                Namespace = first.Namespace,
                ClassName = first.ClassName,
                SharedStatePropertyNames = uniqueNames.ToList(),
                UserImplementsDisposable = first.UserImplementsDisposable,
                HasPersistProperties = first.HasPersistProperties
            };

            string source = InjectSubscriptionEmitter.Emit(model);
            string hintName = string.IsNullOrEmpty(model.Namespace)
                ? $"{model.ClassName}.InjectSub.g.cs"
                : $"{model.Namespace}.{model.ClassName}.InjectSub.g.cs";
            spc.AddSource(hintName, source);
        }
    }

    private sealed record InjectData(
        string PropertyName,
        string ClassName,
        string Namespace,
        bool HasPersistProperties,
        bool UserImplementsDisposable);
}

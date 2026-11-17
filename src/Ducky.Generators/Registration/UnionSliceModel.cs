using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generators.Registration;

/// <summary>
/// A slice deriving directly from <c>Ducky.Slice&lt;TState, TAction&gt;</c> (ADR-0051), symbol-free. Emitted as an override
/// of <c>RegisterCases</c> in its <see cref="Declarations"/> (outermost first, inside <see cref="Namespace"/>), one
/// <see cref="Registrations"/> line per case of the union, when <see cref="NotUnion"/> and <see cref="NotPartial"/> are
/// null; otherwise DUCKY026 (the action type) or DUCKY027 (the types to declare partial) at <see cref="Location"/>.
/// </summary>
internal sealed record UnionSliceModel(
    string Display, string HintName, string? Namespace, EquatableArray<string> Declarations, EquatableArray<string> Registrations,
    string? NotUnion, string? NotPartial, Location Location)
{
    private static readonly SymbolDisplayFormat _declaration = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    /// <summary>The <see cref="Classify"/> of a class with a base list, the generator's syntax step.</summary>
    public static UnionSliceModel? From(GeneratorSyntaxContext context, CancellationToken cancellationToken) =>
        Classify((INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken)!, context.SemanticModel.Compilation, cancellationToken);

    /// <summary>Null unless <paramref name="type"/>'s direct base is <c>Ducky.Slice&lt;TState, TAction&gt;</c>.</summary>
    public static UnionSliceModel? Classify(INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
    {
        var unionSlice = compilation.GetTypeByMetadataName("Ducky.Slice`2");
        if (type.BaseType is not { } baseType || !SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, unionSlice))
        {
            return null;
        }

        var display = type.ToDisplayString();
        var location = type.Locations[0];
        var action = baseType.TypeArguments[1];
        var model = new UnionSliceModel(display, IdentifierSanitizer.HintName(display), null, default, default, null, null, location);
        var iunion = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IUnion");
        if (!IsUnion(action, iunion))
        {
            return model with { NotUnion = action.ToDisplayString() };
        }

        var chain = new List<INamedTypeSymbol>();
        for (var t = type; t is not null; t = t.ContainingType)
        {
            chain.Insert(0, t);
        }

        var notPartial = chain.Where(t => !t.DeclaringSyntaxReferences.All(r =>
                ((TypeDeclarationSyntax)r.GetSyntax(cancellationToken)).Modifiers.Any(SyntaxKind.PartialKeyword)))
            .Select(t => $"'{t.ToDisplayString()}'").ToList();
        if (notPartial.Count > 0)
        {
            return model with { NotPartial = string.Join(", ", notPartial) };
        }

        // A case that is itself a union is unwrapped to its own cases, as Dispatch unwraps (#304); the first path to a
        // case wins, since On<T> takes one handler per type.
        var registrations = new List<string>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        AddCases(action, static value => value, registrations, seen, iunion);
        return model with
        {
            Namespace = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            Declarations = new([.. chain.Select(t => $"partial {Keyword(t)} {t.ToDisplayString(_declaration)}")]),
            Registrations = new([.. registrations]),
        };
    }

    // ADR-0050: a union implements IUnion, and its cases are the parameter types of its public single-parameter instance
    // constructors.
    private static bool IsUnion(ITypeSymbol type, INamedTypeSymbol? iunion) =>
        iunion is not null && type.AllInterfaces.Contains(iunion, SymbolEqualityComparer.Default);

    // wrap puts a value of this union into the slice's own action type, through the enclosing unions.
    private static void AddCases(ITypeSymbol union, Func<string, string> wrap, List<string> registrations, HashSet<ITypeSymbol> seen, INamedTypeSymbol? iunion)
    {
        var name = union.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var cases = ((INamedTypeSymbol)union).InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 1)
            .Select(c => c.Parameters[0].Type);
        foreach (var @case in cases)
        {
            if (IsUnion(@case, iunion))
            {
                AddCases(@case, value => wrap($"new {name}({value})"), registrations, seen, iunion);
            }
            else if (seen.Add(@case))
            {
                registrations.Add($"On<{@case.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>((s, a) => Reduce(s, {wrap($"new {name}(a)")}));");
            }
        }
    }

    private static string Keyword(INamedTypeSymbol type) =>
        (type.IsRecord ? "record " : string.Empty) + (type.TypeKind == TypeKind.Struct ? "struct" : type.TypeKind == TypeKind.Interface ? "interface" : "class");
}

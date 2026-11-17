using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generators.Registration;

/// <summary>
/// A union-first type (ADR-0051) deriving directly from <c>Ducky.Slice&lt;TState, TAction&gt;</c> or
/// <c>Ducky.EffectGroup&lt;TAction&gt;</c>, named by <see cref="Base"/>, symbol-free. Emitted as an override of
/// <c>RegisterCases</c> in its <see cref="Declarations"/> (outermost first, inside <see cref="Namespace"/>), one
/// <see cref="Registrations"/> line per case of the union, when <see cref="NotUnion"/> and <see cref="NotPartial"/> are
/// null; otherwise DUCKY026 (the action type) or DUCKY027 (the types to declare partial) at <see cref="Location"/>.
/// </summary>
internal sealed record UnionSliceModel(
    string Display, string Base, string HintName, string? Namespace, EquatableArray<string> Declarations, EquatableArray<string> Registrations,
    string? NotUnion, string? NotPartial, Location Location)
{
    private static readonly SymbolDisplayFormat _declaration = new(
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    // The union-first bases: where the action type is among the type arguments, the hint-name suffix, and the registration
    // of one case ({0}) through the expression that wraps the case value `a` into the action type ({1}).
    private static readonly ImmutableArray<Kind> _kinds =
    [
        new("Ducky.Slice`2", 1, "Slice<TState, TAction>", ".UnionSlice", "On<{0}>((s, a) => Reduce(s, {1}));"),
        new("Ducky.EffectGroup`1", 0, "EffectGroup<TAction>", ".UnionEffectGroup", "On<{0}>((a, c, t) => Handle({1}, c, t));"),
    ];

    /// <summary>The <see cref="Classify"/> of a class with a base list, the generator's syntax step.</summary>
    public static UnionSliceModel? From(GeneratorSyntaxContext context, CancellationToken cancellationToken) =>
        Classify((INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken)!, context.SemanticModel.Compilation, cancellationToken);

    /// <summary>Null unless <paramref name="type"/>'s direct base is a union-first base.</summary>
    public static UnionSliceModel? Classify(INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
    {
        if (type.BaseType is not { } baseType
            || _kinds.FirstOrDefault(k => SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, compilation.GetTypeByMetadataName(k.MetadataName))) is not { } kind)
        {
            return null;
        }

        // An open action type (a reusable generic base) has no cases to read; its concrete subclasses aren't generated
        // either, since only the direct derivation is.
        var action = baseType.TypeArguments[kind.ActionIndex];
        if (action is ITypeParameterSymbol)
        {
            return null;
        }

        var display = type.ToDisplayString();
        var location = type.Locations[0];
        var model = new UnionSliceModel(display, kind.Display, IdentifierSanitizer.HintName(display) + kind.Suffix, null, default, default, null, null, location);
        var iunion = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IUnion");
        if (!UnionCases.IsUnion(action, iunion))
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

        // The first path to a case wins, since On<T> takes one handler per type.
        var registrations = UnionCases.Of(action, iunion).Select(c => string.Format(
            CultureInfo.InvariantCulture, kind.Registration, c.Case.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), c.Wrap("a")));
        return model with
        {
            Namespace = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            Declarations = new([.. chain.Select(t => $"partial {Keyword(t)} {t.ToDisplayString(_declaration)}")]),
            Registrations = new([.. registrations]),
        };
    }

    private static string Keyword(INamedTypeSymbol type) =>
        (type.IsRecord ? "record " : string.Empty) + (type.TypeKind == TypeKind.Struct ? "struct" : type.TypeKind == TypeKind.Interface ? "interface" : "class");

    private sealed record Kind(string MetadataName, int ActionIndex, string Display, string Suffix, string Registration);
}

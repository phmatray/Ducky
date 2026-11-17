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
/// <c>Ducky.EffectGroup&lt;TAction&gt;</c>, symbol-free. Emitted as an override of
/// <c>RegisterCases</c> in its <see cref="Declarations"/> (outermost first, inside <see cref="Namespace"/>), one
/// <see cref="Registrations"/> line per case of the union, when <see cref="Rule"/> is null; otherwise that diagnostic,
/// formatted with <see cref="Arguments"/>, at <see cref="Location"/>: DUCKY026 (not a union), DUCKY027 (not partial),
/// DUCKY029 (a case dispatch never matches), DUCKY030 (file-local) or DUCKY031 (a hand-written <c>RegisterCases</c>).
/// </summary>
internal sealed record UnionSliceModel(
    string Display, string HintName, string? Namespace, EquatableArray<string> Declarations, EquatableArray<string> Registrations,
    DiagnosticDescriptor? Rule, EquatableArray<string> Arguments, Location Location)
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

    /// <summary>Null unless <paramref name="type"/>'s direct base is a union-first base with a closed action type.</summary>
    public static UnionSliceModel? Classify(INamedTypeSymbol type, Compilation compilation, CancellationToken cancellationToken)
    {
        if (ActionOf(type, compilation) is not ({ } kind, { } action))
        {
            return null;
        }

        var display = type.ToDisplayString();
        var location = type.Locations[0];
        var model = new UnionSliceModel(display, IdentifierSanitizer.HintName(display) + kind.Suffix, null, default, default, null, default, location);
        UnionSliceModel Reported(DiagnosticDescriptor rule, params string[] arguments) => model with { Rule = rule, Arguments = new([display, .. arguments]) };

        var chain = new List<INamedTypeSymbol>();
        for (var t = type; t is not null; t = t.ContainingType)
        {
            chain.Insert(0, t);
        }

        // A file-local type can't span two files, so no generated declaration can extend it (its DUCKY023 is dropped).
        if (chain[0].IsFileLocal)
        {
            return Reported(RegistrationDiagnostics.FileLocal, chain[0].ToDisplayString());
        }

        var iunion = UnionCases.Resolve(compilation);
        if (!UnionCases.IsUnion(action, iunion))
        {
            return Reported(RegistrationDiagnostics.NotUnion, kind.Display, action.ToDisplayString());
        }

        // Checked before partial: a hand-written override is the generator-off shape, which needs no partial.
        if (type.GetMembers("RegisterCases").Any(m => m is IMethodSymbol { Parameters.IsEmpty: true, IsGenericMethod: false }))
        {
            return Reported(RegistrationDiagnostics.HandWritten);
        }

        var notPartial = chain.Where(t => !t.DeclaringSyntaxReferences.All(r =>
                ((TypeDeclarationSyntax)r.GetSyntax(cancellationToken)).Modifiers.Any(SyntaxKind.PartialKeyword)))
            .Select(t => $"'{t.ToDisplayString()}'").ToList();
        if (notPartial.Count > 0)
        {
            return Reported(RegistrationDiagnostics.NotPartial, string.Join(", ", notPartial));
        }

        // Dispatch matches an action's exact runtime type (ADR-0008): never an abstract type, an interface or object
        // (On<T> throws DUCKY307), nor a Nullable<T>, which boxes to its T. The first path to a case wins, since On<T>
        // takes one handler per type.
        var cases = UnionCases.Of(action, iunion);
        var undispatchable = cases.Where(c => c.Case.IsAbstract || c.Case.SpecialType == SpecialType.System_Object
                || c.Case.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            .Select(c => $"'{c.Case.ToDisplayString()}'").ToList();
        if (undispatchable.Count > 0)
        {
            return Reported(RegistrationDiagnostics.Undispatchable, string.Join(", ", undispatchable));
        }

        var registrations = cases.Select(c => string.Format(
            CultureInfo.InvariantCulture, kind.Registration, c.Case.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), c.Wrap("a")));
        return model with
        {
            Namespace = type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(),
            Declarations = new([.. chain.Select(t => $"partial {Keyword(t)} {t.ToDisplayString(_declaration)}")]),
            Registrations = new([.. registrations]),
        };
    }

    /// <summary>
    /// Whether <paramref name="type"/> derives directly from a union-first base with a closed action type: what
    /// <see cref="Classify"/> models, so registration leaves a file-local one to DUCKY030.
    /// </summary>
    public static bool IsUnionFirst(INamedTypeSymbol type, Compilation compilation) => ActionOf(type, compilation).Action is not null;

    // An open action type (a reusable generic base) has no cases to read; its concrete subclasses aren't generated
    // either, since only the direct derivation is.
    private static (Kind? Kind, ITypeSymbol? Action) ActionOf(INamedTypeSymbol type, Compilation compilation)
    {
        var baseType = type.BaseType;
        var kind = baseType is null ? null
            : _kinds.FirstOrDefault(k => SymbolEqualityComparer.Default.Equals(baseType.OriginalDefinition, compilation.GetTypeByMetadataName(k.MetadataName)));
        return kind is null || baseType!.TypeArguments[kind.ActionIndex] is ITypeParameterSymbol ? (null, null) : (kind, baseType.TypeArguments[kind.ActionIndex]);
    }

    private static string Keyword(INamedTypeSymbol type) =>
        (type.IsRecord ? "record " : string.Empty) + (type.TypeKind == TypeKind.Struct ? "struct" : type.TypeKind == TypeKind.Interface ? "interface" : "class");

    private sealed record Kind(string MetadataName, int ActionIndex, string Display, string Suffix, string Registration);
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Ducky.Generators.Dispatch;

/// <summary>
/// One <c>[DuckyAction]</c> type as GEN-01 emits it (SPEC §16.1), symbol-free so the pipeline caches (GEN-06).
/// <see cref="Error"/> is the DUCKY020 reason; the other members are filled in either way.
/// </summary>
internal sealed record ActionModel(
    string TypeName,
    string DisplayName,
    string? Namespace,
    string ClassName,
    string MethodName,
    EquatableArray<HelperModel> Helpers,
    EquatableArray<string> DiagnosticIds,
    string? Error,
    LocationInfo Location)
{
    // Generated code names user types fully qualified, with `?` on annotated reference types.
    private static readonly SymbolDisplayFormat _format =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat _namespaceFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    public static ActionModel Create(GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var compilation = context.SemanticModel.Compilation;
        var obsolete = compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
        var experimental = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.ExperimentalAttribute");
        var setsRequired = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
        var containers = Containers(type);
        var className = string.Join("_", containers.Select(t => t.Name).Reverse()) + "DispatchExtensions";
        var methodName = context.Attributes[0].NamedArguments
            .Where(a => a.Key == "MethodName")
            .Select(a => a.Value.Value as string)
            .FirstOrDefault() ?? type.Name;

        // One helper per accessible, explicitly declared constructor; the implicit ones only when none is declared, and
        // never the record copy constructor (the implicit Inc() of a record struct would otherwise dispatch By = 0).
        // While the type or a base type has a required member, only a [SetsRequiredMembers] one works with `new` (CS9035).
        var hasRequired = BaseTypes(type).SelectMany(t => t.GetMembers()).Any(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true });
        var constructors = type.InstanceConstructors.Where(c => !type.IsAbstract && !type.IsRefLikeType && !IsCopy(c, type)).ToList();
        var declared = constructors.Where(c => !c.IsImplicitlyDeclared).ToList();
        var typePublic = containers.All(t => t.DeclaredAccessibility == Accessibility.Public);
        var usable = (declared.Count > 0 ? declared : constructors)
            .Where(c => IsReachable(c.DeclaredAccessibility) && !IsErrorObsolete(c, obsolete))
            .Where(c => !hasRequired || c.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, setsRequired)))
            .Where(c => !c.Parameters.Any(p => Parts(p.Type).Any(t => IsErrorObsolete(t, obsolete))))
            .ToList();

        // A parameter type that doesn't resolve (yet) gets no helper, and no DUCKY020 either: the compiler reports CS0246.
        var resolved = usable.Where(c => !c.Parameters.Any(p => Parts(p.Type).Any(t => t.TypeKind == TypeKind.Error))).ToList();
        var helpers = resolved.Select(c => Helper(c, typePublic, compilation)).ToImmutableArray();

        // An [Obsolete] DiagnosticId or an [Experimental] id replaces CS0618 for the symbols generated code names.
        var diagnosticIds = containers
            .Concat<ISymbol>(resolved)
            .Concat(resolved.SelectMany(c => c.Parameters).SelectMany(p => Parts(p.Type)))
            .SelectMany(s => DiagnosticIdsOf(s, obsolete, experimental))
            .OfType<string>()
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToImmutableArray();

        var error =
            type.IsGenericType ? "it is generic or nested in a generic type" // IsGenericType counts containing types
            : containers.Any(t => t.IsFileLocal || !IsReachable(t.DeclaredAccessibility)) ? "it is file-local or not accessible from its namespace"
            : containers.Any(t => IsErrorObsolete(t, obsolete)) ? "it is marked [Obsolete(error: true)]"
            : usable.Count == 0 ? "it has no accessible constructor that is usable with new, not [Obsolete(error: true)], takes no error-obsolete parameter type and sets every required member"
            : !SyntaxFacts.IsValidIdentifier(methodName) ? $"MethodName '{methodName}' is not a valid identifier"
            : methodName == className ? $"MethodName '{methodName}' is the name of the generated class" // CS0542
            : type.ContainingNamespace.GetMembers(className).Any() ? $"a type or namespace named '{className}' is already declared in its namespace"
            : null;

        var location = ((TypeDeclarationSyntax)context.TargetNode).Identifier.GetLocation();
        return new ActionModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.ToDisplayString(),
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(_namespaceFormat),
            className,
            methodName,
            new EquatableArray<HelperModel>(helpers),
            new EquatableArray<string>(diagnosticIds),
            error,
            new LocationInfo(location.SourceTree!.FilePath, location.SourceSpan, location.GetLineSpan().Span));
    }

    // The type itself, then each containing type outwards.
    private static List<INamedTypeSymbol> Containers(INamedTypeSymbol type)
    {
        var containers = new List<INamedTypeSymbol>();
        for (INamedTypeSymbol? t = type; t is not null; t = t.ContainingType)
        {
            containers.Add(t);
        }

        return containers;
    }

    // The type itself, then each base type.
    private static IEnumerable<INamedTypeSymbol> BaseTypes(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? t = type; t is not null; t = t.BaseType)
        {
            yield return t;
        }
    }

    private static IEnumerable<object?> DiagnosticIdsOf(ISymbol symbol, INamedTypeSymbol? obsolete, INamedTypeSymbol? experimental) =>
        symbol.GetAttributes().SelectMany(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) ? a.NamedArguments.Where(n => n.Key == "DiagnosticId").Select(n => n.Value.Value)
            : SymbolEqualityComparer.Default.Equals(a.AttributeClass, experimental) ? a.ConstructorArguments.Select(c => c.Value)
            : []);

    // Every named type a type mentions: itself, its containing types, their type arguments and array elements
    // (Outer<Gone>.Inner names Gone).
    private static IEnumerable<ITypeSymbol> Parts(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol => Parts(((IArrayTypeSymbol)type).ElementType),
        INamedTypeSymbol => Containers((INamedTypeSymbol)type).SelectMany(t => t.TypeArguments.SelectMany(Parts).Prepend(t)),
        _ => [],
    };

    // Reachable from a static class in the same namespace (protected internal counts as internal there).
    private static bool IsReachable(Accessibility accessibility) =>
        accessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal;

    // CS0619 can't be suppressed, so generated code never names an [Obsolete(…, error: true)] symbol.
    private static bool IsErrorObsolete(ISymbol symbol, INamedTypeSymbol? obsolete) =>
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) && a.ConstructorArguments.Any(arg => arg.Value is true));

    private static bool IsCopy(IMethodSymbol constructor, INamedTypeSymbol type) =>
        constructor.Parameters.Length == 1 && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, type);

    // The method is internal as soon as the type or the constructor is: C# already requires every parameter type to be
    // at least as accessible as the constructor.
    private static HelperModel Helper(IMethodSymbol constructor, bool typePublic, Compilation compilation) => new(
        typePublic && constructor.DeclaredAccessibility == Accessibility.Public,
        string.Concat(constructor.Parameters.Select(p => ", " + Declaration(p, compilation))),
        string.Join(", ", constructor.Parameters.Select(p => ArgumentPrefix(p.RefKind) + IdentifierSanitizer.EscapeKeyword(p.Name))),
        string.Join(", ", constructor.Parameters.Select(p => DeclarationPrefix(p.RefKind) + Erase(p.Type, compilation).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));

    // Overload resolution ignores tuple element names, so the DUCKY024 signature does too: rebuilding a constructed type
    // from its definition drops them, `(int A, Dup B)` and `(int X, Dup Y)` both become `(int, Dup)`, also in a containing
    // type: Outer<(int A, int B)>.Inner becomes Outer<(int, int)>.Inner.
    private static ITypeSymbol Erase(ITypeSymbol type, Compilation compilation) => type switch
    {
        IArrayTypeSymbol array => compilation.CreateArrayTypeSymbol(Erase(array.ElementType, compilation), array.Rank),
        INamedTypeSymbol { Arity: > 0 } named => Definition(named, compilation).Construct(named.TypeArguments.Select(t => Erase(t, compilation)).ToArray()),
        INamedTypeSymbol { ContainingType: not null } named => Definition(named, compilation),
        _ => type,
    };

    // A nested type is looked up again in its erased containing type; a top-level one is its own definition.
    private static INamedTypeSymbol Definition(INamedTypeSymbol named, Compilation compilation) =>
        named.ContainingType is null
            ? named.ConstructedFrom
            : ((INamedTypeSymbol)Erase(named.ContainingType, compilation)).GetTypeMembers(named.Name, named.Arity).Single();

    private static string Declaration(IParameterSymbol parameter, Compilation compilation) =>
        (parameter.IsParams ? "params " : "")
        + DeclarationPrefix(parameter.RefKind)
        + Annotate(parameter.Type, compilation).ToDisplayString(_format)
        + " " + IdentifierSanitizer.EscapeKeyword(parameter.Name)
        + (parameter.HasExplicitDefaultValue ? " = " + DefaultValue(parameter) : "");

    private static string ArgumentPrefix(RefKind refKind) => refKind switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.RefReadOnlyParameter => "in ",
        _ => "",
    };

    private static string DeclarationPrefix(RefKind refKind) => refKind switch
    {
        RefKind.In => "in ",
        RefKind.RefReadOnlyParameter => "ref readonly ",
        _ => ArgumentPrefix(refKind),
    };

    // GEN-06: an oblivious reference type (user source under #nullable disable) counts as annotated at every position,
    // so `string Query = null` becomes `string? Query = null` in the #nullable enable file instead of CS8625, except as
    // the argument of a type parameter that can't be nullable (Dictionary<string, int> stays `string`, no CS8714). Arity,
    // not IsGenericType: a type nested in a generic type (Dictionary<K, V>.KeyCollection) has no type arguments of its own.
    // A type with no oblivious position is returned as is, so nullable-enabled code is never rebuilt, and a tuple is
    // rebuilt as a tuple: Construct would drop its element names (CS8123 on a named tuple literal).
    private static ITypeSymbol Annotate(ITypeSymbol type, Compilation compilation, bool nullable = true)
    {
        if (!HasOblivious(type))
        {
            return type;
        }

        var rebuilt = type switch
        {
            IArrayTypeSymbol => AnnotateArray((IArrayTypeSymbol)type, compilation),
            INamedTypeSymbol { IsTupleType: true } => AnnotateTuple((INamedTypeSymbol)type, compilation),
            INamedTypeSymbol { Arity: > 0 } => AnnotateGeneric((INamedTypeSymbol)type, compilation),
            _ => type,
        };
        return rebuilt.WithNullableAnnotation(
            !type.IsReferenceType || type.NullableAnnotation != NullableAnnotation.None ? type.NullableAnnotation
            : nullable ? NullableAnnotation.Annotated
            : NullableAnnotation.NotAnnotated);
    }

    private static bool HasOblivious(ITypeSymbol type) =>
        (type.IsReferenceType && type.NullableAnnotation == NullableAnnotation.None)
        || (type is IArrayTypeSymbol && HasOblivious(((IArrayTypeSymbol)type).ElementType))
        || (type is INamedTypeSymbol && ((INamedTypeSymbol)type).TypeArguments.Any(HasOblivious));

    private static ITypeSymbol AnnotateTuple(INamedTypeSymbol tuple, Compilation compilation)
    {
        var elements = tuple.TupleElements.Select(e => Annotate(e.Type, compilation)).ToImmutableArray();
        return compilation.CreateTupleTypeSymbol(
            elements,
            tuple.TupleElements.Select(e => e.IsExplicitlyNamedTupleElement ? e.Name : null).ToImmutableArray(),
            elementNullableAnnotations: elements.Select(t => t.NullableAnnotation).ToImmutableArray());
    }

    private static ITypeSymbol AnnotateArray(IArrayTypeSymbol array, Compilation compilation)
    {
        var element = Annotate(array.ElementType, compilation);
        return compilation.CreateArrayTypeSymbol(element, array.Rank, element.NullableAnnotation);
    }

    private static ITypeSymbol AnnotateGeneric(INamedTypeSymbol type, Compilation compilation)
    {
        var arguments = type.TypeArguments.Select((t, i) => Annotate(t, compilation, CanBeNullable(type.TypeParameters[i]))).ToImmutableArray();
        return type.ConstructedFrom.Construct(arguments, arguments.Select(t => t.NullableAnnotation).ToImmutableArray());
    }

    // `notnull`, a non-nullable `class` constraint or a non-nullable constraint type (CS8714, CS8634, CS8631).
    private static bool CanBeNullable(ITypeParameterSymbol parameter) =>
        !parameter.HasNotNullConstraint
        && parameter.ReferenceTypeConstraintNullableAnnotation != NullableAnnotation.NotAnnotated
        && !parameter.ConstraintNullableAnnotations.Contains(NullableAnnotation.NotAnnotated);

    // The constructor's own default as a C# constant expression: typed float/double/decimal literals, NaN and the
    // infinities as constant divisions, enums as a cast of their underlying value, null as `default` for value types.
    private static string DefaultValue(IParameterSymbol parameter)
    {
        var value = parameter.ExplicitDefaultValue;
        var type = parameter.Type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            ? ((INamedTypeSymbol)parameter.Type).TypeArguments[0]
            : parameter.Type;
        return value is null ? (parameter.Type.IsReferenceType ? "null" : "default")
            : type.TypeKind == TypeKind.Enum ? $"({type.ToDisplayString(_format)})({Literal(value)})"
            : Literal(value);
    }

    private static string Literal(object value) => value switch
    {
        float => Real((float)value, Primitive(value), "F"),
        double => Real((double)value, Primitive(value), "D"),
        decimal => Primitive(value) + "M",
        _ => Primitive(value),
    };

    private static string Primitive(object value) => SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)!;

    // No culture-formatted number: a NegativeSign of U+2212 (sv-SE, fi-FI) would emit `(−1D / 0D)`.
    private static string Real(double value, string text, string suffix) =>
        double.IsNaN(value) ? $"(0{suffix} / 0{suffix})"
        : double.IsPositiveInfinity(value) ? $"(1{suffix} / 0{suffix})"
        : double.IsNegativeInfinity(value) ? $"(-1{suffix} / 0{suffix})"
        : text + suffix;
}

/// <summary>One overload pair: its accessibility, its parameter list (each with a leading comma), its constructor
/// arguments, and its parameter types for the DUCKY024 comparison (nullability ignored, as overload resolution does).</summary>
internal sealed record HelperModel(bool IsPublic, string Parameters, string Arguments, string Signature);

/// <summary>A diagnostic location without the syntax tree, which would defeat caching.</summary>
internal sealed record LocationInfo(string Path, TextSpan Span, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(Path, Span, LineSpan);
}

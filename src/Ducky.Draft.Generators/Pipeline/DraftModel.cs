using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Draft.Generators;

/// <summary>
/// One <c>[Draftable]</c> type as the Draft generator emits it (SPEC §13.3), symbol-free so the pipeline caches (GEN-06).
/// <see cref="Error"/> is the DUCKY101-104/108 descriptor (with <see cref="ErrorArgument"/>) and <see cref="Location"/> the
/// record's identifier in its own tree (so a #pragma in the user's source applies), null on a valid model: a source
/// location compares by tree and span, so it only changes with the record's own file. <see cref="NewDraft"/> and
/// <see cref="NewCreateDraft"/> are true when the generated member hides an accessible non-generic inherited one
/// (<c>new</c>). The other members are filled in either way.
/// </summary>
internal sealed record DraftModel(
    string TypeName,
    string DisplayName,
    string? Namespace,
    EquatableArray<string> Containers,
    EquatableArray<MemberModel> Members,
    bool HasBuild,
    bool NewDraft,
    bool NewCreateDraft,
    EquatableArray<string> DiagnosticIds,
    DiagnosticDescriptor? Error,
    string? ErrorArgument,
    Location? Location)
{
    // Generated code names user types fully qualified, with `?` on annotated reference types.
    private static readonly SymbolDisplayFormat _format =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    private static readonly SymbolDisplayFormat _namespaceFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted);

    // The member names the generator adds to the record (DUCKY104). Constants: a static initializer runs before Stryker
    // activates a mutant, so a mutated literal there would only survive.
    private const string DraftName = "Draft";
    private const string ProduceName = "Produce";
    private const string CreateDraftName = "CreateDraft";

    // The nullable flow attributes the generator mirrors (GEN-06).
    private const string MaybeNullName = "System.Diagnostics.CodeAnalysis.MaybeNullAttribute";
    private const string DisallowNullName = "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

    // Matched by full name like the compiler does, so a polyfill counts.
    private const string ExperimentalName = "System.Diagnostics.CodeAnalysis.ExperimentalAttribute";

    private static readonly ImmutableArray<string> _generatedNames = [DraftName, ProduceName, CreateDraftName];

    public static DraftModel Create(GeneratorAttributeSyntaxContext context)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        var compilation = context.SemanticModel.Compilation;
        var obsolete = compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
        var (error, argument) = Validate(type, compilation, obsolete);
        var members = Mirrored(type, compilation, obsolete);

        // An [Obsolete] DiagnosticId or an [Experimental] id replaces CS0618 for the members generated code reads.
        var diagnosticIds = members
            .SelectMany(p => IdsOf(p, obsolete))
            .Distinct()
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .ToImmutableArray();

        var objectMembers = compilation.GetSpecialType(SpecialType.System_Object).GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .Select(m => m.Name)
            .ToImmutableHashSet();

        return new DraftModel(
            type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            type.ToDisplayString(),
            type.ContainingNamespace.IsGlobalNamespace ? null : type.ContainingNamespace.ToDisplayString(_namespaceFormat),
            new EquatableArray<string>(Nesting(type).Select(Declaration).Reverse().ToImmutableArray()),
            new EquatableArray<MemberModel>(members.Select(p => Member(p, type, compilation, objectMembers, obsolete)).ToImmutableArray()),
            BaseTypes(type).Any(t => !t.GetMembers("Build").IsEmpty),
            Inherited(type, DraftName, compilation).Any(m => m is INamedTypeSymbol { Arity: 0 } or IMethodSymbol { Arity: 0 }),
            Inherited(type, CreateDraftName, compilation).Any(m => m is IMethodSymbol { Parameters.IsEmpty: true, Arity: 0 }),
            new EquatableArray<string>(diagnosticIds),
            error,
            argument,
            error is null ? null : ((TypeDeclarationSyntax)context.TargetNode).Identifier.GetLocation());
    }

    // The shape predicate (SPEC §13.3): a draft is emitted exactly when this returns no descriptor.
    private static (DiagnosticDescriptor? Error, string? Argument) Validate(INamedTypeSymbol type, Compilation compilation, INamedTypeSymbol? obsolete)
    {
        var notPartial = Nesting(type).Skip(1).FirstOrDefault(t => !IsPartial(t));
        var clash = _generatedNames.FirstOrDefault(n => n == type.Name || !type.GetMembers(n).IsEmpty || IsInheritedClash(type, n, compilation));
        DiagnosticDescriptor? error =
            !type.IsRecord || type.TypeKind != TypeKind.Class || !IsPartial(type) || Nesting(type).Any(t => t.IsFileLocal) ? DraftDiagnostics.NotPartialRecord
            : type.IsGenericType ? DraftDiagnostics.Generic // IsGenericType counts containing types
            : notPartial is not null ? DraftDiagnostics.ContainerNotPartial
            : clash is not null ? DraftDiagnostics.NameClash
            : IsErrorObsolete(type, obsolete) ? DraftDiagnostics.ErrorObsolete
            : null;
        return (error, error == DraftDiagnostics.ContainerNotPartial ? notPartial!.ToDisplayString() : clash);
    }

    // An inherited member named like a generated one is hidden by it, and a mirrored property would be read through it
    // (CS0428, CS0542). An inherited method is overloaded, or hidden with `new` like a nested type Draft (§13.3).
    private static bool IsInheritedClash(INamedTypeSymbol type, string name, Compilation compilation) =>
        Inherited(type, name, compilation).Any(m => m is not IMethodSymbol && !(name == DraftName && m is ITypeSymbol));

    // The base types' members named `name` that the record can see: only those are hidden by a generated member.
    private static IEnumerable<ISymbol> Inherited(INamedTypeSymbol type, string name, Compilation compilation) =>
        BaseTypes(type).Skip(1).SelectMany(b => b.GetMembers(name)).Where(m => compilation.IsSymbolAccessibleWithin(m, type));

    // A member named with a prefix of generated code's private names would collide with them (CS0102, §13.3).
    private static bool IsReserved(string name) =>
        name.StartsWith("__x_", System.StringComparison.Ordinal)
        || name.StartsWith("__m_", System.StringComparison.Ordinal)
        || name.StartsWith("__s_", System.StringComparison.Ordinal);

    // Generated code is not unsafe (CS0214).
    private static bool HasPointer(ITypeSymbol type) => type switch
    {
        IPointerTypeSymbol or IFunctionPointerTypeSymbol => true,
        IArrayTypeSymbol => HasPointer(((IArrayTypeSymbol)type).ElementType),
        INamedTypeSymbol => Nesting((INamedTypeSymbol)type).Any(t => t.TypeArguments.Any(HasPointer)),
        _ => false,
    };

    // One declaration is enough: every part of a type declared in several parts is partial (CS0260).
    private static bool IsPartial(INamedTypeSymbol type) =>
        ((TypeDeclarationSyntax)type.DeclaringSyntaxReferences[0].GetSyntax()).Modifiers.Any(SyntaxKind.PartialKeyword);

    // The `partial` declaration line of a containing type or of the record itself.
    private static string Declaration(INamedTypeSymbol type)
    {
        var kind = type.TypeKind == TypeKind.Struct ? "struct" : type.TypeKind == TypeKind.Interface ? "interface" : "class";
        return $"partial {(type.IsRecord ? "record " : "")}{kind} {Identifier(type.Name)}";
    }

    // A lower-case ASCII type name is written verbatim: `@inc` declares `inc` without another CS8981 in generated code.
    private static string Identifier(string name) =>
        name.All(c => c is >= 'a' and <= 'z') ? "@" + name : IdentifierSanitizer.EscapeKeyword(name);

    // The instance properties the draft mirrors: declared public, internal or protected internal, readable from the record,
    // not error-obsolete (CS0619 can't be suppressed), with an id a #pragma can name, not named with a private prefix of
    // generated code, and of a type with no pointer that names no unbound, error-obsolete or (to the assembly)
    // inaccessible type; an inherited one only when no more derived member hides its name. EqualityContract is protected
    // or private, so it never qualifies. A member left out keeps its value through `with`.
    private static List<IPropertySymbol> Mirrored(INamedTypeSymbol type, Compilation compilation, INamedTypeSymbol? obsolete)
    {
        var hidden = new HashSet<string>();
        var members = new List<IPropertySymbol>();
        foreach (var declaring in BaseTypes(type))
        {
            members.AddRange(declaring.GetMembers().OfType<IPropertySymbol>().Where(p =>
                !hidden.Contains(p.Name)
                && !p.IsStatic
                && !p.IsIndexer
                && !IsReserved(p.Name)
                && !HasPointer(p.Type)
                && IsAccessible(p.GetMethod, type, compilation, obsolete)
                && !Overridden(p, q => q.OverriddenProperty).Any(q => IsErrorObsolete(q, obsolete))
                && Parts(p.Type).All(t => t.TypeKind != TypeKind.Error && compilation.IsSymbolAccessibleWithin(t, compilation.Assembly) && !IsErrorObsolete(t, obsolete))
                && IdsOf(p, obsolete).All(IsPragmaSafe)));
            hidden.UnionWith(declaring.GetMembers().Select(m => m.Name));
        }

        return members;
    }

    private static MemberModel Member(IPropertySymbol property, INamedTypeSymbol type, Compilation compilation, ImmutableHashSet<string> objectMembers, INamedTypeSymbol? obsolete)
    {
        // The minimum of the getter's and its type's accessibility (the getter, not the property: `public int X { internal get; }`
        // must not be readable through the draft); protected internal is internal in the sealed draft.
        var isPublic = property.GetMethod!.DeclaredAccessibility == Accessibility.Public && Parts(property.Type).All(t => t.DeclaredAccessibility == Accessibility.Public);
        var writable = !property.Type.IsRefLikeType && IsAccessible(property.SetMethod, type, compilation, obsolete);

        // GEN-06: a [MaybeNull] read may be null, so the draft member is annotated; a [DisallowNull] write may not, so the
        // draft member keeps the attribute. Either way Build forgives the draft value it passes back.
        var maybeNull = !property.Type.IsValueType && HasAttribute(property, MaybeNullName);
        var disallowNull = HasAttribute(property, DisallowNullName);
        var memberType = Annotate(property.Type, compilation);
        return new MemberModel(
            property.Name,
            (maybeNull ? memberType.WithNullableAnnotation(NullableAnnotation.Annotated) : memberType).ToDisplayString(_format),
            isPublic,
            objectMembers.Contains(property.Name),
            !writable ? null : isPublic && property.SetMethod!.DeclaredAccessibility != Accessibility.Public ? "internal " : "",
            disallowNull,
            maybeNull || disallowNull);
    }

    // Declared public, internal or protected internal, and reachable from the record (not internal to another assembly).
    // An error-obsolete accessor can't be called either (CS0619): its property is left out, or get-only.
    private static bool IsAccessible(IMethodSymbol? accessor, INamedTypeSymbol type, Compilation compilation, INamedTypeSymbol? obsolete) =>
        accessor is not null
        && accessor.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal or Accessibility.ProtectedOrInternal
        && !Overridden(accessor, m => m.OverriddenMethod).Any(m => IsErrorObsolete(m, obsolete))
        && compilation.IsSymbolAccessibleWithin(accessor, type);

    // The type itself, then each containing type outwards.
    private static List<INamedTypeSymbol> Nesting(INamedTypeSymbol type)
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

    // The [Obsolete] DiagnosticIds and [Experimental] ids that apply where generated code reads a property: on each type
    // its type names, on the property, its accessors and each member it overrides, and on the assemblies and modules
    // declaring them. DiagnosticId = "" falls back to CS0618, and a bare pragma would disable every warning.
    private static IEnumerable<string> IdsOf(IPropertySymbol property, INamedTypeSymbol? obsolete) =>
        Parts(property.Type)
            .Concat(Overridden(property, q => q.OverriddenProperty).SelectMany(q => new ISymbol?[] { q, q.GetMethod, q.SetMethod }))
            .SelectMany(s => new[] { s, s?.ContainingAssembly, s?.ContainingModule })
            .OfType<ISymbol>()
            .SelectMany(s => DiagnosticIdsOf(s, obsolete))
            .OfType<string>()
            .Where(id => id.Length > 0);

    private static IEnumerable<object?> DiagnosticIdsOf(ISymbol symbol, INamedTypeSymbol? obsolete) =>
        symbol.GetAttributes().SelectMany(a =>
            SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) ? a.NamedArguments.Where(n => n.Key == "DiagnosticId").Select(n => n.Value.Value)
            : $"{a.AttributeClass}" == ExperimentalName ? a.ConstructorArguments.Select(c => c.Value)
            : []);

    // C# binds a member access on an override to the original virtual declaration, whose [Obsolete] and [Experimental]
    // then apply to generated code: the member, then each member it overrides.
    private static IEnumerable<T> Overridden<T>(T symbol, System.Func<T, T?> overridden)
        where T : class
    {
        for (T? s = symbol; s is not null; s = overridden(s))
        {
            yield return s;
        }
    }

    // Every named type a type mentions: itself, its containing types, their type arguments and array elements.
    private static IEnumerable<INamedTypeSymbol> Parts(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol => Parts(((IArrayTypeSymbol)type).ElementType),
        INamedTypeSymbol => Nesting((INamedTypeSymbol)type).SelectMany(t => new[] { t }.Concat(t.TypeArguments.SelectMany(Parts))),
        _ => [],
    };

    // CS0619 can't be suppressed, so generated code never names an [Obsolete(…, error: true)] symbol.
    private static bool IsErrorObsolete(ISymbol symbol, INamedTypeSymbol? obsolete) =>
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) && a.ConstructorArguments.Any(arg => arg.Value is true));

    // GEN-06: an oblivious reference type (user source under #nullable disable) counts as annotated at every position, so
    // the draft member of `string Name` is `string?` in the #nullable enable file, except as the argument of a type
    // parameter that can't be nullable (Dictionary<string, object> keeps `string`, no CS8714). A type with no oblivious
    // position is returned as is, and a tuple is rebuilt as a tuple (Construct would drop its element names).
    private static ITypeSymbol Annotate(ITypeSymbol type, Compilation compilation, bool nullable = true)
    {
        if (!HasOblivious(type))
        {
            return type;
        }

        var rebuilt = type switch
        {
            IArrayTypeSymbol => AnnotateArray((IArrayTypeSymbol)type, compilation),
            INamedTypeSymbol { IsTupleType: true, TupleElements.Length: >= 2 } => AnnotateTuple((INamedTypeSymbol)type, compilation),
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

    // The compiler matches nullable flow attributes by full name (a polyfill counts) and, for a read or write through the
    // property, only on the property itself: an accessor's [return: MaybeNull] or [param: DisallowNull] doesn't apply.
    // A symbol formats as its display string, a null AttributeClass as "".
    private static bool HasAttribute(IPropertySymbol property, string name) =>
        property.GetAttributes().Any(a => $"{a.AttributeClass}" == name);

    // The ids generated code disables: an ASCII identifier, as a #pragma reads it, that is not a preprocessor keyword
    // (a pragma naming `default` is CS1072). The compiler rejects any other [Experimental] id in source (CS9211),
    // but not in metadata, nor any [Obsolete] DiagnosticId.
    internal static bool IsPragmaSafe(string id) =>
        id.Select((c, i) => c is '_' or (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') || (i > 0 && c is >= '0' and <= '9')).All(safe => safe)
        && SyntaxFacts.GetPreprocessorKeywordKind(id) == SyntaxKind.None;
}

/// <summary>
/// One mirrored property: its verbatim name, its type as generated code writes it, whether the draft member is public
/// (else internal), whether it hides an <c>object</c> member (<c>new</c>), and its setter's modifier (<c>""</c> or
/// <c>"internal "</c>), null for a get-only member, whether the draft member carries
/// <c>[DisallowNull]</c>, and whether Build passes its value back with <c>!</c> (a <c>[MaybeNull]</c> or <c>[DisallowNull]</c> property).
/// </summary>
internal sealed record MemberModel(string Name, string Type, bool IsPublic, bool IsNew, string? Setter, bool DisallowNull, bool NullForgiving);

using System.Linq;
using Microsoft.CodeAnalysis;

namespace Ducky.Draft.Generators;

/// <summary>
/// Symbol-based member type mapping (SPEC §13.3 table): <c>OriginalDefinition</c> compared with
/// <see cref="SymbolEqualityComparer"/> against <c>GetTypeByMetadataName</c> symbols, never display strings. A type
/// <c>C</c> maps to <c>C.Draft</c> when it is <c>[Draftable]</c> and either passes the generator's own shape predicate
/// (<see cref="DraftModel.Validate"/>, same compilation) or has a compatible nested <c>Draft</c> (another assembly).
/// </summary>
internal sealed class DraftMapping
{
    private const string Runtime = "global::Ducky.Draft.";

    private readonly Compilation _compilation;
    private readonly INamedTypeSymbol? _obsolete;
    private readonly INamedTypeSymbol? _list;
    private readonly INamedTypeSymbol? _array;
    private readonly INamedTypeSymbol? _dictionary;
    private readonly INamedTypeSymbol? _hashSet;
    private readonly INamedTypeSymbol? _sortedSet;
    private readonly INamedTypeSymbol? _mutableList;
    private readonly INamedTypeSymbol? _mutableDictionary;
    private readonly INamedTypeSymbol? _draftable;
    private readonly INamedTypeSymbol? _draftInterface;

    public DraftMapping(Compilation compilation, INamedTypeSymbol? obsolete)
    {
        _compilation = compilation;
        _obsolete = obsolete;
        _list = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
        _array = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableArray`1");
        _dictionary = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableDictionary`2");
        _hashSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet`1");
        _sortedSet = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableSortedSet`1");
        _mutableList = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
        _mutableDictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        _draftable = compilation.GetTypeByMetadataName("Ducky.Draft.DraftableAttribute");
        _draftInterface = compilation.GetTypeByMetadataName("Ducky.Draft.IDraft`1");
    }

    /// <summary>A mutable collection member (<c>List&lt;T&gt;</c>, <c>T[]</c>, <c>Dictionary&lt;K,V&gt;</c>) is plain and gets DUCKY105.</summary>
    public bool IsMutable(ITypeSymbol type) =>
        type is IArrayTypeSymbol || Is(type, _mutableList) || Is(type, _mutableDictionary);

    /// <summary>
    /// Whether a same-compilation type is drafted: <c>[Draftable]</c> and passing the shape predicate. A derived record of
    /// such a base hides the base's generated <c>Draft</c> and <c>CreateDraft</c>, which the generator can't see.
    /// </summary>
    public bool IsDraftedHere(INamedTypeSymbol type) =>
        HasDraftable(type) && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly)
        && DraftModel.Validate(type, _compilation, _obsolete).Error is null;

    /// <summary>
    /// The draft of a writable member of declared type <paramref name="declared"/> whose type, as generated code writes it,
    /// is <paramref name="annotated"/>; null for a plain member. <c>Unmapped</c> is a <c>[Draftable]</c> type of another
    /// assembly that would have been drafted but has no compatible <c>Draft</c> (DUCKY107).
    /// </summary>
    public (DraftMember? Draft, INamedTypeSymbol? Unmapped) Map(ITypeSymbol declared, ITypeSymbol annotated)
    {
        // A nullable ImmutableArray<T> is Nullable<ImmutableArray<T>>: its draft is the array's, nullable.
        var nullableValue = annotated.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
        var type = nullableValue ? ((INamedTypeSymbol)annotated).TypeArguments[0] : annotated;
        var source = nullableValue ? ((INamedTypeSymbol)declared).TypeArguments[0] : declared;
        var nullable = nullableValue || annotated.NullableAnnotation == NullableAnnotation.Annotated;
        return type switch
        {
            INamedTypeSymbol when Is(type, _list) || Is(type, _array) =>
                List((INamedTypeSymbol)source, (INamedTypeSymbol)type, nullable, Is(type, _array)),
            INamedTypeSymbol when Is(type, _dictionary) => Dictionary((INamedTypeSymbol)source, (INamedTypeSymbol)type, nullable),
            INamedTypeSymbol when Is(type, _hashSet) || Is(type, _sortedSet) => (Set((INamedTypeSymbol)type, nullable, Is(type, _hashSet)), null),
            INamedTypeSymbol => Nested((INamedTypeSymbol)type, nullable),
            _ => (null, null),
        };
    }

    // ImmutableList<T> / ImmutableArray<T>: element drafts for a draftable element that is not nullable-annotated (an
    // oblivious one keeps them, GEN-06); an array's identity is its underlying array (compared by value).
    private (DraftMember? Draft, INamedTypeSymbol? Unmapped) List(INamedTypeSymbol source, INamedTypeSymbol type, bool nullable, bool isArray)
    {
        var (element, unmapped) = Element(source.TypeArguments[0]);
        var build = isArray ? "{0}.BuildArray()" : "{0}.BuildList()";
        var draft = element is null
            ? new DraftMember($"{Runtime}ListDraft<{Name(type.TypeArguments[0])}>", nullable, isArray, "{0}", build, "{0}?.IsDirty")
            : new DraftMember($"{Runtime}ListDraft<{element}, {element}.Draft>", nullable, isArray, ElementFactory(element), build, "{0}?.IsDirty");
        return (draft, unmapped);
    }

    // ImmutableDictionary<K,V>: element drafts for a draftable value that is not nullable-annotated.
    private (DraftMember? Draft, INamedTypeSymbol? Unmapped) Dictionary(INamedTypeSymbol source, INamedTypeSymbol type, bool nullable)
    {
        var key = Name(type.TypeArguments[0]);
        var (value, unmapped) = Element(source.TypeArguments[1]);
        var draft = value is null
            ? new DraftMember($"{Runtime}DictionaryDraft<{key}, {Name(type.TypeArguments[1])}>", nullable, false, "{0}", "{0}.Build()", "{0}?.IsDirty")
            : new DraftMember($"{Runtime}DictionaryDraft<{key}, {value}, {value}.Draft>", nullable, false, ElementFactory(value), "{0}.Build()", "{0}?.IsDirty");
        return (draft, unmapped);
    }

    private static DraftMember Set(INamedTypeSymbol type, bool nullable, bool isHashSet) =>
        new($"{Runtime}SetDraft<{Name(type.TypeArguments[0])}>", nullable, false, "{0}", isHashSet ? "{0}.BuildHashSet()" : "{0}.BuildSortedSet()", "{0}?.IsDirty");

    // A draftable record member: its nested Draft, whose IDraft members are explicit.
    private (DraftMember? Draft, INamedTypeSymbol? Unmapped) Nested(INamedTypeSymbol type, bool nullable)
    {
        var named = (INamedTypeSymbol)type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        var (drafted, unmapped) = Draftable(named);
        var name = Name(named);
        var draft = drafted
            ? new DraftMember($"{name}.Draft", nullable, false, $"new {name}.Draft({{0}})", $"(({Runtime}IDraft<{name}>){{0}}).Build()", $"(({Runtime}IDraft<{name}>?){{0}})?.IsDirty")
            : null;
        return (draft, unmapped);
    }

    // The element type generated code drafts (written without `?`), or null for a plain element; a nullable-annotated
    // element is never drafted, nor checked.
    private (string? Element, INamedTypeSymbol? Unmapped) Element(ITypeSymbol element)
    {
        var (drafted, unmapped) = element.NullableAnnotation == NullableAnnotation.Annotated ? (false, null) : Draftable(element);
        return (drafted ? Name(element.WithNullableAnnotation(NullableAnnotation.NotAnnotated)) : null, unmapped);
    }

    private static string ElementFactory(string element) => $"new({{0}}, static x => new {element}.Draft(x))";

    // [Draftable], then the shape predicate in this compilation, or a compatible nested Draft in another assembly.
    private (bool Drafted, INamedTypeSymbol? Unmapped) Draftable(ITypeSymbol type)
    {
        if (!HasDraftable(type))
        {
            return (false, null);
        }

        if (SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, _compilation.Assembly))
        {
            return (DraftModel.Validate((INamedTypeSymbol)type, _compilation, _obsolete).Error is null, null);
        }

        var compatible = HasCompatibleDraft((INamedTypeSymbol)type);
        return (compatible, compatible ? null : (INamedTypeSymbol)type);
    }

    // [Draftable] is not inherited (Inherited = false), so only the type's own attributes count.
    private bool HasDraftable(ITypeSymbol type) =>
        type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, _draftable));

    // The parent's generated code calls `new C.Draft(x)` and builds through IDraft<C>: a nested public non-generic,
    // non-abstract class Draft (ListDraft's TDraft : class, no Nullable<Draft> field) implementing IDraft<C> with a public
    // constructor taking exactly one C by value. Neither may be error-obsolete (CS0619) or carry an [Obsolete] DiagnosticId
    // or an [Experimental] id, which the generated CS0618 pragma doesn't cover.
    private bool HasCompatibleDraft(INamedTypeSymbol type) =>
        type.GetTypeMembers("Draft", 0).Any(d =>
            d.DeclaredAccessibility == Accessibility.Public
            && d.TypeKind == TypeKind.Class
            && !d.IsAbstract
            && IsCleanlyNamed(d)
            && d.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, _draftInterface) && SymbolEqualityComparer.Default.Equals(i.TypeArguments[0], type))
            && d.InstanceConstructors.Any(c =>
                c.DeclaredAccessibility == Accessibility.Public
                && c.Parameters.Length == 1
                && c.Parameters[0].RefKind == RefKind.None
                && SymbolEqualityComparer.Default.Equals(c.Parameters[0].Type, type)
                && IsCleanlyNamed(c)));

    private static bool Is(ITypeSymbol type, INamedTypeSymbol? definition) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, definition);

    private static string Name(ITypeSymbol type) => type.ToDisplayString(DraftModel.Format);

    private bool IsCleanlyNamed(ISymbol symbol) =>
        !DraftModel.IsErrorObsolete(symbol, _obsolete) && !DraftModel.HasDiagnosticId(symbol, _obsolete);
}

/// <summary>
/// How a writable member is drafted (SPEC §13.3): the draft type generated code writes (without <c>?</c>), whether the
/// member is nullable (its getter returns null while the original is null and nothing was assigned), whether its no-op
/// identity is compared by value (<c>ImmutableArray&lt;T&gt;</c>: the same underlying array) instead of by reference, and
/// the format strings creating the draft from a value, building it and reading its dirty state from a field (<c>{0}</c>).
/// </summary>
internal sealed record DraftMember(string Type, bool Nullable, bool ByValue, string Create, string Build, string Dirty);

/// <summary>
/// A DUCKY105 or DUCKY107 report on a mirrored member: its location, the member and the type it names. The location keeps
/// its tree (a #pragma in the user's source applies) and compares by tree and span; it never enters the
/// <see cref="DraftModel"/>, so moving the member re-reports it without emitting again.
/// </summary>
internal sealed record MemberDiagnostic(DiagnosticDescriptor Descriptor, Location Location, string Member, string Type);

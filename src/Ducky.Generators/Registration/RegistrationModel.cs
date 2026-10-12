using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Ducky.Generators.Registration;

/// <summary>A [Persist] or [Prerender] (<see cref="Attribute"/>) on a type, symbol-free, for DUCKY022 at <see cref="Location"/>.</summary>
internal sealed record AttributeModel(string Attribute, string Display, bool IsSlice, Location Location)
{
    public static AttributeModel From(GeneratorAttributeSyntaxContext context, string attribute)
    {
        var type = (INamedTypeSymbol)context.TargetSymbol;
        return new(attribute, type.ToDisplayString(),
            RegistrationModel.Derives(type, context.SemanticModel.Compilation.GetTypeByMetadataName("Ducky.Slice")),
            ((TypeDeclarationSyntax)context.TargetNode).Identifier.GetLocation());
    }
}

/// <summary>The compilation-wide inputs of GEN-02: the sanitized assembly name and which gated methods resolve.</summary>
internal sealed record RegistrationGates(string Assembly, bool Persist, bool Prerender)
{
    public const string PersistMethod = "Ducky.Blazor.DuckyBlazorBuilderExtensions.PersistAttributeDefaults<TSlice>(DuckyBuilder, Action<PersistOptions>)";
    public const string PrerenderMethod = "Ducky.Blazor.DuckyBlazorBuilderExtensions.Prerender<TSlice>(DuckyBuilder)";

    public static RegistrationGates From(Compilation compilation) => new(
        AssemblyNameSanitizer.Sanitize(compilation.AssemblyName),
        Resolves(compilation, "Public PersistAttributeDefaults`1(Ducky.DuckyBuilder, System.Action<Ducky.Blazor.PersistOptions>)"),
        Resolves(compilation, "Public Prerender`1(Ducky.DuckyBuilder)"));

    // Gated on the exact method symbol (accessibility, name, arity, parameter types), never on the extension type: no
    // reference, or a skewed or older Ducky.Blazor without the method, is DUCKY022 instead of CS0117 in generated code.
    private static bool Resolves(Compilation compilation, string signature) =>
        compilation.GetTypeByMetadataName("Ducky.Blazor.DuckyBlazorBuilderExtensions")?.GetMembers().OfType<IMethodSymbol>()
            .Any(m => $"{m.DeclaredAccessibility} {m.Name}`{m.Arity}({string.Join(", ", m.Parameters.Select(p => p.Type.ToDisplayString()))})" == signature) == true;
}

/// <summary>
/// One slice or effect GEN-02 registers (<see cref="Excluded"/> null) or reports as DUCKY023 (the reason), symbol-free.
/// <see cref="Suppressed"/> holds the DiagnosticIds of its [Obsolete] and [Experimental] attributes and those of its
/// containing types, which the generated registration has to suppress besides CS0612/CS0618 (GEN-06).
/// <see cref="Location"/> keeps its tree: it compares by tree reference and span, so the model stays equal while its file
/// is unchanged, and a diagnostic lands in that exact tree (never re-found by path: trees may share one, INV-25).
/// </summary>
internal sealed record RegistrationModel(string Type, string Display, bool IsSlice, string? Excluded, Location Location, EquatableArray<string> Suppressed = default)
{
    public const string FileLocal = "it is file-local, so the generated registration can't reference it";
    public const string NoConstructor = "a slice needs a public parameterless constructor that new() can call (one marked [SetsRequiredMembers] when the slice has required members)";

    /// <summary>The <see cref="Classify"/> of a class with a base list, the generator's syntax step.</summary>
    public static RegistrationModel? From(GeneratorSyntaxContext context, CancellationToken cancellationToken) =>
        Classify((INamedTypeSymbol)context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken)!, context.SemanticModel.Compilation);

    /// <summary>
    /// Classifies <paramref name="type"/>: null unless it is a non-abstract, non-generic slice or effect that generated
    /// code may reference (accessible from the assembly, neither it nor a containing type error-obsolete), and an effect
    /// without a public constructor is skipped (SPEC §16.1). DUCKY016 counts the registered ones (no reason).
    /// </summary>
    public static RegistrationModel? Classify(INamedTypeSymbol type, Compilation compilation)
    {
        var isSlice = Derives(type, compilation.GetTypeByMetadataName("Ducky.Slice"));
        var isEffect = Derives(type, compilation.GetTypeByMetadataName("Ducky.Effect"));
        var containing = Chain(type, t => t.ContainingType).ToList();
        var obsolete = compilation.GetTypeByMetadataName("System.ObsoleteAttribute");
        if (!(isSlice || isEffect) || type.IsAbstract || type.IsGenericType ||
            containing.Any(t => t.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) && a.ConstructorArguments.Any(c => true.Equals(c.Value)))))
        {
            return null;
        }

        var name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var display = type.ToDisplayString();
        var location = type.Locations[0];

        // Accessibility doesn't model `file`, so the outermost type's IsFileLocal is checked first.
        if (containing[containing.Count - 1].IsFileLocal)
        {
            return new(name, display, isSlice, FileLocal, location);
        }

        var constructors = type.InstanceConstructors.Where(c => c.DeclaredAccessibility == Accessibility.Public).ToList();
        if (!compilation.IsSymbolAccessibleWithin(type, compilation.Assembly) || !(isSlice || constructors.Count > 0))
        {
            return null;
        }

        // new() ignores a constructor that leaves a required member unset (CS9040).
        var required = Chain(type, t => t.BaseType).SelectMany(t => t.GetMembers()).Any(m => m is IPropertySymbol { IsRequired: true } or IFieldSymbol { IsRequired: true });
        var setsRequired = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.SetsRequiredMembersAttribute");
        if (isSlice && !constructors.Any(c => c.Parameters.IsEmpty && (!required || Has(c, setsRequired))))
        {
            return new(name, display, isSlice, NoConstructor, location);
        }

        // [Obsolete(DiagnosticId = "X")] reports X instead of CS0618 (an empty id keeps CS0618), [Experimental("X")] X.
        var experimental = compilation.GetTypeByMetadataName("System.Diagnostics.CodeAnalysis.ExperimentalAttribute");
        var suppressed = containing.SelectMany(t => t.GetAttributes()).SelectMany(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, experimental) ? a.ConstructorArguments.Select(c => c.Value) :
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, obsolete) ? a.NamedArguments.Where(n => n.Key == "DiagnosticId").Select(n => n.Value.Value) :
                [])
            .OfType<string>().Where(id => id.Length > 0);
        return new(name, display, isSlice, null, location, new([.. suppressed]));
    }

    /// <summary>Whether <paramref name="type"/> derives from <paramref name="baseType"/>.</summary>
    public static bool Derives(INamedTypeSymbol type, INamedTypeSymbol? baseType) =>
        Chain(type.BaseType, t => t.BaseType).Contains(baseType, SymbolEqualityComparer.Default);

    private static bool Has(ISymbol symbol, INamedTypeSymbol? attribute) =>
        symbol.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute));

    private static IEnumerable<INamedTypeSymbol> Chain(INamedTypeSymbol? type, Func<INamedTypeSymbol, INamedTypeSymbol?> next)
    {
        for (; type is not null; type = next(type))
        {
            yield return type;
        }
    }
}

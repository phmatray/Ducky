using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators;

/// <summary>
/// ADR-0050's reading of a C# union, shared by the union-first types (ADR-0051) and the dispatch helpers: a union implements
/// <c>IUnion</c>, and its cases are the parameter types of its public single-parameter instance constructors.
/// </summary>
internal static class UnionCases
{
    /// <summary>The <c>IUnion</c> interface every C# union implements; null on a runtime without unions.</summary>
    public static INamedTypeSymbol? Resolve(Compilation compilation) =>
        compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IUnion");

    /// <summary>
    /// The cases of a <c>[DuckyAction]</c> union that are actions of this compilation: declared in it (a BCL or referenced
    /// case is not), and not carrying their own <paramref name="attribute"/>, which acts for them.
    /// </summary>
    public static IEnumerable<INamedTypeSymbol> OwnActions(ITypeSymbol union, Compilation compilation, INamedTypeSymbol? iunion, INamedTypeSymbol? attribute) =>
        Of(union, iunion).Select(c => c.Case).OfType<INamedTypeSymbol>()
            .Where(c => SymbolEqualityComparer.Default.Equals(c.ContainingAssembly, compilation.Assembly)
                && !c.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attribute)));

    public static bool IsUnion(ITypeSymbol type, INamedTypeSymbol? iunion) =>
        type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iunion));

    /// <summary>
    /// The cases of <paramref name="union"/>, each with the expression that wraps a value of it into
    /// <paramref name="union"/> and the unions and constructors that expression names (<c>Path</c>). A case that is itself
    /// a union is unwrapped to its own cases, as Dispatch unwraps (#304); the first path to a case wins, and unions that
    /// contain each other end the walk.
    /// </summary>
    public static List<(ITypeSymbol Case, Func<string, string> Wrap, ImmutableArray<ISymbol> Path)> Of(ITypeSymbol union, INamedTypeSymbol? iunion)
    {
        var cases = new List<(ITypeSymbol, Func<string, string>, ImmutableArray<ISymbol>)>();
        Add(union, static value => value, [], cases, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), iunion);
        return cases;
    }

    /// <summary>
    /// Whether <paramref name="method"/> is a <c>Reduce</c> override (of an override…) of <c>Slice&lt;TState, TAction&gt;.Reduce</c>
    /// (<paramref name="unionSlice"/>), the union slice's reducer that DUCKY001–003 and DUCKY011 analyse (#324).
    /// </summary>
    public static bool IsReduce(IMethodSymbol method, INamedTypeSymbol? unionSlice)
    {
        if (method.Name != "Reduce")
        {
            return false;
        }

        var declared = method;
        while (declared.OverriddenMethod is { } overridden)
        {
            declared = overridden;
        }

        return SymbolEqualityComparer.Default.Equals(declared.ContainingType.OriginalDefinition, unionSlice);
    }

    // seen holds the unions entered as well as the cases.
    private static void Add(
        ITypeSymbol union, Func<string, string> wrap, ImmutableArray<ISymbol> path, List<(ITypeSymbol, Func<string, string>, ImmutableArray<ISymbol>)> cases,
        HashSet<ITypeSymbol> seen, INamedTypeSymbol? iunion)
    {
        if (!seen.Add(union))
        {
            return;
        }

        var name = union.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var constructors = ((INamedTypeSymbol)union).InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 1);
        foreach (var constructor in constructors)
        {
            var @case = constructor.Parameters[0].Type;
            Func<string, string> wrapped = value => wrap($"new {name}({value})");
            var step = path.Add(union).Add(constructor);
            if (IsUnion(@case, iunion))
            {
                Add(@case, wrapped, step, cases, seen, iunion);
            }
            else if (seen.Add(@case))
            {
                cases.Add((@case, wrapped, step));
            }
        }
    }
}

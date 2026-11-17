using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Ducky.Generators;

/// <summary>
/// ADR-0050's reading of a C# union, shared by the union-first types (ADR-0051) and the dispatch helpers: a union implements
/// <c>IUnion</c>, and its cases are the parameter types of its public single-parameter instance constructors.
/// </summary>
internal static class UnionCases
{
    public static bool IsUnion(ITypeSymbol type, INamedTypeSymbol? iunion) =>
        type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iunion));

    /// <summary>
    /// The cases of <paramref name="union"/>, each with the expression that wraps a value of it into
    /// <paramref name="union"/>. A case that is itself a union is unwrapped to its own cases, as Dispatch unwraps (#304);
    /// the first path to a case wins, and unions that contain each other end the walk.
    /// </summary>
    public static List<(ITypeSymbol Case, Func<string, string> Wrap)> Of(ITypeSymbol union, INamedTypeSymbol? iunion)
    {
        var cases = new List<(ITypeSymbol, Func<string, string>)>();
        Add(union, static value => value, cases, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), iunion);
        return cases;
    }

    // seen holds the unions entered as well as the cases.
    private static void Add(ITypeSymbol union, Func<string, string> wrap, List<(ITypeSymbol, Func<string, string>)> cases, HashSet<ITypeSymbol> seen, INamedTypeSymbol? iunion)
    {
        if (!seen.Add(union))
        {
            return;
        }

        var name = union.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var parameters = ((INamedTypeSymbol)union).InstanceConstructors
            .Where(c => c.DeclaredAccessibility == Accessibility.Public && c.Parameters.Length == 1)
            .Select(c => c.Parameters[0].Type);
        foreach (var @case in parameters)
        {
            Func<string, string> wrapped = value => wrap($"new {name}({value})");
            if (IsUnion(@case, iunion))
            {
                Add(@case, wrapped, cases, seen, iunion);
            }
            else if (seen.Add(@case))
            {
                cases.Add((@case, wrapped));
            }
        }
    }
}

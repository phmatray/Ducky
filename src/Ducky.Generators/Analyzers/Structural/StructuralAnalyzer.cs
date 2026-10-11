using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Analyzers.Structural;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY005 and DUCKY011–015 (SPEC §16.2): handlers for non-concrete action types, an unchanged <c>with</c> copy of the
/// state in an <c>On&lt;T&gt;</c> reducer lambda, a <c>[Persist]</c> slice on its derived key, a concurrency key without
/// value equality, a duplicate <c>On&lt;T&gt;</c> in one constructor, and two slices sharing a state type. Generated code
/// is not analysed.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class StructuralAnalyzer : DiagnosticAnalyzer
{
    // Names a type as the runtime sees it, for DUCKY014: tuple element names are erased (ExpandValueTuple drops them).
    // ponytail: dynamic nested in a type argument (List<dynamic> vs List<object>) still reads as distinct; normalize the
    // symbol if that ever matters (a top-level dynamic is DUCKY005 anyway).
    private static readonly SymbolDisplayFormat _runtimeIdentity =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandValueTuple);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => StructuralDiagnostics.All;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // An unresolved type is null and never matches, so a compilation without Ducky needs no branch.
            var compilation = start.Compilation;
            var symbols = new Symbols(
                compilation.GetTypeByMetadataName("Ducky.Slice`1"),
                compilation.GetTypeByMetadataName("Ducky.EffectGroup"),
                compilation.GetTypeByMetadataName("Ducky.Effect`1"),
                compilation.GetTypeByMetadataName("Ducky.PersistAttribute"));
            var slices = new ConcurrentBag<(INamedTypeSymbol Slice, ITypeSymbol State)>();

            start.RegisterOperationBlockStartAction(block => OnBlockStart(block, symbols));
            start.RegisterOperationAction(op => OnReturn(op, symbols), OperationKind.Return);
            start.RegisterSymbolAction(symbol => OnType(symbol, symbols, slices), SymbolKind.NamedType);
            start.RegisterCompilationEndAction(end => ReportSharedStates(end, slices));
        });
    }

    // On<T> calls: DUCKY005 and DUCKY011 anywhere, DUCKY014 among the calls of one constructor.
    private static void OnBlockStart(OperationBlockStartAnalysisContext block, Symbols symbols)
    {
        var seen = block.OwningSymbol is IMethodSymbol { MethodKind: MethodKind.Constructor }
            ? new HashSet<string>(StringComparer.Ordinal)
            : null;
        block.RegisterOperationAction(
            op =>
            {
                var invocation = (IInvocationOperation)op.Operation;
                var method = invocation.TargetMethod;
                // On<T> is the only generic method of Slice<T> and EffectGroup user code can call; an explicit base
                // constructor call is an invocation too.
                var owner = method.ContainingType.OriginalDefinition;
                var isSlice = Is(owner, symbols.Slice);
                if (!isSlice && !Is(owner, symbols.EffectGroup))
                {
                    return;
                }

                if (method.TypeArguments.IsEmpty)
                {
                    return;
                }

                var action = method.TypeArguments[0];
                var location = ((InvocationExpressionSyntax)invocation.Syntax).Expression.GetLocation();
                if (IsNonConcrete(action))
                {
                    op.ReportDiagnostic(Diagnostic.Create(StructuralDiagnostics.NonConcreteHandler, location, action.ToDisplayString()));
                }

                if (seen?.Add(action.ToDisplayString(_runtimeIdentity)) == false)
                {
                    op.ReportDiagnostic(Diagnostic.Create(StructuralDiagnostics.DuplicateHandler, location, action.ToDisplayString()));
                }

                var reducer = (invocation.Arguments[0].Value as IDelegateCreationOperation)?.Target as IAnonymousFunctionOperation;
                if (isSlice && reducer is not null)
                {
                    ReportEmptyWith(op, reducer);
                }
            },
            OperationKind.Invocation);
    }

    // DUCKY011: `s with { }` on the reducer's state parameter, nested lambdas included.
    private static void ReportEmptyWith(OperationAnalysisContext op, IAnonymousFunctionOperation reducer)
    {
        var state = reducer.Symbol.Parameters[0];
        foreach (var with in reducer.Body.Descendants().OfType<IWithOperation>())
        {
            if (with.Initializer.Initializers.IsEmpty && Is((with.Operand as IParameterReferenceOperation)?.Parameter, state))
            {
                op.ReportDiagnostic(Diagnostic.Create(StructuralDiagnostics.EmptyWith, with.Syntax.GetLocation(), with.Syntax.ToString()));
            }
        }
    }

    // DUCKY013: a value returned by an override of Effect<T>.ConcurrencyKey, converted to object from a class or array
    // type without an Equals override. An object or interface typed value has an unknown runtime type.
    private static void OnReturn(OperationAnalysisContext op, Symbols symbols)
    {
        // Pattern variables stay out of conditions with && or ||: a mutated operator would break definite assignment.
        var conversion = ((IReturnOperation)op.Operation).ReturnedValue as IConversionOperation;
        var type = conversion?.Operand.Type;
        // object has an unknown runtime type: a conditional or switch with no natural type is target-typed to it.
        if (type?.TypeKind is not (TypeKind.Class or TypeKind.Array) || type.SpecialType == SpecialType.System_Object)
        {
            return;
        }

        // A lambda in a field or property initializer belongs to that field or property, not to a method.
        var method = op.ContainingSymbol as IMethodSymbol;
        if (method is null)
        {
            return;
        }

        while (method.OverriddenMethod is not null)
        {
            method = method.OverriddenMethod;
        }

        if (method.Name == "ConcurrencyKey" && Is(method.ContainingType.OriginalDefinition, symbols.Effect) && !HasValueEquality(type) && !InNestedFunction(op.Operation))
        {
            op.ReportDiagnostic(Diagnostic.Create(StructuralDiagnostics.ConcurrencyKeyEquality, conversion!.Syntax.GetLocation(), type.ToDisplayString()));
        }
    }

    // DUCKY005 Effect<T> arm and DUCKY012, and the slices DUCKY015 compares at compilation end.
    private static void OnType(SymbolAnalysisContext context, Symbols symbols, ConcurrentBag<(INamedTypeSymbol Slice, ITypeSymbol State)> slices)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        var action = Closing(type, symbols.Effect);
        // Reported only where the non-concrete closing first appears: an effect deriving from an already reported base
        // cannot fix it. The base's own declaration (OriginalDefinition) keeps `E : Base<BaseAct>` reported.
        var inherited = type.BaseType is null ? null : Closing(type.BaseType.OriginalDefinition, symbols.Effect);
        if (action is not null && IsNonConcrete(action) && (inherited is null || !IsNonConcrete(inherited)))
        {
            context.ReportDiagnostic(Diagnostic.Create(StructuralDiagnostics.NonConcreteHandler, type.Locations[0], action.ToDisplayString()));
        }

        var state = Closing(type, symbols.Slice);
        if (state is null)
        {
            return;
        }

        if (!type.IsAbstract)
        {
            slices.Add((type, state));
        }

        var persist = type.GetAttributes().FirstOrDefault(a => Is(a.AttributeClass, symbols.Persist));
        if (persist is not null && !PinsKey(type, symbols.Slice))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                StructuralDiagnostics.DerivedPersistKey,
                persist.ApplicationSyntaxReference!.GetSyntax(context.CancellationToken).GetLocation(),
                type.ToDisplayString()));
        }
    }

    // DUCKY015: every slice whose state type another slice also has, with the others in ordinal order.
    private static void ReportSharedStates(CompilationAnalysisContext context, ConcurrentBag<(INamedTypeSymbol Slice, ITypeSymbol State)> slices)
    {
        foreach (var group in slices.GroupBy(s => s.State, SymbolEqualityComparer.Default))
        {
            var names = group.Select(s => s.Slice.ToDisplayString()).ToArray();
            foreach (var (slice, state) in group)
            {
                var name = slice.ToDisplayString();
                var others = names.Where(n => n != name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
                if (others.Length > 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        StructuralDiagnostics.SharedState, slice.Locations[0], name, state.ToDisplayString(), string.Join("', '", others)));
                }
            }
        }
    }

    private static bool Is(ISymbol? symbol, ISymbol? expected) => SymbolEqualityComparer.Default.Equals(symbol, expected);

    // Exact-type matching never hits an abstract class, an interface (abstract too), object or dynamic (object at run
    // time); a type parameter may still be closed with a concrete type.
    private static bool IsNonConcrete(ITypeSymbol type) =>
        type.IsAbstract || type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;

    // The type argument of the closed `generic` in the base chain of `type`, or null.
    private static ITypeSymbol? Closing(INamedTypeSymbol type, INamedTypeSymbol? generic)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (Is(current.OriginalDefinition, generic))
            {
                return current.TypeArguments[0];
            }
        }

        return null;
    }

    // Whether the slice or a base type below Slice<T> overrides Key.
    private static bool PinsKey(INamedTypeSymbol type, INamedTypeSymbol? slice)
    {
        for (var current = type; !Is(current.OriginalDefinition, slice); current = current.BaseType!)
        {
            if (current.GetMembers("Key").Any(m => m.IsOverride))
            {
                return true;
            }
        }

        return false;
    }

    // Records synthesize an Equals override, so they need no case of their own; an anonymous type's Equals override is
    // not among its members.
    private static bool HasValueEquality(ITypeSymbol type)
    {
        if (type.IsAnonymousType)
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers("Equals").Any(m => m.IsOverride))
            {
                return true;
            }
        }

        return false;
    }

    // A return inside a lambda or local function returns that function's value, not the method's.
    private static bool InNestedFunction(IOperation operation)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent.Kind is OperationKind.AnonymousFunction or OperationKind.LocalFunction)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record Symbols(INamedTypeSymbol? Slice, INamedTypeSymbol? EffectGroup, INamedTypeSymbol? Effect, INamedTypeSymbol? Persist);
}

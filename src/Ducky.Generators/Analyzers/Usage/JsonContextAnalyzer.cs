using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Analyzers.Usage;
using Ducky.Generators.Registration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY006 and DUCKY025 (SPEC §10, §16.2, ADR-0022), on <c>UseJson(resolver)</c> given a <c>JsonSerializerContext</c>
/// (<c>X.Default</c>) or <c>JsonTypeInfoResolver.Combine(...)</c> over contexts. DUCKY006: a <c>[DuckyAction]</c> or slice
/// state type of this compilation, other than one naming a type parameter, outside the covered set, the <c>T</c> of every <c>JsonTypeInfo&lt;T&gt;</c> property the
/// contexts declare, which is exactly what the System.Text.Json generator emits and <c>GetTypeInfo</c> answers; so this is
/// an analyzer, which sees that generator's output, never a generator. A <c>Combine</c> with any other resolver is not
/// checked. DUCKY025: a context in a <c>Combine</c> whose <c>[JsonSourceGenerationOptions]</c> sets a named option
/// (<c>GenerationMode</c> aside, a source-generation setting) or <c>JsonSerializerDefaults</c> other than <c>General</c>,
/// which <c>Combine</c> drops.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonContextAnalyzer : DiagnosticAnalyzer
{
    private const string UseJson = "UseJson";
    private const string Combine = "Combine";

    // JsonSerializerDefaults.General.
    private const int General = 0;

    // A source-generation setting, not a serializer option: Combine drops nothing of it.
    private const string GenerationMode = "GenerationMode";

    // typeof(Count?) is CS8639, so a reference type's nullable annotation never reaches a message.
    private static readonly SymbolDisplayFormat _typeFormat =
        SymbolDisplayFormat.CSharpErrorMessageFormat.RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UsageDiagnostics.NotCovered, UsageDiagnostics.OptionsDropped];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Unresolved without Ducky or System.Text.Json: a null type never matches, so nothing is reported.
            var compilation = start.Compilation;
            var types = new Types(
                compilation.GetTypeByMetadataName("Ducky.DuckyBuilder"),
                compilation.GetTypeByMetadataName("Ducky.DuckyActionAttribute"),
                compilation.GetTypeByMetadataName("Ducky.Slice`1"),
                compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSerializerContext"),
                compilation.GetTypeByMetadataName("System.Text.Json.Serialization.JsonSourceGenerationOptionsAttribute"),
                compilation.GetTypeByMetadataName("System.Text.Json.Serialization.Metadata.JsonTypeInfoResolver"),
                compilation.GetTypeByMetadataName("System.Text.Json.Serialization.Metadata.JsonTypeInfo`1"));
            start.RegisterOperationAction(c => Analyze(c, types), OperationKind.Invocation);
        });
    }

    private static void Analyze(OperationAnalysisContext context, Types types)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name != UseJson || !SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, types.Builder))
        {
            return;
        }

        // Both overloads take one argument; a Combine's resolvers are its elements, anything else is the one resolver.
        var argument = invocation.Arguments[0].Value;
        var combine = Unwrap(argument) as IInvocationOperation;
        var isCombine = combine?.TargetMethod.Name == Combine && SymbolEqualityComparer.Default.Equals(combine.TargetMethod.ContainingType, types.Resolver);
        var resolvers = isCombine ? Elements(Unwrap(combine!.Arguments[0].Value)) : [argument];
        var contexts = resolvers.Select(r => Context(r, types.Context)).ToList();
        if (isCombine)
        {
            ReportDroppedOptions(context, types, resolvers, contexts);
        }

        if (contexts.All(c => c is not null))
        {
            ReportNotCovered(context, types, argument, contexts.OfType<INamedTypeSymbol>());
        }
    }

    private static void ReportNotCovered(OperationAnalysisContext context, Types types, IOperation argument, IEnumerable<INamedTypeSymbol> contexts)
    {
        var covered = contexts.SelectMany(c => c.GetMembers()).OfType<IPropertySymbol>().Select(p => p.Type).OfType<INamedTypeSymbol>()
            .Where(t => SymbolEqualityComparer.Default.Equals(t.OriginalDefinition, types.TypeInfo))
            .Select(t => t.TypeArguments[0])
            .ToImmutableHashSet(SymbolEqualityComparer.Default);
        var required = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in context.Compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type, context.CancellationToken).OfType<INamedTypeSymbol>())
        {
            if (type.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, types.Action)))
            {
                required.Add(type);
            }

            required.UnionWith(States(type, types.Slice));
        }

        foreach (var type in required.Where(t => !IsOpen(t) && !covered.Contains(t)))
        {
            context.ReportDiagnostic(Diagnostic.Create(UsageDiagnostics.NotCovered, argument.Syntax.GetLocation(), type.ToDisplayString(_typeFormat)));
        }
    }

    private static void ReportDroppedOptions(OperationAnalysisContext context, Types types, ImmutableArray<IOperation> resolvers, List<INamedTypeSymbol?> contexts)
    {
        // Rebuilt from the resolvers, so a Combine written over several lines still gives a one-line message.
        var fix = "JsonTypeInfoResolver.Combine(" + string.Join(", ", resolvers.Select(r => r.Syntax.ToString())) + ")";
        for (var i = 0; i < resolvers.Length; i++)
        {
            var options = contexts[i]?.GetAttributes().Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, types.Options)) ?? [];
            if (options.Any(a => a.NamedArguments.Any(n => n.Key != GenerationMode) || a.ConstructorArguments.Any(c => !General.Equals(c.Value))))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    UsageDiagnostics.OptionsDropped, resolvers[i].Syntax.GetLocation(), contexts[i]!.ToDisplayString(), fix));
            }
        }
    }

    // The resolvers of a Combine: its params array or collection expression; an array whose elements aren't written out
    // gives none, so DUCKY006 checks nothing.
    private static ImmutableArray<IOperation> Elements(IOperation resolvers) => resolvers switch
    {
        IArrayCreationOperation { Initializer: not null } array => array.Initializer.ElementValues,
        ICollectionExpressionOperation collection => collection.Elements,
        _ => [resolvers],
    };

    // The context type of a resolver expression (`X.Default`), or null when it isn't a JsonSerializerContext.
    private static INamedTypeSymbol? Context(IOperation resolver, INamedTypeSymbol? context) => Unwrap(resolver).Type switch
    {
        INamedTypeSymbol type when RegistrationModel.Derives(type, context) => type,
        _ => null,
    };

    // The implicit conversion to IJsonTypeInfoResolver (or to its array element type) around an argument.
    private static IOperation Unwrap(IOperation operation) => (operation as IConversionOperation)?.Operand ?? operation;

    // A type that names a type parameter (T, T[], List<T>, Outer<T>.Inner) can't go in typeof(...) of a context.
    private static bool IsOpen(ITypeSymbol? type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => IsOpen(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(IsOpen) || IsOpen(named.ContainingType),
        _ => false,
    };

    // The TState of every Slice<TState> the type derives from; an open one is dropped by the caller.
    private static IEnumerable<ITypeSymbol> States(INamedTypeSymbol type, INamedTypeSymbol? slice)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, slice))
            {
                yield return current.TypeArguments[0];
            }
        }
    }

    // The well-known types, resolved once per compilation.
    private sealed record Types(
        INamedTypeSymbol? Builder,
        INamedTypeSymbol? Action,
        INamedTypeSymbol? Slice,
        INamedTypeSymbol? Context,
        INamedTypeSymbol? Options,
        INamedTypeSymbol? Resolver,
        INamedTypeSymbol? TypeInfo);
}

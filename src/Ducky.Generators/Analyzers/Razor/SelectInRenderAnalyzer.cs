using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ducky.Generators.Analyzers.Razor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY009 (SPEC §11.2, §16.2): a <c>DuckyComponent</c>/<c>DuckyLayout</c> <c>Select</c> in <c>BuildRenderTree</c>
/// (markup), <c>OnParametersSet(Async)</c>, <c>OnAfterRender(Async)</c> or an event handler runs after the first render,
/// when registration is closed (INV-20, DUCKY352). Markup exists only in <c>&lt;auto-generated&gt;</c> Razor output, so
/// generated code is analysed, and the diagnostic lands at the <c>#line</c>-mapped <c>.razor</c> location.
/// </summary>
/// <remarks>
/// An event handler is a method a callback argument of <c>EventCallbackFactory</c> (or its binder extensions) or of
/// <c>RuntimeHelpers.CreateInferredEventCallback</c> names or calls, which is what Razor emits for <c>@onclick</c>,
/// <c>@bind:after</c>, <c>@bind:set</c> and <c>EventCallback</c> parameters, generic ones included. A <c>Select</c> in a
/// templated delegate (<c>RenderFragment</c>) runs during render wherever it is declared. Limit: handlers and
/// <c>Select</c> calls are matched within one type (its partial declarations included), so a handler inherited from a
/// base component and named only in a derived component's markup is not reported; DUCKY352 still throws at run time.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SelectInRenderAnalyzer : DiagnosticAnalyzer
{
    private const string Select = "Select";
    private const string CreateInferredEventCallback = "CreateInferredEventCallback";

    // The renderer calls these overrides after (or for) a render; OnInitialized(Async) and SetParametersAsync come first.
    private static readonly ImmutableHashSet<string> _afterFirstRender =
        ["BuildRenderTree", "OnParametersSet", "OnParametersSetAsync", "OnAfterRender", "OnAfterRenderAsync"];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => SelectDiagnostics.Supported;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // Stryker disable once Statement : equivalent, Analyze | ReportDiagnostics is also Roslyn's default when unconfigured (RS1025 asks to state it)
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // An unresolved type is null and never matches, so a compilation without Ducky.Blazor needs no branch.
            var compilation = start.Compilation;
            var types = new Types(
                [compilation.GetTypeByMetadataName("Ducky.Blazor.DuckyComponent"), compilation.GetTypeByMetadataName("Ducky.Blazor.DuckyLayout")],
                [
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.EventCallbackFactory"),
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.EventCallbackFactoryBinderExtensions"),
                ],
                compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.CompilerServices.RuntimeHelpers"),
                [
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment"),
                    compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RenderFragment`1"),
                ]);
            start.RegisterSymbolStartAction(type => AnalyzeType(type, types), SymbolKind.NamedType);
        });
    }

    // Per type: the Select calls with the member that owns each, and the methods its event callbacks reference; a
    // handler may be declared after the markup that names it (a code-behind partial), so reporting waits for the end.
    private static void AnalyzeType(SymbolStartAnalysisContext type, Types types)
    {
        var selects = new ConcurrentQueue<(ISymbol Member, bool InFragment, Location Location)>();
        var handlers = new ConcurrentQueue<IMethodSymbol>();
        type.RegisterOperationAction(
            operation =>
            {
                var invocation = (IInvocationOperation)operation.Operation;
                var target = invocation.TargetMethod;
                if (target.Name == Select && types.Owners.Contains(target.ContainingType, SymbolEqualityComparer.Default))
                {
                    // The owner is a method, or a field whose initializer calls Select on another instance.
                    selects.Enqueue((operation.ContainingSymbol, InFragment(invocation, types.Fragments), invocation.Syntax.GetLocation()));
                }

                if (types.Factories.Contains(target.ContainingType, SymbolEqualityComparer.Default)
                    || (target.Name == CreateInferredEventCallback && SymbolEqualityComparer.Default.Equals(target.ContainingType, types.RuntimeHelpers)))
                {
                    // The definition, so a generic handler (`@onclick="Pick<int>"`) matches the member its Select is in.
                    foreach (var method in invocation.Arguments.SelectMany(a => a.Descendants()).Select(Referenced).OfType<IMethodSymbol>())
                    {
                        handlers.Enqueue(method.OriginalDefinition);
                    }
                }
            },
            OperationKind.Invocation);
        type.RegisterSymbolEndAction(end =>
        {
            foreach (var (member, inFragment, location) in selects)
            {
                if (inFragment || (member.IsOverride && _afterFirstRender.Contains(member.Name)) || handlers.Contains(member, SymbolEqualityComparer.Default))
                {
                    // A property getter's templated delegate is named after the property.
                    var name = ((member as IMethodSymbol)?.AssociatedSymbol ?? member).Name;
                    end.ReportDiagnostic(Diagnostic.Create(SelectDiagnostics.SelectAfterFirstRender, location, name));
                }
            }
        });
    }

    // A method group (`@onclick="Pick"`) or a call inside a handler lambda (`@onclick="() => Pick(2)"`).
    private static IMethodSymbol? Referenced(IOperation operation) => operation switch
    {
        IMethodReferenceOperation reference => reference.Method,
        IInvocationOperation call => call.TargetMethod,
        _ => null,
    };

    // Inside a delegate created as RenderFragment or RenderFragment<T>: a templated Razor delegate (`@<p>...</p>`) or a
    // child-content fragment, whose body runs when the component renders, wherever it is declared. A delegate creation's
    // Type is never null, and no pattern variable is declared, so no mutant of the condition breaks definite assignment.
    // ponytail: any RenderFragment creation above the call counts, so `new RenderFragment(Make(Select(...)))` in
    // OnInitialized would be flagged too; also require a lambda Target if that shape ever turns up.
    private static bool InFragment(IOperation operation, ImmutableArray<INamedTypeSymbol?> fragments)
    {
        for (var parent = operation.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is IDelegateCreationOperation && fragments.Contains(parent.Type!.OriginalDefinition, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        return false;
    }

    // The well-known types, resolved once per compilation; each is null in a compilation without Blazor.
    private sealed record Types(
        ImmutableArray<INamedTypeSymbol?> Owners,
        ImmutableArray<INamedTypeSymbol?> Factories,
        INamedTypeSymbol? RuntimeHelpers,
        ImmutableArray<INamedTypeSymbol?> Fragments);
}

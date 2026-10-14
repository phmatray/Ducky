using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Draft.Generators;

/// <summary>
/// DUCKY109 (SPEC §16.2): inside a <c>Produce</c> recipe, an expression statement discards the result of a call whose
/// receiver is a plain get/set member of a draft (a draft or collection draft: anything implementing
/// Ducky.Draft.IRevocable) and whose return type is the member's type (<c>d.Pending.Enqueue(job);</c> on an
/// <c>ImmutableQueue&lt;T&gt;</c>). The method returns a new instance, so the edit is lost and <c>Produce</c> returns the base.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DraftDiscardAnalyzer : DiagnosticAnalyzer
{
    private const string Produce = "Produce";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [DraftDiagnostics.Discard];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // A component's @code block compiles to generated C# (*.razor.g.cs); the Draft generator emits no recipe.
        // Stryker disable once Statement : equivalent, Analyze | ReportDiagnostics is also Roslyn's default when unconfigured (RS1025 asks to state it)
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Without the Ducky.Draft runtime both are null and match no type, so nothing is reported.
            var draft = start.Compilation.GetTypeByMetadataName("Ducky.Draft.IDraft`1");
            var revocable = start.Compilation.GetTypeByMetadataName("Ducky.Draft.IRevocable");
            start.RegisterOperationAction(c => Analyze(c, draft, revocable), OperationKind.ExpressionStatement);
        });
    }

    // Pattern variables live only in case labels, so no mutant of a condition can leave one unassigned.
    // ponytail: the receiver must be the member itself; an alias (`var q = d.Pending; q.Enqueue(j);`), an extension
    // method and a conditional access (`d.Pending?.Enqueue(j);`) are not followed.
    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol? draft, INamedTypeSymbol? revocable)
    {
        switch (((IExpressionStatementOperation)context.Operation).Operation)
        {
            case IInvocationOperation { Instance: IPropertyReferenceOperation member } call
                when member.Property.SetMethod is not null
                    && SymbolEqualityComparer.Default.Equals(call.Type, member.Type)
                    && Implements(member.Instance?.Type, revocable)
                    && !Implements(member.Type, revocable)
                    && InRecipe(call, draft):
                context.ReportDiagnostic(Diagnostic.Create(
                    DraftDiagnostics.Discard, call.Syntax.GetLocation(), member.Syntax.ToString(), call.TargetMethod.Name));
                break;
        }
    }

    // Inside a lambda passed to a Produce call whose parameter is a draft, nested lambdas and local functions included.
    private static bool InRecipe(IOperation? operation, INamedTypeSymbol? draft) => operation switch
    {
        null => false,
        IAnonymousFunctionOperation { Parent: IDelegateCreationOperation { Parent: IArgumentOperation { Parent: IInvocationOperation { TargetMethod.Name: Produce } } } } recipe
            when recipe.Symbol.Parameters.Any(p => Implements(p.Type, draft)) => true,
        _ => InRecipe(operation.Parent, draft),
    };

    // A static member's receiver is null: no draft.
    private static bool Implements(ITypeSymbol? type, INamedTypeSymbol? target) =>
        type?.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, target)) == true;
}

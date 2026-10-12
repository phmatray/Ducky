using System.Collections.Immutable;
using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY010 (SPEC §7, §16.2, ADR-0014): blocking on the task of a Ducky, Ducky.Blazor or Ducky.Testing
/// <c>DispatchAsync</c>, <c>WhenIdleAsync</c> or <c>InitializeAsync</c> with <c>.Result</c>, <c>.Wait()</c> or
/// <c>GetAwaiter().GetResult()</c> (through <c>ConfigureAwait</c> too): on the drainer that is a self-deadlock. The
/// SetAsync arm ships with SliceStore's analyzer arms.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class BlockingAnalyzer : DiagnosticAnalyzer
{
    private const string Result = "Result";
    private const string Wait = "Wait";
    private const string GetResult = "GetResult";
    private const string GetAwaiter = "GetAwaiter";
    private const string ConfigureAwait = "ConfigureAwait";

    private static readonly ImmutableHashSet<string> _blocked = ["DispatchAsync", "WhenIdleAsync", "InitializeAsync"];

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UsageDiagnostics.Blocking];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // The assemblies whose methods count: Ducky's, Ducky.Blazor's and Ducky.Testing's (TestStore forwards to the
            // store). One that doesn't resolve is null and never matches a method's containing assembly.
            var compilation = start.Compilation;
            ImmutableArray<IAssemblySymbol?> owners =
            [
                compilation.GetTypeByMetadataName("Ducky.IDispatcher")?.ContainingAssembly,
                compilation.GetTypeByMetadataName("Ducky.Blazor.DuckyComponent")?.ContainingAssembly,
                compilation.GetTypeByMetadataName("Ducky.Testing.TestStore")?.ContainingAssembly,
            ];
            start.RegisterOperationAction(c => Analyze(c, owners), OperationKind.Invocation, OperationKind.PropertyReference);
        });
    }

    // Pattern variables live only in case labels, so no mutant of a condition can leave one unassigned.
    private static void Analyze(OperationAnalysisContext context, ImmutableArray<IAssemblySymbol?> owners)
    {
        switch (context.Operation)
        {
            case IPropertyReferenceOperation { Property.Name: Result } property:
                Report(context, owners, property.Instance, ".Result");
                break;
            case IInvocationOperation { TargetMethod.Name: Wait } wait:
                Report(context, owners, wait.Instance, ".Wait()");
                break;
            case IInvocationOperation { TargetMethod.Name: GetResult, Instance: IInvocationOperation { TargetMethod.Name: GetAwaiter } awaiter }:
                Report(context, owners, awaiter.Instance, "GetAwaiter().GetResult()");
                break;
        }
    }

    // `task` is what the blocking member is called on: a blocked call, maybe behind ConfigureAwait.
    // ponytail: only a call written in place is seen; `var t = store.DispatchAsync(a); t.Wait();` needs data flow.
    private static void Report(OperationAnalysisContext context, ImmutableArray<IAssemblySymbol?> owners, IOperation? task, string how)
    {
        var call = task as IInvocationOperation;
        while (call?.TargetMethod.Name == ConfigureAwait)
        {
            call = call.Instance as IInvocationOperation;
        }

        if (call is not null && _blocked.Contains(call.TargetMethod.Name) && owners.Contains(call.TargetMethod.ContainingAssembly, SymbolEqualityComparer.Default))
        {
            context.ReportDiagnostic(Diagnostic.Create(UsageDiagnostics.Blocking, context.Operation.Syntax.GetLocation(), call.TargetMethod.Name, how));
        }
    }
}

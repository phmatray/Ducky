using System;
using System.Collections.Immutable;
using Ducky.Generators.Analyzers.Usage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY004 (SPEC §7, §16.2, ADR-0025): <c>Lifetime = ServiceLifetime.Singleton</c> on the Ducky builder in a web project
/// that is not Blazor WebAssembly, where one store would serve every user. The project kind comes from the
/// <c>CompilerVisibleProperty</c> items of <c>buildTransitive/Ducky.targets</c>. Only a constant Singleton is reported:
/// a value computed at run time (<c>OperatingSystem.IsBrowser() ? …</c>) is not.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class LifetimeAnalyzer : DiagnosticAnalyzer
{
    private const string Web = "build_property.UsingMicrosoftNETSdkWeb";
    private const string Wasm = "build_property.UsingMicrosoftNETSdkBlazorWebAssembly";
    private const string Lifetime = "Lifetime";

    // ServiceLifetime.Singleton.
    private const int Singleton = 0;

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UsageDiagnostics.SingletonOnServer];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var options = start.Options.AnalyzerConfigOptionsProvider.GlobalOptions;
            if (IsTrue(options, Web) && !IsTrue(options, Wasm))
            {
                // Unresolved without Ducky: null never equals a property, so nothing is reported.
                var builder = start.Compilation.GetTypeByMetadataName("Ducky.DuckyBuilder");
                start.RegisterOperationAction(c => Analyze(c, builder), OperationKind.SimpleAssignment);
            }
        });
    }

    // An out argument is assigned whatever TryGetValue returns, so no mutant breaks definite assignment.
    private static bool IsTrue(AnalyzerConfigOptions options, string key) =>
        options.TryGetValue(key, out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    // The pattern variable lives in a case label, so no mutant of the condition can leave it unassigned.
    private static void Analyze(OperationAnalysisContext context, INamedTypeSymbol? builder)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        switch (assignment.Target)
        {
            case IPropertyReferenceOperation property
                when property.Property.Name == Lifetime && SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, builder)
                    && Singleton.Equals(assignment.Value.ConstantValue.Value):
                context.ReportDiagnostic(Diagnostic.Create(UsageDiagnostics.SingletonOnServer, assignment.Syntax.GetLocation()));
                break;
        }
    }
}

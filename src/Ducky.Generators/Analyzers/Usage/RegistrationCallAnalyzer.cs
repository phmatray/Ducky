using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Ducky.Generators.Analyzers.Usage;
using Ducky.Generators.Registration;
using Ducky.Shared.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Ducky.Generators;

/// <summary>
/// DUCKY016 (SPEC §16.2, ADR-0017): the assembly defines a slice or effect its generated <c>AddDuckyGenerated_{Asm}</c>
/// registers (SPEC §16.1) and calls <c>AddDucky</c>, but never calls that method, so none of them is registered. Reported at each
/// <c>AddDucky</c> call once the whole compilation is seen.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RegistrationCallAnalyzer : DiagnosticAnalyzer
{
    private const string AddDucky = "AddDucky";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [UsageDiagnostics.RegistrationNotCalled];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Stryker disable once Statement : equivalent, concurrent execution is a scheduling hint with no observable result
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // Unresolved without Ducky: a null type never matches, so nothing is reported.
            var compilation = start.Compilation;
            var extensions = compilation.GetTypeByMetadataName("Ducky.DuckyServiceCollectionExtensions");
            var assembly = AssemblyNameSanitizer.Sanitize(compilation.AssemblyName);
            var generated = "AddDuckyGenerated_" + assembly;
            var calls = new ConcurrentQueue<Location>();
            var called = new Flag();
            start.RegisterOperationAction(
                c =>
                {
                    var method = ((IInvocationOperation)c.Operation).TargetMethod;
                    if (method.Name == AddDucky && SymbolEqualityComparer.Default.Equals(method.ContainingType, extensions))
                    {
                        calls.Enqueue(c.Operation.Syntax.GetLocation());
                    }

                    if (method.Name == generated)
                    {
                        Interlocked.Exchange(ref called.Value, 1);
                    }
                },
                OperationKind.Invocation);
            start.RegisterCompilationEndAction(end =>
            {
                if (Volatile.Read(ref called.Value) == 1 || !DefinesRegistrations(end.Compilation, end.CancellationToken))
                {
                    return;
                }

                foreach (var location in calls)
                {
                    end.ReportDiagnostic(Diagnostic.Create(UsageDiagnostics.RegistrationNotCalled, location, assembly));
                }
            });
        });
    }

    // A slice or effect of this compilation that the generated method registers, by the generator's own rule: a private
    // nested, generic, `file` or constructor-less one isn't, so it doesn't count.
    private static bool DefinesRegistrations(Compilation compilation, CancellationToken cancellationToken) =>
        compilation.GetSymbolsWithName(_ => true, SymbolFilter.Type, cancellationToken).OfType<INamedTypeSymbol>()
            .Any(t => RegistrationModel.Classify(t, compilation) is { Excluded: null });

    // Set once any action sees the generated call; read when the compilation ends.
    private sealed class Flag
    {
        public int Value;
    }
}
